using Microsoft.EntityFrameworkCore;
using NovaPay.Api.Domain;

namespace NovaPay.Api.Data;

public class NovaPayDbContext(DbContextOptions<NovaPayDbContext> options) : DbContext(options)
{
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Transfer> Transfers => Set<Transfer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.OwnerName).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Transfer>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.IdempotencyKey).IsRequired().HasMaxLength(200);
            entity.Property(t => t.RequestFingerprint).IsRequired().HasMaxLength(64);

            // One completed transfer per idempotency key, enforced at the database level as the
            // authoritative guard against double-processing a retried request.
            entity.HasIndex(t => t.IdempotencyKey).IsUnique();

            // Speeds up the daily-outbound-limit aggregation query (sum by wallet + day).
            entity.HasIndex(t => new { t.FromWalletId, t.CreatedAtUtc });
        });
    }
}
