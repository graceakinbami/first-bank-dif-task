namespace NovaPay.Api.Domain;

public class Wallet
{
    public Guid Id { get; set; }
    public string OwnerName { get; set; } = string.Empty;

    /// <summary>Balance held in kobo (1 Naira = 100 kobo) to avoid floating-point drift.</summary>
    public long BalanceKobo { get; set; }

    /// <summary>
    /// Per-wallet override of the daily outbound transfer limit, in kobo.
    /// Null means the system default (see NovaPayOptions.DailyTransferLimitKobo) applies.
    /// Real NovaPay tiers this by KYC level (BVN/NIN-verified tiers get higher limits per CBN guidelines).
    /// </summary>
    public long? DailyTransferLimitKoboOverride { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
