using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbContext
{
    private static void ConfigureNpcEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NpcCompany>(e =>
        {
            e.HasKey(npc => npc.Id);
            e.Property(npc => npc.Name).HasMaxLength(200);
            e.Property(npc => npc.Archetype).HasMaxLength(30);
            e.HasIndex(npc => npc.CompanyId).IsUnique();
            e.HasIndex(npc => new { npc.HomeCityId, npc.IsActive });
            e.HasOne(npc => npc.Company).WithMany().HasForeignKey(npc => npc.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(npc => npc.HomeCity).WithMany().HasForeignKey(npc => npc.HomeCityId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NpcDecisionLog>(e =>
        {
            e.HasKey(log => log.Id);
            e.Property(log => log.ActionType).HasMaxLength(50);
            e.Property(log => log.Outcome).HasMaxLength(500);
            e.HasIndex(log => new { log.NpcCompanyId, log.Tick });
            e.HasOne(log => log.NpcCompany)
                .WithMany(npc => npc.DecisionLogs)
                .HasForeignKey(log => log.NpcCompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

