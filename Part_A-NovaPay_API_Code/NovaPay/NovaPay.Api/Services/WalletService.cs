using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NovaPay.Api.Common;
using NovaPay.Api.Data;
using NovaPay.Api.Domain;

namespace NovaPay.Api.Services;

public class WalletService(
    NovaPayDbContext db,
    WalletLock walletLock,
    IOptions<NovaPayOptions> options,
    TimeProvider timeProvider,
    ILogger<WalletService> logger) : IWalletService
{
    private readonly NovaPayOptions _options = options.Value;

    public async Task<Wallet> CreateWalletAsync(string ownerName, CancellationToken ct)
    {
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            OwnerName = ownerName,
            BalanceKobo = 0,
            CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
        };

        db.Wallets.Add(wallet);
        await db.SaveChangesAsync(ct);
        return wallet;
    }

    public async Task<Wallet> GetWalletAsync(Guid walletId, CancellationToken ct)
    {
        var wallet = await db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.Id == walletId, ct);
        return wallet ?? throw new WalletNotFoundException(walletId);
    }

    public async Task<Wallet> CreditWalletAsync(Guid walletId, long amountKobo, CancellationToken ct)
    {
        using var _ = await walletLock.AcquireAsync(walletId, ct);

        var wallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == walletId, ct)
            ?? throw new WalletNotFoundException(walletId);

        wallet.BalanceKobo += amountKobo;
        await db.SaveChangesAsync(ct);
        return wallet;
    }

    public async Task<Transfer> TransferAsync(
        Guid fromWalletId, Guid toWalletId, long amountKobo, string idempotencyKey, CancellationToken ct)
    {
        var fingerprint = IdempotencyFingerprint.Compute(fromWalletId, toWalletId, amountKobo);

        using var _ = await walletLock.AcquirePairAsync(fromWalletId, toWalletId, ct);

        var existing = await db.Transfers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.RequestFingerprint != fingerprint)
                throw new IdempotencyKeyConflictException(idempotencyKey);

            logger.LogInformation("Replaying transfer {TransferId} for Idempotency-Key {Key}", existing.Id, idempotencyKey);
            return existing;
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var fromWallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == fromWalletId, ct)
                ?? throw new WalletNotFoundException(fromWalletId);
            var toWallet = await db.Wallets.FirstOrDefaultAsync(w => w.Id == toWalletId, ct)
                ?? throw new WalletNotFoundException(toWalletId);

            if (fromWallet.BalanceKobo < amountKobo)
                throw new InsufficientFundsException(fromWalletId);

            var dailyLimitKobo = fromWallet.DailyTransferLimitKoboOverride ?? _options.DailyTransferLimitKobo;
            var (dayStartUtc, dayEndUtc) = WatClock.GetWatDayBoundsUtc(timeProvider.GetUtcNow());
            var alreadySentTodayKobo = await db.Transfers
                .Where(t => t.FromWalletId == fromWalletId && t.CreatedAtUtc >= dayStartUtc && t.CreatedAtUtc < dayEndUtc)
                .SumAsync(t => (long?)t.AmountKobo, ct) ?? 0;

            if (alreadySentTodayKobo + amountKobo > dailyLimitKobo)
                throw new DailyLimitExceededException(fromWalletId, dailyLimitKobo);

            fromWallet.BalanceKobo -= amountKobo;
            toWallet.BalanceKobo += amountKobo;

            var transfer = new Transfer
            {
                Id = Guid.NewGuid(),
                IdempotencyKey = idempotencyKey,
                RequestFingerprint = fingerprint,
                FromWalletId = fromWalletId,
                ToWalletId = toWalletId,
                AmountKobo = amountKobo,
                CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime
            };
            db.Transfers.Add(transfer);

            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
            return transfer;
        }
        catch (DbUpdateException)
        {
            // Lost a race against another process inserting the same key (defense in depth beyond
            // the in-process WalletLock, which only protects a single instance).
            await dbTransaction.RollbackAsync(ct);
            var raced = await db.Transfers.AsNoTracking().FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);
            if (raced is null)
                throw;
            if (raced.RequestFingerprint != fingerprint)
                throw new IdempotencyKeyConflictException(idempotencyKey);
            return raced;
        }
        catch
        {
            await dbTransaction.RollbackAsync(ct);
            throw;
        }
    }
}
