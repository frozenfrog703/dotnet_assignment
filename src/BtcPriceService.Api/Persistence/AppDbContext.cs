using BtcPriceService.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace BtcPriceService.Api.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BtcPriceRecord> BtcPriceRecords => Set<BtcPriceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BtcPriceRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TimestampUtc).IsUnique();
            entity.Property(x => x.TimestampUtc).IsRequired();
            entity.Property(x => x.AggregatedPrice).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
        });
    }
}
