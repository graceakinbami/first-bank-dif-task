namespace NovaPay.Api.Domain;

/// <summary>
/// A record of a successfully completed wallet-to-wallet transfer, keyed by the client-supplied
/// Idempotency-Key. Only completed transfers are persisted here: a failed attempt (insufficient
/// funds, limit exceeded, unknown wallet) moves no money, so it leaves nothing that needs
/// deduplicating and the client is free to retry the same key once conditions change.
/// </summary>
public class Transfer
{
    public Guid Id { get; set; }

    public required string IdempotencyKey { get; set; }

    /// <summary>SHA-256 hex hash of the normalized request payload tied to this idempotency key.</summary>
    public required string RequestFingerprint { get; set; }

    public Guid FromWalletId { get; set; }
    public Guid ToWalletId { get; set; }
    public long AmountKobo { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
