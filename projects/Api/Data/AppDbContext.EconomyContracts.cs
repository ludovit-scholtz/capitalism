using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbContext
{
    private static void ConfigureEconomyEntitiesContractsAndUnlocks(ModelBuilder modelBuilder)
    {
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
            // The status doubles as the concurrency token: two requests that both read Active/Pending cannot both write a transition.
            e.Property(contract => contract.Status).HasMaxLength(20).IsConcurrencyToken();
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
