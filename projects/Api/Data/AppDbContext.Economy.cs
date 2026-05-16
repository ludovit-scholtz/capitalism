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

        modelBuilder.Entity<ContractBid>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.BidPricePerUnit).HasPrecision(18, 4);
            e.HasOne(b => b.Contract).WithMany(c => c.Bids).HasForeignKey(b => b.ContractId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(b => b.Company).WithMany().HasForeignKey(b => b.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(b => new { b.ContractId, b.CompanyId }).IsUnique();
            e.HasIndex(b => new { b.ContractId, b.BidPricePerUnit });
        });

        modelBuilder.Entity<ContractFulfillment>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.QuantityDelivered).HasPrecision(18, 4);
            e.Property(f => f.QuantityRequired).HasPrecision(18, 4);
            e.HasOne(f => f.Contract).WithOne(c => c.Fulfillment).HasForeignKey<ContractFulfillment>(f => f.ContractId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Company).WithMany().HasForeignKey(f => f.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(f => f.ContractId).IsUnique();
        });

        modelBuilder.Entity<SupplyContract>(e =>
        {
            e.HasKey(contract => contract.Id);
            e.Property(contract => contract.QuantityPerTick).HasPrecision(18, 4);
            e.Property(contract => contract.PricePerUnit).HasPrecision(18, 4);
            e.Property(contract => contract.PenaltyRatePercent).HasPrecision(6, 3);
            e.Property(contract => contract.TotalDeliveredQuantity).HasPrecision(18, 4);
            e.Property(contract => contract.TotalUndeliveredQuantity).HasPrecision(18, 4);
            e.Property(contract => contract.TotalPenaltyAmount).HasPrecision(18, 4);
            e.Property(contract => contract.CurrencyCode).HasMaxLength(8);
            e.Property(contract => contract.Status).HasMaxLength(20);
            e.HasOne(contract => contract.SellerCompany).WithMany().HasForeignKey(contract => contract.SellerCompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(contract => contract.BuyerCompany).WithMany().HasForeignKey(contract => contract.BuyerCompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(contract => contract.SellerBuildingUnit).WithMany().HasForeignKey(contract => contract.SellerBuildingUnitId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(contract => contract.ResourceType).WithMany().HasForeignKey(contract => contract.ResourceTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(contract => contract.ProductType).WithMany().HasForeignKey(contract => contract.ProductTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(contract => new { contract.Status, contract.StartTick });
            e.HasIndex(contract => new { contract.BuyerCompanyId, contract.Status });
            e.HasIndex(contract => new { contract.SellerCompanyId, contract.Status });
        });

        modelBuilder.Entity<CityUnlockRequirement>(e =>
        {
            e.HasKey(requirement => requirement.Id);
            e.Property(requirement => requirement.RequiredNetWorthUsd).HasPrecision(18, 2);
            e.HasOne(requirement => requirement.City)
                .WithMany()
                .HasForeignKey(requirement => requirement.CityId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(requirement => requirement.CityId).IsUnique();
        });

        modelBuilder.Entity<CompanyCityUnlock>(e =>
        {
            e.HasKey(unlock => unlock.Id);
            e.HasOne(unlock => unlock.Company)
                .WithMany()
                .HasForeignKey(unlock => unlock.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(unlock => unlock.City)
                .WithMany()
                .HasForeignKey(unlock => unlock.CityId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(unlock => new { unlock.CompanyId, unlock.CityId }).IsUnique();
        });

        modelBuilder.Entity<LoanOffer>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.AnnualInterestRatePercent).HasPrecision(8, 4);
            e.Property(o => o.MaxPrincipalPerLoan).HasPrecision(18, 2);
            e.Property(o => o.TotalCapacity).HasPrecision(18, 2);
            e.Property(o => o.UsedCapacity).HasPrecision(18, 2);
            e.HasOne(o => o.BankBuilding).WithMany().HasForeignKey(o => o.BankBuildingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(o => o.LenderCompany).WithMany().HasForeignKey(o => o.LenderCompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(o => new { o.LenderCompanyId, o.IsActive });
        });

        modelBuilder.Entity<Loan>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.OriginalPrincipal).HasPrecision(18, 2);
            e.Property(l => l.RemainingPrincipal).HasPrecision(18, 4);
            e.Property(l => l.AnnualInterestRatePercent).HasPrecision(8, 4);
            e.Property(l => l.PaymentAmount).HasPrecision(18, 4);
            e.Property(l => l.AccumulatedPenalty).HasPrecision(18, 4);
            e.Property(l => l.Status).HasMaxLength(20);
            e.Property(l => l.ConcurrencyToken).IsConcurrencyToken();
            e.HasOne(l => l.LoanOffer).WithMany(o => o.Loans).HasForeignKey(l => l.LoanOfferId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.BorrowerCompany).WithMany().HasForeignKey(l => l.BorrowerCompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.BorrowerBankAccount).WithMany().HasForeignKey(l => l.BorrowerBankAccountId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(l => l.BankBuilding).WithMany().HasForeignKey(l => l.BankBuildingId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.LenderCompany).WithMany().HasForeignKey(l => l.LenderCompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => new { l.BorrowerCompanyId, l.Status });
            e.HasIndex(l => new { l.LenderCompanyId, l.Status });
            e.HasIndex(l => l.BorrowerBankAccountId);
            e.HasIndex(l => l.NextPaymentTick);
            e.HasIndex(l => l.DueSoonAlertForPaymentTick);
        });

        modelBuilder.Entity<LoanCollateralSecurityAuditLog>(e =>
        {
            e.HasKey(log => log.Id);
            e.Property(log => log.Action).HasMaxLength(40);
            e.Property(log => log.RejectionReason).HasMaxLength(80);
            e.Property(log => log.Detail).HasMaxLength(500);
            e.HasIndex(log => new { log.LoanId, log.OccurredAtUtc });
            e.HasIndex(log => new { log.BuildingId, log.OccurredAtUtc });
            e.HasIndex(log => new { log.PlayerId, log.OccurredAtUtc });
        });

        modelBuilder.Entity<FxRate>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.BaseCurrencyCode).HasMaxLength(3);
            e.Property(r => r.QuoteCurrencyCode).HasMaxLength(3);
            e.Property(r => r.Rate).HasPrecision(18, 6);
            e.Property(r => r.Source).HasMaxLength(20);
            e.HasIndex(r => new { r.BaseCurrencyCode, r.QuoteCurrencyCode, r.RateDate });
        });

        modelBuilder.Entity<FxRateHistory>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.BaseCurrencyCode).HasMaxLength(3);
            e.Property(h => h.QuoteCurrencyCode).HasMaxLength(3);
            e.Property(h => h.MidRate).HasPrecision(18, 6);
            e.Property(h => h.BuyRate).HasPrecision(18, 6);
            e.Property(h => h.SellRate).HasPrecision(18, 6);
            e.HasIndex(h => new { h.BaseCurrencyCode, h.QuoteCurrencyCode, h.GameTick });
            e.HasIndex(h => h.GameTick);
        });

        modelBuilder.Entity<AdminActionAuditLog>(e =>
        {
            e.HasKey(log => log.Id);
            e.Property(log => log.AdminActorEmail).HasMaxLength(256);
            e.Property(log => log.AdminActorDisplayName).HasMaxLength(100);
            e.Property(log => log.EffectivePlayerEmail).HasMaxLength(256);
            e.Property(log => log.EffectivePlayerDisplayName).HasMaxLength(100);
            e.Property(log => log.EffectiveAccountType).HasMaxLength(20);
            e.Property(log => log.EffectiveCompanyName).HasMaxLength(200);
            e.Property(log => log.GraphQlOperationName).HasMaxLength(160);
            e.Property(log => log.MutationSummary).HasMaxLength(500);
            e.HasIndex(log => log.RecordedAtUtc);
            e.HasIndex(log => log.AdminActorPlayerId);
            e.HasIndex(log => log.EffectivePlayerId);
        });

        modelBuilder.Entity<ForexTradeRecord>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.FromCurrencyCode).HasMaxLength(3);
            e.Property(t => t.ToCurrencyCode).HasMaxLength(3);
            e.Property(t => t.FromAmount).HasPrecision(18, 4);
            e.Property(t => t.ToAmount).HasPrecision(18, 4);
            e.Property(t => t.FeeAmount).HasPrecision(18, 4);
            e.Property(t => t.Rate).HasPrecision(18, 6);
            e.HasOne(t => t.Player).WithMany().HasForeignKey(t => t.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => new { t.PlayerId, t.ExecutedAtTick });
        });

        modelBuilder.Entity<PlayerGoldBalance>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Balance).HasPrecision(18, 8);
            e.HasOne(g => g.Player).WithMany().HasForeignKey(g => g.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(g => g.PlayerId).IsUnique();
            e.ToTable("PlayerGoldBalances", t =>
                t.HasCheckConstraint("CK_PlayerGoldBalances_Balance_NonNegative", "\"Balance\" >= 0"));
        });

        modelBuilder.Entity<GoldAmmPool>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.CurrencyCode).HasMaxLength(3);
            e.Property(p => p.FiatReserve).HasPrecision(18, 4);
            e.Property(p => p.GoldReserve).HasPrecision(18, 8);
            e.Property(p => p.TotalLiquidityShares).HasPrecision(18, 8);
            e.HasIndex(p => p.CurrencyCode).IsUnique();
        });

        modelBuilder.Entity<GoldAmmPosition>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.LiquidityShares).HasPrecision(18, 8);
            e.Property(p => p.FiatProvided).HasPrecision(18, 4);
            e.Property(p => p.GoldProvided).HasPrecision(18, 8);
            e.HasOne(p => p.Pool).WithMany(pool => pool.Positions).HasForeignKey(p => p.PoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Player).WithMany().HasForeignKey(p => p.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(p => new { p.PoolId, p.PlayerId }).IsUnique();
        });

        modelBuilder.Entity<GoldAmmTradeRecord>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Direction).HasMaxLength(20);
            e.Property(t => t.CurrencyCode).HasMaxLength(3);
            e.Property(t => t.InputAmount).HasPrecision(18, 8);
            e.Property(t => t.OutputAmount).HasPrecision(18, 8);
            e.Property(t => t.FeeAmount).HasPrecision(18, 8);
            e.Property(t => t.ImpliedPrice).HasPrecision(18, 4);
            e.HasOne(t => t.Player).WithMany().HasForeignKey(t => t.PlayerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Pool).WithMany().HasForeignKey(t => t.PoolId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(t => new { t.PlayerId, t.ExecutedAtTick });
        });

        modelBuilder.Entity<CityMarketReport>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.ReportType).HasMaxLength(20);
            e.HasOne(r => r.City).WithMany().HasForeignKey(r => r.CityId).OnDelete(DeleteBehavior.Cascade);
            // Unique constraint: one report per city per type per tick window.
            e.HasIndex(r => new { r.CityId, r.ReportType, r.TickFrom }).IsUnique();
            e.HasIndex(r => r.GeneratedAtUtc);
        });

        modelBuilder.Entity<DemandSeasonality>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Q1Multiplier).HasPrecision(5, 3);
            e.Property(d => d.Q2Multiplier).HasPrecision(5, 3);
            e.Property(d => d.Q3Multiplier).HasPrecision(5, 3);
            e.Property(d => d.Q4Multiplier).HasPrecision(5, 3);
            e.HasOne(d => d.ProductType).WithMany().HasForeignKey(d => d.ProductTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => d.ProductTypeId).IsUnique();
        });

        modelBuilder.Entity<CityEconomicReport>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasOne(r => r.City).WithMany().HasForeignKey(r => r.CityId).OnDelete(DeleteBehavior.Cascade);
            e.Property(r => r.TotalSalaries).HasPrecision(18, 4);
            e.Property(r => r.TotalPublicRevenue).HasPrecision(18, 4);
            e.Property(r => r.TotalPowerConsumption).HasPrecision(18, 4);
            e.Property(r => r.TotalPowerSupply).HasPrecision(18, 4);
            e.Property(r => r.AverageProductQuality).HasPrecision(5, 4);
            e.Property(r => r.EconomicIndex).HasPrecision(5, 2);
            // Efficient historical lookups per city ordered by cycle.
            e.HasIndex(r => new { r.CityId, r.TaxCycleEnd });
        });

        modelBuilder.Entity<InterCityTradeRoute>(e =>
        {
            e.Property(r => r.Status).HasMaxLength(20);
            // Compound index for the TradeRoutePhase query that filters in-transit routes
            // whose arrival tick has been reached.  Without this index the phase performs a
            // full table scan on every game tick.
            e.HasIndex(r => new { r.Status, r.ExpectedArrivalTick });
        });

        modelBuilder.Entity<InventorySpoilageRecord>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.QuantitySpoiled).HasPrecision(18, 4);
            e.Property(r => r.QualityAtSpoilage).HasPrecision(5, 4);
            e.Property(r => r.EstimatedLossValue).HasPrecision(18, 2);
            e.HasOne(r => r.Company).WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Building).WithMany().HasForeignKey(r => r.BuildingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.BuildingUnit).WithMany().HasForeignKey(r => r.BuildingUnitId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(r => r.ProductType).WithMany().HasForeignKey(r => r.ProductTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.BuildingId, r.RecordedAtTick });
            e.HasIndex(r => new { r.CompanyId, r.RecordedAtTick });
        });

        modelBuilder.Entity<VictoryNewsletter>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.WinnerDisplayName).HasMaxLength(100);
            e.Property(v => v.WinnerCompanyName).HasMaxLength(200);
            e.Property(v => v.WinnerNetWorthUsd).HasPrecision(28, 2);
            e.Property(v => v.TotalFxVolumeUsd).HasPrecision(28, 2);
            e.Property(v => v.TotalProductsSold).HasPrecision(18, 2);
        });

        modelBuilder.Entity<GameState>(e =>
        {
            e.Property(g => g.WinnerNetWorth).HasPrecision(28, 2);
            e.Property(g => g.ShardState).HasDefaultValue(GameShardState.Active);
        });
    }
}
