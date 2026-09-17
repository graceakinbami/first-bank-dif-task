using System.Net;
using System.Net.Http.Json;
using NovaPay.Api.Dtos;

namespace NovaPay.Tests;

public class WalletsEndpointTests : IClassFixture<NovaPayApiFactory>
{
    private readonly HttpClient _client;

    public WalletsEndpointTests(NovaPayApiFactory factory)
    {
        _client = factory.CreateClient().WithBearerToken(NovaPayApiFactory.ValidToken);
    }

    [Fact]
    public async Task CreateWallet_StartsAtZeroBalance()
    {
        var response = await _client.PostAsJsonAsync("/wallets", new CreateWalletRequest("Ada"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var wallet = await response.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.Equal(0, wallet!.BalanceKobo);
        Assert.Equal("Ada", wallet.OwnerName);
        Assert.NotEqual(Guid.Empty, wallet.Id);
    }

    [Fact]
    public async Task CreateWallet_MissingOwnerName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/wallets", new CreateWalletRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWallet_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/wallets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Credit_IncreasesBalance_AndIsReflectedInGet()
    {
        var created = await (await _client.PostAsJsonAsync("/wallets", new CreateWalletRequest("Bola")))
            .Content.ReadFromJsonAsync<WalletResponse>();

        var creditResponse = await _client.PostAsJsonAsync(
            $"/wallets/{created!.Id}/credit", new CreditWalletRequest(150_000));
        Assert.Equal(HttpStatusCode.OK, creditResponse.StatusCode);
        var credited = await creditResponse.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(150_000, credited!.BalanceKobo);

        var fetched = await (await _client.GetAsync($"/wallets/{created.Id}")).Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(150_000, fetched!.BalanceKobo);
    }

    [Fact]
    public async Task Credit_NegativeAmount_ReturnsBadRequest()
    {
        var created = await (await _client.PostAsJsonAsync("/wallets", new CreateWalletRequest("Chidi")))
            .Content.ReadFromJsonAsync<WalletResponse>();

        var response = await _client.PostAsJsonAsync($"/wallets/{created!.Id}/credit", new CreditWalletRequest(-1));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Credit_ZeroAmount_IsAccepted_AndLeavesBalanceUnchanged()
    {
        var created = await (await _client.PostAsJsonAsync("/wallets", new CreateWalletRequest("Dubem")))
            .Content.ReadFromJsonAsync<WalletResponse>();

        var response = await _client.PostAsJsonAsync($"/wallets/{created!.Id}/credit", new CreditWalletRequest(0));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var credited = await response.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(0, credited!.BalanceKobo);
    }

    [Fact]
    public async Task Credit_UnknownWallet_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync($"/wallets/{Guid.NewGuid()}/credit", new CreditWalletRequest(1000));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-token")]
    public async Task Endpoints_RejectMissingOrInvalidToken(string? token)
    {
        using var factory = new NovaPayApiFactory();
        var anonClient = factory.CreateClient();
        if (token is not null)
            anonClient.WithBearerToken(token);

        var response = await anonClient.PostAsJsonAsync("/wallets", new CreateWalletRequest("Eve"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
