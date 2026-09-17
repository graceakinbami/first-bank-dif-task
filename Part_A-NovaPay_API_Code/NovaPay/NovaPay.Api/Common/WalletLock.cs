using System.Collections.Concurrent;

namespace NovaPay.Api.Common;

/// <summary>
/// Per-wallet in-process async lock guarding balance mutations.
///
/// SQLite (used here for zero-setup durability) has no row-level locking, so a naive
/// read-check-write over EF Core can race: two concurrent transfers could both read the same
/// starting balance, both pass the "sufficient funds" check, and both debit — overdrawing the
/// wallet. Serializing writers per wallet id closes that race without giving up per-wallet
/// concurrency for unrelated wallets.
///
/// This only serializes within a single process. A multi-instance deployment on a real RDBMS
/// (Postgres/SQL Server) would instead rely on `SELECT ... FOR UPDATE` / row locking inside the
/// database transaction, which is the production-grade equivalent of what this class does in memory.
/// </summary>
public sealed class WalletLock
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();

    private SemaphoreSlim GetSemaphore(Guid walletId) =>
        _locks.GetOrAdd(walletId, _ => new SemaphoreSlim(1, 1));

    /// <summary>Acquires the lock for a single wallet (used by credit).</summary>
    public async Task<IDisposable> AcquireAsync(Guid walletId, CancellationToken ct)
    {
        var sem = GetSemaphore(walletId);
        await sem.WaitAsync(ct);
        return new Releaser(sem);
    }

    /// <summary>
    /// Acquires both wallets involved in a transfer, always in a fixed (Guid-ordered) sequence,
    /// so two transfers moving money in opposite directions between the same pair can never deadlock.
    ///
    /// A self-transfer (walletIdA == walletIdB) is the degenerate case of that pair: waiting twice
    /// on the same non-reentrant SemaphoreSlim would deadlock the request against itself forever,
    /// so it takes a single permit instead.
    /// </summary>
    public async Task<IDisposable> AcquirePairAsync(Guid walletIdA, Guid walletIdB, CancellationToken ct)
    {
        if (walletIdA == walletIdB)
        {
            return await AcquireAsync(walletIdA, ct);
        }

        var (first, second) = walletIdA.CompareTo(walletIdB) <= 0
            ? (walletIdA, walletIdB)
            : (walletIdB, walletIdA);

        var semFirst = GetSemaphore(first);
        await semFirst.WaitAsync(ct);
        try
        {
            var semSecond = GetSemaphore(second);
            await semSecond.WaitAsync(ct);
            return new PairReleaser(semFirst, semSecond);
        }
        catch
        {
            semFirst.Release();
            throw;
        }
    }

    private sealed class Releaser(SemaphoreSlim sem) : IDisposable
    {
        public void Dispose() => sem.Release();
    }

    private sealed class PairReleaser(SemaphoreSlim first, SemaphoreSlim second) : IDisposable
    {
        public void Dispose()
        {
            second.Release();
            first.Release();
        }
    }
}
