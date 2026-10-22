using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbContext
{
    public DbSet<TickTimingRecord> TickTimingRecords => Set<TickTimingRecord>();

    private static void ConfigureTickTimingEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TickTimingRecord>(e =>
        {
            e.HasKey(r => r.Tick);
            e.Property(r => r.Tick).ValueGeneratedNever();
        });
    }
}
