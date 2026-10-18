using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbContext
{
    public DbSet<PlayerCampaignClaim> PlayerCampaignClaims => Set<PlayerCampaignClaim>();

    private static void ConfigureCampaignEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlayerCampaignClaim>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.GoalCode).HasMaxLength(60);
            e.Property(c => c.CurrencyCode).HasMaxLength(3);
            e.Property(c => c.Amount).HasPrecision(18, 2);
            e.HasOne(c => c.Player)
                .WithMany()
                .HasForeignKey(c => c.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.PlayerId, c.GoalCode }).IsUnique();
        });
    }
}
