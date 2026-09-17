using System.Security.Cryptography;
using System.Text;

namespace NovaPay.Api.Common;

/// <summary>
/// Ties an Idempotency-Key to the specific request payload it was issued for, so a key reused
/// with a different body (a client bug, or a hostile replay) is rejected instead of silently
/// replaying the wrong result.
/// </summary>
public static class IdempotencyFingerprint
{
    public static string Compute(Guid fromWalletId, Guid toWalletId, long amountKobo)
    {
        var raw = $"{fromWalletId:N}|{toWalletId:N}|{amountKobo}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
