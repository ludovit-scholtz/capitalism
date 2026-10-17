using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbContext
{
    private static void ConfigureEconomyEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResourceType>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.Slug).IsUnique();
            e.Property(r => r.Name).HasMaxLength(100);
            e.Property(r => r.Slug).HasMaxLength(100);
            e.Property(r => r.Category).HasMaxLength(30);
            e.Property(r => r.BasePrice).HasPrecision(18, 2);
            e.Property(r => r.WeightPerUnit).HasPrecision(18, 4);
            e.Property(r => r.UnitName).HasMaxLength(50);
            e.Property(r => r.UnitSymbol).HasMaxLength(20);
            e.Property(r => r.ImageUrl).HasMaxLength(12000);
        });

        modelBuilder.Entity<ProductType>(e =>
        {
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Slug).HasMaxLength(200);
            e.Property(p => p.Industry).HasMaxLength(50);
            e.Property(p => p.BasePrice).HasPrecision(18, 2);
            e.Property(p => p.PriceElasticity).HasPrecision(5, 4);
            e.Property(p => p.OutputQuantity).HasPrecision(18, 4);
            e.Property(p => p.EnergyConsumptionMwh).HasPrecision(18, 4);
            e.Property(p => p.BasicLaborHours).HasPrecision(18, 4);
            e.Property(p => p.UnitName).HasMaxLength(50);
            e.Property(p => p.UnitSymbol).HasMaxLength(20);
        });

        modelBuilder.Entity<ProductRecipe>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Quantity).HasPrecision(18, 4);
            e.HasOne(r => r.ProductType).WithMany(p => p.Recipes).HasForeignKey(r => r.ProductTypeId);
            e.HasOne(r => r.ResourceType).WithMany().HasForeignKey(r => r.ResourceTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.InputProductType).WithMany().HasForeignKey(r => r.InputProductTypeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Inventory>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Quantity).HasPrecision(18, 4);
            e.Property(i => i.SourcingCostTotal).HasPrecision(18, 4);
            e.Property(i => i.Quality).HasPrecision(5, 4);
            e.HasOne(i => i.Building).WithMany().HasForeignKey(i => i.BuildingId);
            e.HasOne(i => i.BuildingUnit).WithMany().HasForeignKey(i => i.BuildingUnitId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(i => i.ResourceType).WithMany().HasForeignKey(i => i.ResourceTypeId);
            e.HasOne(i => i.ProductType).WithMany().HasForeignKey(i => i.ProductTypeId);
            e.HasIndex(i => i.BuildingId);
        });

        modelBuilder.Entity<BuildingUnitResourceHistory>(e =>
        {
            e.HasKey(history => history.Id);
            e.Property(history => history.InflowQuantity).HasPrecision(18, 4);
            e.Property(history => history.OutflowQuantity).HasPrecision(18, 4);
            e.Property(history => history.ConsumedQuantity).HasPrecision(18, 4);
            e.Property(history => history.ProducedQuantity).HasPrecision(18, 4);
            e.HasOne(history => history.Building)
                .WithMany()
                .HasForeignKey(history => history.BuildingId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(history => history.BuildingUnit)
                .WithMany()
                .HasForeignKey(history => history.BuildingUnitId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(history => history.ResourceType)
                .WithMany()
                .HasForeignKey(history => history.ResourceTypeId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasOne(history => history.ProductType)
                .WithMany()
                .HasForeignKey(history => history.ProductTypeId)
                .OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(history => new { history.BuildingId, history.Tick });
            e.HasIndex(history => new { history.BuildingUnitId, history.Tick, history.ResourceTypeId, history.ProductTypeId })
                .IsUnique();
        });

        modelBuilder.Entity<Brand>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.Name).HasMaxLength(200);
            e.Property(b => b.Scope).HasMaxLength(20);
            e.Property(b => b.IndustryCategory).HasMaxLength(50);
            e.Property(b => b.Awareness).HasPrecision(5, 4);
            e.Property(b => b.Quality).HasPrecision(5, 4);
            e.Property(b => b.MarketingQuality).HasPrecision(5, 4);
            e.Property(b => b.MarketingEfficiencyMultiplier).HasPrecision(7, 4).HasDefaultValue(1m);
            e.HasOne(b => b.Company).WithMany().HasForeignKey(b => b.CompanyId);
        });

        modelBuilder.Entity<GameState>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.TaxRate).HasPrecision(5, 2);
            e.Property(g => g.WinnerDisplayName).HasMaxLength(100);
            e.Property(g => g.WinnerCompanyName).HasMaxLength(200);
        });

        modelBuilder.Entity<RealWorldBillionaire>(e =>
        {
            e.HasKey(item => item.Id);
            e.Property(item => item.Name).HasMaxLength(120);
            e.Property(item => item.WealthUsd).HasPrecision(18, 2);
            e.HasIndex(item => item.Rank).IsUnique();
            e.ToTable("RealWorldBillionaires", table =>
                table.HasCheckConstraint("CK_RealWorldBillionaires_Rank_Range", "\"Rank\" BETWEEN 1 AND 10"));
        });

        modelBuilder.Entity<ExchangeOrder>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Side).HasMaxLength(10);
            e.Property(o => o.PricePerUnit).HasPrecision(18, 2);
            e.Property(o => o.Quantity).HasPrecision(18, 4);
            e.Property(o => o.RemainingQuantity).HasPrecision(18, 4);
            e.Property(o => o.MinQuality).HasPrecision(5, 4);
            e.HasOne(o => o.ExchangeBuilding).WithMany().HasForeignKey(o => o.ExchangeBuildingId);
            e.HasOne(o => o.Company).WithMany().HasForeignKey(o => o.CompanyId);
            // Partial-style index: tick context loads only active orders; without this index
            // the query does a full table scan across the entire historical order book.
            e.HasIndex(o => o.IsActive);
        });

        modelBuilder.Entity<LimitOrder>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.StockSymbol).HasMaxLength(40);
            e.Property(o => o.Side).HasMaxLength(10);
            e.Property(o => o.Status).HasMaxLength(20);
            e.Property(o => o.LimitPrice).HasPrecision(18, 4);
            e.Property(o => o.ReservedCashRemaining).HasPrecision(18, 4);
            e.HasOne(o => o.Company).WithMany().HasForeignKey(o => o.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.OwnerPlayer).WithMany().HasForeignKey(o => o.OwnerPlayerId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.OwnerCompany).WithMany().HasForeignKey(o => o.OwnerCompanyId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(o => o.SettlementBankAccount).WithMany().HasForeignKey(o => o.SettlementBankAccountId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(o => new { o.CompanyId, o.Status, o.Side, o.LimitPrice, o.CreatedAtTick });
            e.HasIndex(o => new { o.OwnerPlayerId, o.Status });
            e.HasIndex(o => new { o.OwnerCompanyId, o.Status });
        });

        modelBuilder.Entity<LimitOrderExecution>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.StockSymbol).HasMaxLength(40);
            e.Property(x => x.Price).HasPrecision(18, 4);
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.BuyOrder).WithMany().HasForeignKey(x => x.BuyOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.SellOrder).WithMany().HasForeignKey(x => x.SellOrderId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CompanyId, x.ExecutedAtTick });
            e.HasIndex(x => new { x.StockSymbol, x.ExecutedAtTick });
        });

        modelBuilder.Entity<LedgerEntry>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Category).HasMaxLength(40);
            e.Property(l => l.Description).HasMaxLength(500);
            e.Property(l => l.Amount).HasPrecision(18, 4);
            e.HasOne(l => l.Company).WithMany().HasForeignKey(l => l.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.Building).WithMany().HasForeignKey(l => l.BuildingId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(l => l.BankAccount).WithMany().HasForeignKey(l => l.BankAccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(l => l.BuildingUnit).WithMany().HasForeignKey(l => l.BuildingUnitId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(l => l.ProductType).WithMany().HasForeignKey(l => l.ProductTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(l => l.ResourceType).WithMany().HasForeignKey(l => l.ResourceTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(l => new { l.CompanyId, l.RecordedAtTick });
            e.HasIndex(l => new { l.CompanyId, l.BankAccountId, l.RecordedAtTick });
            // Compound index for category-filtered drill-down queries
            e.HasIndex(l => new { l.CompanyId, l.Category, l.RecordedAtTick });
            // Index for the tick-engine salary-window query which filters by Category without CompanyId.
            // Without this, every tick does a full table scan on LedgerEntries to compute recent wages.
            e.HasIndex(l => new { l.Category, l.RecordedAtTick });
        });

        modelBuilder.Entity<PublicSalesRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.QuantitySold).HasPrecision(18, 4);
            e.Property(r => r.PricePerUnit).HasPrecision(18, 4);
            e.Property(r => r.Revenue).HasPrecision(18, 4);
            e.Property(r => r.Demand).HasPrecision(18, 4);
            e.Property(r => r.SalesCapacity).HasPrecision(18, 4);
            e.Property(r => r.TrendFactor).HasPrecision(8, 4);
            e.HasOne(r => r.BuildingUnit).WithMany().HasForeignKey(r => r.BuildingUnitId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Building).WithMany().HasForeignKey(r => r.BuildingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Company).WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.City).WithMany().HasForeignKey(r => r.CityId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.ProductType).WithMany().HasForeignKey(r => r.ProductTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.ResourceType).WithMany().HasForeignKey(r => r.ResourceTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(r => new { r.BuildingUnitId, r.Tick });
            e.HasIndex(r => new { r.CompanyId, r.Tick });
        });

        modelBuilder.Entity<MarketTrendState>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.TrendFactor).HasPrecision(8, 4);
            e.HasIndex(t => new { t.CityId, t.ItemId }).IsUnique();
        });

        modelBuilder.Entity<EconomicCycle>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Phase).HasMaxLength(24);
            e.Property(c => c.IntensityFactor).HasPrecision(8, 4);
            e.HasIndex(c => c.PhaseStartedTick);
        });

        modelBuilder.Entity<MarketEvent>(e =>
        {
            e.HasKey(me => me.Id);
            e.Property(me => me.EventType).HasMaxLength(48);
            e.Property(me => me.Title).HasMaxLength(160);
            e.Property(me => me.Description).HasMaxLength(1000);
            e.Property(me => me.MagnitudeMultiplier).HasPrecision(8, 4);
            e.HasOne(me => me.AffectedResourceType).WithMany().HasForeignKey(me => me.AffectedResourceTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(me => me.AffectedCity).WithMany().HasForeignKey(me => me.AffectedCityId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(me => new { me.EventType, me.StartsAtTick, me.ExpiresAtTick });
            e.HasIndex(me => new { me.AffectedCityId, me.ExpiresAtTick });
            e.HasIndex(me => new { me.AffectedResourceTypeId, me.ExpiresAtTick });
        });

        modelBuilder.Entity<GlobalEvent>(e =>
        {
            e.HasKey(ge => ge.Id);
            e.Property(ge => ge.EventType).HasMaxLength(48);
            e.Property(ge => ge.Severity).HasMaxLength(20);
            e.Property(ge => ge.Title).HasMaxLength(200);
            e.Property(ge => ge.Description).HasMaxLength(2000);
            e.Property(ge => ge.TriggeredByAdminId).HasMaxLength(64);
            e.Property(ge => ge.OperatingCostMultiplier).HasPrecision(8, 4);
            e.Property(ge => ge.TradeRouteMultiplier).HasPrecision(8, 4);
            e.Property(ge => ge.RdMultiplier).HasPrecision(8, 4);
            e.Property(ge => ge.MineEfficiencyMultiplier).HasPrecision(8, 4);
            e.HasOne(ge => ge.AffectedCity).WithMany().HasForeignKey(ge => ge.AffectedCityId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(ge => new { ge.IsActive, ge.StartTick });
            e.HasIndex(ge => new { ge.EventType, ge.StartTick });
        });

        modelBuilder.Entity<GovernmentContract>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(200);
            e.Property(c => c.Description).HasMaxLength(2000);
            e.Property(c => c.QuantityRequired).HasPrecision(18, 4);
            e.Property(c => c.MinimumQuality).HasPrecision(5, 2);
            e.Property(c => c.BudgetCap).HasPrecision(18, 4);
            e.Property(c => c.Status).HasMaxLength(20);
            e.HasOne(c => c.City).WithMany().HasForeignKey(c => c.CityId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.ProductType).WithMany().HasForeignKey(c => c.ProductTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.WinnerCompany).WithMany().HasForeignKey(c => c.WinnerCompanyId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(c => new { c.CityId, c.Status, c.DeadlineTick });
            e.HasIndex(c => new { c.WinnerCompanyId, c.Status });
        });

        ConfigureEconomyEntitiesContractsAndUnlocks(modelBuilder);
    }
}
