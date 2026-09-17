using System.Net;
using System.Net.Http.Json;
using NovaPay.Api.Dtos;

namespace NovaPay.Tests;

/// <summary>
/// Proves the daily outbound limit resets at WAT midnight, not UTC midnight, end-to-end through
/// the live /transfers path — not just against WatClock's pure boundary math in isolation.
///
/// WAT is UTC+1, so WAT midnight for the Mar 4 -> Mar 5 rollover lands at 2026-03-04T23:00:00Z:
/// one hour *before* UTC midnight. The window from 23:00Z to 24:00Z on Mar 4 is therefore already
/// a new WAT calendar day while still being the same UTC calendar date. A daily-limit query that
/// (incorrectly) grouped transfers by UTC date would still block spending in that window; this
/// test fails against that bug and passes against the WAT-bounded implementation.
/// </summary>
public class WatDayBoundaryTests
{
    [Fact]
    public async Task DailyLimit_ResetsAtWatMidnight_NotUtcMidnight()
    {
        var beforeWatMidnightUtc = new DateTimeOffset(2026, 3, 4, 22, 0, 0, TimeSpan.Zero); // WAT 23:00 Mar 4 — still "WAT day Mar 4"
        var afterWatMidnightUtc = new DateTimeOffset(2026, 3, 4, 23, 30, 0, TimeSpan.Zero);  // WAT 00:30 Mar 5 — new WAT day, same UTC date (Mar 4)

        var fakeClock = new FakeTimeProvider(beforeWatMidnightUtc);
        using var factory = new NovaPayApiFactory { TimeProvider = fakeClock };
        var client = factory.CreateClient().WithBearerToken(NovaPayApiFactory.ValidToken);

        var from = await (await client.PostAsJsonAsync("/wallets", new CreateWalletRequest("Boundary-From")))
            .Content.ReadFromJsonAsync<WalletResponse>();
        var to = await (await client.PostAsJsonAsync("/wallets", new CreateWalletRequest("Boundary-To")))
            .Content.ReadFromJsonAsync<WalletResponse>();
        // Credit far above the daily limit so every check below is a limit check, never a balance check.
        await client.PostAsJsonAsync($"/wallets/{from!.Id}/credit", new CreditWalletRequest(200_000_000));

        // Spends 40M of the 50M WAT-day-Mar-4 allowance.
        var firstTransfer = await client.PostTransferAsync(
            new CreateTransferRequest(from.Id, to!.Id, 40_000_000), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, firstTransfer.StatusCode);

        // 40M + 20M = 60M > the 50M limit, still within the same WAT day -> correctly rejected.
        var secondSameDay = await client.PostTransferAsync(
            new CreateTransferRequest(from.Id, to.Id, 20_000_000), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondSameDay.StatusCode);

        // Cross into the next WAT day (still the same UTC calendar date).
        fakeClock.UtcNow = afterWatMidnightUtc;

        // A naive UTC-date grouping would still see 40M spent "today" and reject this; the WAT day
        // actually rolled over, so this 20M transfer has the full fresh 50M allowance available.
        var afterReset = await client.PostTransferAsync(
            new CreateTransferRequest(from.Id, to.Id, 20_000_000), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Created, afterReset.StatusCode);

        var fromAfter = await (await client.GetAsync($"/wallets/{from.Id}")).Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(200_000_000 - 40_000_000 - 20_000_000, fromAfter!.BalanceKobo);
    }
}
