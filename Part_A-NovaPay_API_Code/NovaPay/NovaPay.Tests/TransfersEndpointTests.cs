using System.Net;
using System.Net.Http.Json;
using NovaPay.Api.Dtos;

namespace NovaPay.Tests;

public class TransfersEndpointTests : IClassFixture<NovaPayApiFactory>
{
    private readonly NovaPayApiFactory _factory;
    private readonly HttpClient _client;

    public TransfersEndpointTests(NovaPayApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient().WithBearerToken(NovaPayApiFactory.ValidToken);
    }

    private async Task<WalletResponse> CreateWalletAsync(string owner, long startingBalanceKobo = 0)
    {
        var wallet = await (await _client.PostAsJsonAsync("/wallets", new CreateWalletRequest(owner)))
            .Content.ReadFromJsonAsync<WalletResponse>();
        if (startingBalanceKobo > 0)
            await _client.PostAsJsonAsync($"/wallets/{wallet!.Id}/credit", new CreditWalletRequest(startingBalanceKobo));
        return wallet!;
    }

    [Fact]
    public async Task Transfer_MovesFundsAtomically()
    {
        var from = await CreateWalletAsync("From1", 1_000_000);
        var to = await CreateWalletAsync("To1");

        var response = await _client.PostTransferAsync(
            new CreateTransferRequest(from.Id, to.Id, 300_000), idempotencyKey: Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var transfer = await response.Content.ReadFromJsonAsync<TransferResponse>();
        Assert.Equal(300_000, transfer!.AmountKobo);

        var fromAfter = await (await _client.GetAsync($"/wallets/{from.Id}")).Content.ReadFromJsonAsync<WalletResponse>();
        var toAfter = await (await _client.GetAsync($"/wallets/{to.Id}")).Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(700_000, fromAfter!.BalanceKobo);
        Assert.Equal(300_000, toAfter!.BalanceKobo);
    }

    [Fact]
    public async Task Transfer_MissingIdempotencyKey_ReturnsBadRequest()
    {
        var from = await CreateWalletAsync("From2", 1_000_000);
        var to = await CreateWalletAsync("To2");

        var response = await _client.PostTransferAsync(new CreateTransferRequest(from.Id, to.Id, 1000), idempotencyKey: null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_InsufficientFunds_LeavesBalancesUnchanged()
    {
        var from = await CreateWalletAsync("From3", 1000);
        var to = await CreateWalletAsync("To3");

        var response = await _client.PostTransferAsync(
            new CreateTransferRequest(from.Id, to.Id, 1_000_000), idempotencyKey: Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var fromAfter = await (await _client.GetAsync($"/wallets/{from.Id}")).Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(1000, fromAfter!.BalanceKobo);
    }

    // Regression guard: AcquirePairAsync used to wait twice on the same non-reentrant semaphore
    // for a self-transfer, deadlocking the request forever. The Timeout makes that failure mode
    // show up as a failed test instead of a test run that never completes.
    [Fact(Timeout = 5000)]
    public async Task Transfer_ToSameWallet_SucceedsAndLeavesBalanceUnchanged()
    {
        var wallet = await CreateWalletAsync("Solo", 1000);

        var response = await _client.PostTransferAsync(
            new CreateTransferRequest(wallet.Id, wallet.Id, 100), idempotencyKey: Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var after = await (await _client.GetAsync($"/wallets/{wallet.Id}")).Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(1000, after!.BalanceKobo);
    }

    [Fact]
    public async Task Transfer_UnknownWallet_ReturnsNotFound()
    {
        var from = await CreateWalletAsync("From4", 1000);

        var response = await _client.PostTransferAsync(
            new CreateTransferRequest(from.Id, Guid.NewGuid(), 100), idempotencyKey: Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Transfer_RetriedWithSameIdempotencyKeyAndPayload_ReplaysWithoutDoubleDebit()
    {
        var from = await CreateWalletAsync("From5", 1_000_000);
        var to = await CreateWalletAsync("To5");
        var key = Guid.NewGuid().ToString();
        var body = new CreateTransferRequest(from.Id, to.Id, 200_000);

        var first = await _client.PostTransferAsync(body, key);
        var second = await _client.PostTransferAsync(body, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstTransfer = await first.Content.ReadFromJsonAsync<TransferResponse>();
        var secondTransfer = await second.Content.ReadFromJsonAsync<TransferResponse>();
        Assert.Equal(firstTransfer!.Id, secondTransfer!.Id);

        var fromAfter = await (await _client.GetAsync($"/wallets/{from.Id}")).Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(800_000, fromAfter!.BalanceKobo); // debited exactly once
    }

    [Fact]
    public async Task Transfer_SameIdempotencyKeyDifferentPayload_ReturnsConflict()
    {
        var from = await CreateWalletAsync("From6", 1_000_000);
        var to = await CreateWalletAsync("To6");
        var key = Guid.NewGuid().ToString();

        var first = await _client.PostTransferAsync(new CreateTransferRequest(from.Id, to.Id, 100_000), key);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostTransferAsync(new CreateTransferRequest(from.Id, to.Id, 200_000), key);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Transfer_ExceedingDailyLimit_IsRejected()
    {
        var from = await CreateWalletAsync("From7", 200_000_000); // well above the ₦500,000/day limit
        var to = await CreateWalletAsync("To7");

        var withinLimit = await _client.PostTransferAsync(
            new CreateTransferRequest(from.Id, to.Id, 50_000_000), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, withinLimit.StatusCode);

        var overLimit = await _client.PostTransferAsync(
            new CreateTransferRequest(from.Id, to.Id, 1), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overLimit.StatusCode);
    }

    [Fact]
    public async Task ConcurrentTransfers_NeverOverdrawTheWallet()
    {
        // Balance covers exactly 5 transfers of 100,000 kobo; fire 20 concurrently and verify
        // that no more than 5 ever succeed, regardless of scheduling order.
        var from = await CreateWalletAsync("Racer", 500_000);
        var to = await CreateWalletAsync("Sink");

        var tasks = Enumerable.Range(0, 20).Select(_ => _client.PostTransferAsync(
            new CreateTransferRequest(from.Id, to.Id, 100_000), Guid.NewGuid().ToString()));
        var results = await Task.WhenAll(tasks);

        var succeeded = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rejected = results.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);

        Assert.Equal(5, succeeded);
        Assert.Equal(15, rejected);

        var fromAfter = await (await _client.GetAsync($"/wallets/{from.Id}")).Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(0, fromAfter!.BalanceKobo);
        Assert.True(fromAfter.BalanceKobo >= 0);
    }
}
