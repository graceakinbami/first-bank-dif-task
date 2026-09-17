using NovaPay.Api.Domain;

namespace NovaPay.Api.Services;

public interface IWalletService
{
    Task<Wallet> CreateWalletAsync(string ownerName, CancellationToken ct);

    Task<Wallet> GetWalletAsync(Guid walletId, CancellationToken ct);

    Task<Wallet> CreditWalletAsync(Guid walletId, long amountKobo, CancellationToken ct);

    /// <summary>
    /// Executes (or, on a retried Idempotency-Key, replays) a wallet-to-wallet transfer.
    /// Throws <see cref="Common.WalletNotFoundException"/>, <see cref="Common.InsufficientFundsException"/>,
    /// <see cref="Common.DailyLimitExceededException"/>, or <see cref="Common.IdempotencyKeyConflictException"/>
    /// as appropriate; a successful call always returns a persisted, completed <see cref="Transfer"/>.
    /// </summary>
    Task<Transfer> TransferAsync(Guid fromWalletId, Guid toWalletId, long amountKobo, string idempotencyKey, CancellationToken ct);
}
