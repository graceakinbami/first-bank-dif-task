using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NovaPay.Tests;

/// <summary>
/// Spins up the real API pipeline (auth, middleware, controllers, EF Core) against a fresh,
/// file-based SQLite database unique to this factory instance, so tests never share state and
/// can run with real transactions instead of mocking the database away.
/// </summary>
public class NovaPayApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"novapay-tests-{Guid.NewGuid():N}.db");

    public const string ValidToken = "novapay-dev-token";

    /// <summary>
    /// Set before the first CreateClient() call to replace the real system clock with a fake one,
    /// letting a test move "now" across a WAT day boundary and assert the daily limit actually
    /// resets there — something no amount of testing WatClock's pure math in isolation can prove.
    /// </summary>
    public FakeTimeProvider? TimeProvider { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:NovaPay"] = $"Data Source={_dbPath};Default Timeout=30",
                ["NovaPay:BearerToken"] = ValidToken,
                ["NovaPay:DailyTransferLimitKobo"] = "50000000"
            });
        });

        if (TimeProvider is not null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<System.TimeProvider>();
                services.AddSingleton<System.TimeProvider>(TimeProvider);
            });
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_dbPath))
                File.Delete(_dbPath);
        }
        catch
        {
            // Best-effort cleanup; a lingering temp file never affects test correctness.
        }
    }
}
