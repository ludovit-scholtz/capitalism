using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbContext
{
    private static void ConfigureBuildingEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Building>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.Type).HasMaxLength(30);
            e.Property(b => b.Name).HasMaxLength(200);
            e.Property(b => b.PowerConsumption).HasPrecision(18, 2);
            e.Property(b => b.AskingPrice).HasPrecision(18, 2);
            e.Property(b => b.PricePerSqm).HasPrecision(18, 2);
            e.Property(b => b.OccupancyPercent).HasPrecision(5, 2);
            e.Property(b => b.TotalAreaSqm).HasPrecision(18, 2);
            e.Property(b => b.PowerOutput).HasPrecision(18, 2);
            e.Property(b => b.PowerStatus).HasMaxLength(20);
            e.Property(b => b.PowerPriority).HasDefaultValue(5);
            e.Property(b => b.InterestRate).HasPrecision(5, 2);
            e.Property(b => b.DepositInterestRatePercent).HasPrecision(8, 4);
            e.Property(b => b.LendingInterestRatePercent).HasPrecision(8, 4);
            e.Property(b => b.PendingDepositInterestRatePercent).HasPrecision(8, 4);
            e.Property(b => b.TotalDeposits).HasPrecision(18, 2);
            e.Property(b => b.ConstructionCost).HasPrecision(18, 2);
            e.Property(b => b.SuspendedReason).HasMaxLength(200);
            e.Property(b => b.ConcurrencyToken).IsConcurrencyToken();
            e.HasOne(b => b.Company).WithMany(c => c.Buildings).HasForeignKey(b => b.CompanyId);
            e.HasOne(b => b.City).WithMany(c => c.Buildings).HasForeignKey(b => b.CityId);
            e.HasMany(b => b.MediaHouseUnits)
                .WithOne(unit => unit.Building)
                .HasForeignKey(unit => unit.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.BankAccount).WithMany().HasForeignKey(b => b.BankAccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(b => b.PendingConfiguration)
                .WithOne(plan => plan.Building)
                .HasForeignKey<BuildingConfigurationPlan>(plan => plan.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BankDepositRateHistory>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.PreviousRatePercent).HasPrecision(8, 4);
            e.Property(h => h.NewRatePercent).HasPrecision(8, 4);
            e.HasIndex(h => h.BankBuildingId);
            e.HasIndex(h => new { h.BankBuildingId, h.IsApplied });
            e.HasIndex(h => h.EffectiveTick);
            e.HasOne(h => h.BankBuilding).WithMany().HasForeignKey(h => h.BankBuildingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(h => h.ChangedByPlayer).WithMany().HasForeignKey(h => h.ChangedByPlayerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BankAccount>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.AccountNumber).HasMaxLength(16);
            e.Property(a => a.CurrencyCode).HasMaxLength(3);
            e.Property(a => a.Balance).HasPrecision(18, 2);
            e.Property(a => a.AlertMinBalanceThreshold).HasPrecision(18, 2);
            e.Property(a => a.DepositInterestRatePercent).HasPrecision(8, 4);
            e.Property(a => a.TotalInterestPaid).HasPrecision(18, 4);
            e.HasIndex(a => a.AccountNumber).IsUnique();
            e.HasIndex(a => new { a.CurrencyCode, a.IsGovernmentAccount });
            e.HasIndex(a => new { a.PlayerId, a.CurrencyCode }).IsUnique();
            e.HasIndex(a => new { a.BankBuildingId, a.ClosedAtUtc });
            e.HasIndex(a => new { a.CompanyId, a.BankBuildingId, a.ClosedAtUtc });
            e.HasOne(a => a.Company).WithMany(c => c.BankAccounts).HasForeignKey(a => a.CompanyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.Player).WithMany().HasForeignKey(a => a.PlayerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(a => a.BankBuilding).WithMany().HasForeignKey(a => a.BankBuildingId).OnDelete(DeleteBehavior.Cascade);
            e.Property(a => a.ConcurrencyToken).IsConcurrencyToken();
        });

        modelBuilder.Entity<BuildingUnit>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.UnitType).HasMaxLength(30);
            e.Property(u => u.PurchaseSource).HasMaxLength(20);
            e.Property(u => u.SaleVisibility).HasMaxLength(20);
            e.Property(u => u.BrandScope).HasMaxLength(20);
            e.Property(u => u.MinPrice).HasPrecision(18, 2);
            e.Property(u => u.MaxPrice).HasPrecision(18, 2);
            e.Property(u => u.Budget).HasPrecision(18, 2);
            e.Property(u => u.LowInventoryAlertThreshold).HasPrecision(18, 4);
            e.Property(u => u.MinQuality).HasPrecision(5, 4);
            e.HasOne(u => u.Building).WithMany(b => b.Units).HasForeignKey(u => u.BuildingId);
            e.HasIndex(u => u.BuildingId);
        });

        modelBuilder.Entity<MediaHouseUnit>(e =>
        {
            e.HasKey(unit => unit.Id);
            e.Property(unit => unit.MediaType).HasMaxLength(20);
            e.Property(unit => unit.CampaignBudgetPerTick).HasPrecision(18, 2);
            e.Property(unit => unit.BrandQualityBoostPerTick).HasPrecision(18, 6);
            e.Property(unit => unit.LaborCostPerTick).HasPrecision(18, 2);
            e.Property(unit => unit.EnergyCostPerTick).HasPrecision(18, 2);
            e.HasIndex(unit => unit.BuildingId);
            e.HasIndex(unit => unit.TargetCompanyId);
            e.HasOne(unit => unit.Building)
                .WithMany(building => building.MediaHouseUnits)
                .HasForeignKey(unit => unit.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(unit => unit.TargetCompany)
                .WithMany(company => company.TargetedMediaHouseUnits)
                .HasForeignKey(unit => unit.TargetCompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BrandQualityRecord>(e =>
        {
            e.HasKey(record => record.Id);
            e.Property(record => record.BoostApplied).HasPrecision(18, 6);
            e.Property(record => record.CampaignBudgetSpent).HasPrecision(18, 2);
            e.Property(record => record.LaborCostSpent).HasPrecision(18, 2);
            e.Property(record => record.EnergyCostSpent).HasPrecision(18, 2);
            e.HasIndex(record => record.BuildingId);
            e.HasIndex(record => new { record.BuildingId, record.RecordedAtTick });
            e.HasIndex(record => record.MediaHouseUnitId);
            e.HasIndex(record => record.TargetCompanyId);
            e.HasOne(record => record.Building)
                .WithMany()
                .HasForeignKey(record => record.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(record => record.MediaHouseUnit)
                .WithMany()
                .HasForeignKey(record => record.MediaHouseUnitId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(record => record.TargetCompany)
                .WithMany()
                .HasForeignKey(record => record.TargetCompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BuildingConfigurationPlan>(e =>
        {
            e.HasKey(plan => plan.Id);
            e.HasOne(plan => plan.Building)
                .WithOne(building => building.PendingConfiguration)
                .HasForeignKey<BuildingConfigurationPlan>(plan => plan.BuildingId);
        });

        modelBuilder.Entity<BuildingConfigurationPlanUnit>(e =>
        {
            e.HasKey(unit => unit.Id);
            e.Property(unit => unit.UnitType).HasMaxLength(30);
            e.Property(unit => unit.PurchaseSource).HasMaxLength(20);
            e.Property(unit => unit.SaleVisibility).HasMaxLength(20);
            e.Property(unit => unit.BrandScope).HasMaxLength(20);
            e.Property(unit => unit.MinPrice).HasPrecision(18, 2);
            e.Property(unit => unit.MaxPrice).HasPrecision(18, 2);
            e.Property(unit => unit.Budget).HasPrecision(18, 2);
            e.Property(unit => unit.MinQuality).HasPrecision(5, 4);
            e.Property(unit => unit.LowInventoryAlertThreshold).HasPrecision(18, 4);
            e.HasOne(unit => unit.BuildingConfigurationPlan)
                .WithMany(plan => plan.Units)
                .HasForeignKey(unit => unit.BuildingConfigurationPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BuildingConfigurationPlanRemoval>(e =>
        {
            e.HasKey(removal => removal.Id);
            e.HasOne(removal => removal.BuildingConfigurationPlan)
                .WithMany(plan => plan.Removals)
                .HasForeignKey(removal => removal.BuildingConfigurationPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<City>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.CountryCode).HasMaxLength(2);
            e.Property(c => c.AverageRentPerSqm).HasPrecision(18, 2);
            e.Property(c => c.BaseSalaryPerManhour).HasPrecision(18, 4);
        });

        modelBuilder.Entity<BuildingLot>(e =>
        {
            e.HasKey(lot => lot.Id);
            e.Property(lot => lot.Name).HasMaxLength(200);
            e.Property(lot => lot.Description).HasMaxLength(500);
            e.Property(lot => lot.District).HasMaxLength(100);
            e.Property(lot => lot.PopulationIndex).HasPrecision(9, 4);
            e.Property(lot => lot.BasePrice).HasPrecision(18, 2);
            e.Property(lot => lot.Price).HasPrecision(18, 2);
            e.Property(lot => lot.SuitableTypes).HasMaxLength(200);
            e.Property(lot => lot.MaterialQuality).HasPrecision(5, 4);
            e.Property(lot => lot.MaterialQuantity).HasPrecision(18, 2);
            e.Property(lot => lot.OriginalMaterialQuantity).HasPrecision(18, 2);
            e.Property(lot => lot.ConcurrencyToken).IsConcurrencyToken();
            e.HasOne(lot => lot.City).WithMany(c => c.Lots).HasForeignKey(lot => lot.CityId);
            e.HasOne(lot => lot.OwnerCompany).WithMany().HasForeignKey(lot => lot.OwnerCompanyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(lot => lot.Building).WithMany().HasForeignKey(lot => lot.BuildingId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(lot => lot.ResourceType).WithMany().HasForeignKey(lot => lot.ResourceTypeId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MineDepletionRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.ResourceTypeName).HasMaxLength(100);
            e.Property(r => r.OriginalQuantity).HasPrecision(18, 2);
            e.HasIndex(r => r.LotId);
            e.HasIndex(r => r.CompanyId);
            e.HasIndex(r => r.DepletedAtTick);
        });

        modelBuilder.Entity<MineExtractionRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.ExtractedAmount).HasPrecision(18, 4);
            e.Property(r => r.EfficiencyPercent).HasPrecision(5, 4);
            e.Property(r => r.ReserveRemaining).HasPrecision(18, 2);
            e.HasIndex(r => new { r.BuildingId, r.Tick }).IsDescending(false, true);
        });

        modelBuilder.Entity<ResourceReplenishmentSchedule>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasOne(s => s.City).WithMany().HasForeignKey(s => s.CityId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => s.CityId).IsUnique();
            e.HasIndex(s => s.NextReplenishmentTick);
        });

        modelBuilder.Entity<CityResource>(e =>
        {
            e.HasKey(cr => cr.Id);
            e.Property(cr => cr.Abundance).HasPrecision(5, 4);
            e.HasOne(cr => cr.City).WithMany(c => c.Resources).HasForeignKey(cr => cr.CityId);
            e.HasOne(cr => cr.ResourceType).WithMany().HasForeignKey(cr => cr.ResourceTypeId);
        });

        modelBuilder.Entity<BuildingSaleOffer>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.OfferedPrice).HasPrecision(18, 2);
            e.Property(o => o.Status).HasMaxLength(20);
            e.Property(o => o.OfferVersion).IsConcurrencyToken();
            e.Property(o => o.NegotiationNote).HasMaxLength(500);
            e.HasIndex(o => o.BuildingId);
            e.HasIndex(o => o.BuyerPlayerId);
            e.HasIndex(o => new { o.BuildingId, o.Status });
            e.HasOne(o => o.Building).WithMany().HasForeignKey(o => o.BuildingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.BuyerPlayer).WithMany().HasForeignKey(o => o.BuyerPlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.BuyerCompany).WithMany().HasForeignKey(o => o.BuyerCompanyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BuildingOfferSecurityAuditLog>(e =>
        {
            e.HasKey(log => log.Id);
            e.Property(log => log.Action).HasMaxLength(40);
            e.HasIndex(log => new { log.OfferId, log.OccurredAtUtc });
            e.HasIndex(log => new { log.BuyerPlayerId, log.OccurredAtUtc });
        });
    }
}
