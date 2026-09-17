namespace NovaPay.Api.Common;

public class NovaPayOptions
{
    public const string SectionName = "NovaPay";

    /// <summary>Default per-wallet daily outbound transfer limit, in kobo. CBN Tier-2-style default: ₦500,000/day.</summary>
    public long DailyTransferLimitKobo { get; set; } = 50_000_000;

    /// <summary>Hardcoded bearer token accepted by every endpoint. A stand-in for real OAuth2/JWT issuance.</summary>
    public string BearerToken { get; set; } = "novapay-dev-token";
}
