using Api.Data;
using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine;

/// <summary>
/// Holds all pre-loaded game data for a single tick, indexed for O(1) lookups.
/// Created by <see cref="TickProcessor"/> at the start of each tick.
/// </summary>
public sealed partial class TickContext
{
    private readonly Dictionary<(Guid BuildingUnitId, Guid? ResourceTypeId, Guid? ProductTypeId, long Tick), BuildingUnitResourceHistory> _unitResourceHistoryByKey = [];

    private static decimal RoundInventoryDecimal(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    public required AppDbContext Db { get; init; }
    public required GameState GameState { get; init; }
    public long CurrentTick => GameState.CurrentTick;

    // ── Indexed game data ──

    public Dictionary<Guid, Building> BuildingsById { get; init; } = [];
    public Dictionary<string, List<Building>> BuildingsByType { get; init; } = [];
    public Dictionary<Guid, List<BuildingUnit>> UnitsByBuilding { get; init; } = [];
    public Dictionary<Guid, List<MediaHouseUnit>> MediaHouseUnitsByBuilding { get; init; } = [];
    public Dictionary<Guid, Dictionary<(int GridX, int GridY), BuildingUnit>> UnitsByBuildingPosition { get; init; } = [];
    public Dictionary<Guid, List<Inventory>> InventoryByUnit { get; init; } = [];
    public Dictionary<Guid, List<Inventory>> InventoryByBuilding { get; init; } = [];
    public Dictionary<Guid, Company> CompaniesById { get; init; } = [];
    public Dictionary<Guid, List<CompanyCitySalarySetting>> CitySalarySettingsByCompany { get; init; } = [];
    public Dictionary<Guid, City> CitiesById { get; init; } = [];
    public Dictionary<Guid, List<BuildingLot>> LotsByCompany { get; init; } = [];
    public Dictionary<Guid, BuildingLot> LotsByBuildingId { get; init; } = [];
    public Dictionary<Guid, List<CityResource>> ResourcesByCity { get; init; } = [];
    public Dictionary<Guid, ResourceType> ResourceTypesById { get; init; } = [];
    public Dictionary<Guid, ProductType> ProductTypesById { get; init; } = [];
    public Dictionary<Guid, List<ProductRecipe>> RecipesByProduct { get; init; } = [];
    public Dictionary<Guid, List<Brand>> BrandsByCompany { get; init; } = [];
    public List<ExchangeOrder> ActiveExchangeOrders { get; init; } = [];
    public Dictionary<Guid, decimal> TickStartRemainingQuantityByInventoryId { get; init; } = [];

    /// <summary>
    /// Total absolute LaborCost ledger amounts paid in each city over the past
    /// <see cref="GameConstants.RecentSalaryWindowTicks"/> ticks.
    /// Used by <see cref="Phases.PublicSalesPhase"/> to compute the dynamic
    /// salary purchasing-power factor (ROADMAP: "game currency collected by
    /// salaries in past 10 ticks").  Keyed by CityId.
    /// </summary>
    public Dictionary<Guid, decimal> RecentSalaryByCity { get; init; } = [];

    /// <summary>
    /// Persisted market-trend states keyed by <c>(CityId, ItemId)</c>.
    /// Pre-loaded by <see cref="TickProcessor"/> and mutated in-place by
    /// <see cref="Phases.PublicSalesPhase"/> so changes are saved in the same
    /// <c>SaveChangesAsync</c> call at the end of the tick.
    /// </summary>
    public Dictionary<(Guid CityId, Guid ItemId), MarketTrendState> TrendStatesByKey { get; init; } = [];

    /// <summary>
    /// EUR-based FX rates for the currencies used across all cities in the game.
    /// Key = ISO 4217 currency code, Value = units of that currency per 1 EUR.
    /// EUR itself maps to 1.0. Pre-loaded by <see cref="TickProcessor"/> from
    /// <see cref="Data.AppDbContext.FxRates"/> with <see cref="Utilities.FxRateHelper"/> fallbacks.
    /// Used by purchasing and sales phases to convert EUR-denominated base prices
    /// into the correct local city currency.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> EurFxRates { get; init; } = new Dictionary<string, decimal>();

    /// <summary>
    /// Returns the EUR→local-currency FX rate for a city.
    /// Uses the pre-loaded <see cref="EurFxRates"/> table with fallback to hardcoded rates.
    /// </summary>
    public decimal GetCityFxRate(City city)
        => Utilities.FxRateHelper.GetEurRate(EurFxRates, city.CurrencyCode);

    /// <summary>
    /// Set of <see cref="BuildingUnit"/> IDs that currently have a pending upgrade in progress
    /// (i.e., a <see cref="BuildingConfigurationPlanUnit"/> with <c>IsChanged = true</c>,
    /// <c>TicksRequired &gt; 0</c>, and <c>AppliesAtTick &gt; CurrentTick</c>).
    /// While a unit is under upgrade it must not receive items, push items, manufacture goods,
    /// mine resources, or execute sales during any tick in the upgrade window.
    /// </summary>
    public HashSet<Guid> UnitsUnderUpgrade { get; init; } = [];

    /// <summary>
    /// Accumulated research budgets keyed by (CompanyId, ProductTypeId).
    /// Pre-loaded by <see cref="TickProcessor"/> from <see cref="Data.AppDbContext.ProductResearchBudgets"/>
    /// and mutated in-place by <see cref="Phases.ResearchPhase"/>.
    /// </summary>
    public Dictionary<(Guid CompanyId, Guid ProductTypeId), ProductResearchBudget> ResearchBudgetsByKey { get; init; } = [];

    /// <summary>
    /// Current-tick weather snapshot for each city.
    /// Pre-loaded by <see cref="TickProcessor"/> from <see cref="Data.AppDbContext.CityWeatherForecasts"/>
    /// and refreshed in-place by <see cref="Phases.WeatherUpdatePhase"/> each tick.
    /// Used by <see cref="Phases.PowerDistributionPhase"/> to scale SOLAR and WIND output.
    /// </summary>
    public Dictionary<Guid, WeatherSnapshot> WeatherByCity { get; init; } = [];

    /// <summary>
    /// Bank accounts keyed by their ID.
    /// Pre-loaded by <see cref="TickProcessor"/> to allow the <see cref="Phases.OperatingCostPhase"/>
    /// to check and debit building bank accounts without extra DB queries per building.
    /// </summary>
    public Dictionary<Guid, BankAccount> BankAccountsById { get; init; } = [];

    /// <summary>
    /// Per-product seasonal demand multipliers keyed by <see cref="ProductType"/> ID.
    /// Pre-loaded by <see cref="TickProcessor"/> from <see cref="Data.AppDbContext.DemandSeasonalities"/>.
    /// Used by <see cref="Phases.PublicSalesPhase"/> to apply the seasonal multiplier
    /// for the current game-year quarter.
    /// Products without a row default to 1.0× (neutral seasonality).
    /// </summary>
    public Dictionary<Guid, DemandSeasonality> SeasonalityByProductTypeId { get; init; } = [];

    /// <summary>Current global cycle demand intensity multiplier.</summary>
    public decimal EconomicCycleIntensity { get; init; } = 1.0m;

    /// <summary>Active market events for the current tick.</summary>
    public List<MarketEvent> ActiveMarketEvents { get; init; } = [];

    /// <summary>Resource-level commodity shock multipliers.</summary>
    public Dictionary<Guid, decimal> CommodityShockMultiplierByResourceId { get; init; } = [];

    /// <summary>City-scoped interest-rate multipliers.</summary>
    public Dictionary<Guid, decimal> InterestRateMultiplierByCityId { get; init; } = [];
    public decimal GlobalInterestRateMultiplier { get; init; } = 1.0m;

    /// <summary>City-scoped seasonal-demand event multipliers.</summary>
    public Dictionary<Guid, decimal> SeasonalDemandMultiplierByCityId { get; init; } = [];
    public decimal GlobalSeasonalDemandMultiplier { get; init; } = 1.0m;

    // ── Global economic shock events ──────────────────────────────────────────────────────────────

    /// <summary>All currently active global shock events loaded at tick start.</summary>
    public List<GlobalEvent> ActiveGlobalEvents { get; init; } = [];

    /// <summary>
    /// Aggregate multiplier applied to building operating costs (labor + energy) from active global events.
    /// Multiplicatively stacked from all active events' <see cref="GlobalEvent.OperatingCostMultiplier"/>.
    /// </summary>
    public decimal GlobalEventOperatingCostMultiplier { get; init; } = 1.0m;

    /// <summary>
    /// Aggregate multiplier applied to trade-route shipping costs from active global events.
    /// Multiplicatively stacked from all active events' <see cref="GlobalEvent.TradeRouteMultiplier"/>.
    /// </summary>
    public decimal GlobalEventTradeRouteMultiplier { get; init; } = 1.0m;

    /// <summary>
    /// Aggregate multiplier applied to R&amp;D budget accumulation from active global events.
    /// Multiplicatively stacked from all active events' <see cref="GlobalEvent.RdMultiplier"/>.
    /// </summary>
    public decimal GlobalEventRdMultiplier { get; init; } = 1.0m;

    /// <summary>
    /// Per-city aggregate mine-efficiency multipliers.
    /// Key: CityId. Value: multiplicatively-stacked <see cref="GlobalEvent.MineEfficiencyMultiplier"/>
    /// from all events affecting that specific city, combined with any global (city-agnostic) events.
    /// </summary>
    public Dictionary<Guid, decimal> GlobalEventMineEfficiencyByCityId { get; init; } = [];

    public List<Inventory> NewInventory { get; } = [];
    public List<BuildingUnitResourceHistory> NewUnitResourceHistories { get; } = [];

    /// <summary>
    /// Effective MW output for each power-plant building, computed and stored by
    /// <see cref="Phases.PowerDistributionPhase"/> so that <see cref="Phases.PowerGridEconomicsPhase"/>
    /// can use the same output values without recomputing (important because fuel reserves are consumed
    /// during the distribution phase and would appear as 0 if re-evaluated later).
    /// Keyed by <see cref="Building.Id"/>.
    /// </summary>
    public Dictionary<Guid, decimal> PlantEffectiveOutputMwById { get; } = [];

    // ── Helpers ──

    /// <summary>
    /// Returns the operational efficiency factor for a building based on its PowerStatus.
    /// <list type="bullet">
    /// <item>POWERED → 1.0 (full capacity)</item>
    /// <item>CONSTRAINED → <see cref="GameConstants.ConstrainedEfficiencyFactor"/> (partial capacity)</item>
    /// <item>OFFLINE → 0.0 (completely stopped)</item>
    /// </list>
    /// </summary>
    public static decimal GetPowerEfficiency(Building building) => building.PowerStatus switch
    {
        Data.Entities.PowerStatus.Constrained => GameConstants.ConstrainedEfficiencyFactor,
        Data.Entities.PowerStatus.Offline     => 0m,
        _                                     => 1m
    };

    public IEnumerable<BankAccount> GetCompanyBankAccounts(Guid companyId)
        => BankAccountsById.Values.Where(account => account.CompanyId == companyId && account.ClosedAtUtc == null);

    public decimal GetCompanyBankBalance(Guid companyId)
        => CompanyBankingService.GetTotalBalance(GetCompanyBankAccounts(companyId));

    public BankAccount? GetCompanyFundingAccount(Guid companyId, string? currencyCode = null, Guid? excludeAccountId = null)
    {
        var accounts = GetCompanyBankAccounts(companyId);
        return string.IsNullOrWhiteSpace(currencyCode)
            ? CompanyBankingService.FindAnyPreferredAccount(accounts, excludeAccountId)
            : CompanyBankingService.FindPreferredAccount(accounts, currencyCode, excludeAccountId)
                ?? CompanyBankingService.FindAnyPreferredAccount(accounts, excludeAccountId);
    }

    public BankAccount? GetBuildingFundingAccount(Building building)
    {
        if (building.BankAccountId.HasValue && BankAccountsById.TryGetValue(building.BankAccountId.Value, out var bankAccount))
        {
            return bankAccount;
        }

        return GetCompanyFundingAccount(building.CompanyId, CitiesById.GetValueOrDefault(building.CityId)?.CurrencyCode);
    }

    public decimal GetCommodityShockMultiplier(Guid? resourceTypeId)
    {
        if (!resourceTypeId.HasValue) return 1m;
        return CommodityShockMultiplierByResourceId.TryGetValue(resourceTypeId.Value, out var multiplier)
            ? multiplier
            : 1m;
    }

    public decimal GetInterestRateMultiplier(Guid? cityId)
    {
        if (cityId.HasValue && InterestRateMultiplierByCityId.TryGetValue(cityId.Value, out var cityMultiplier))
            return cityMultiplier;
        return GlobalInterestRateMultiplier;
    }

    public decimal GetSeasonalDemandEventMultiplier(Guid cityId)
    {
        if (SeasonalDemandMultiplierByCityId.TryGetValue(cityId, out var cityMultiplier))
            return cityMultiplier;
        return GlobalSeasonalDemandMultiplier;
    }

    /// <summary>
    /// Returns the aggregate mine-efficiency multiplier for a given city, derived from active global events.
    /// If no city-specific multiplier is recorded, returns the global multiplier (product of city-agnostic events)
    /// or 1.0 if no global events affect mining.
    /// </summary>
    public decimal GetGlobalEventMineEfficiency(Guid cityId)
    {
        return GlobalEventMineEfficiencyByCityId.TryGetValue(cityId, out var m) ? m : 1.0m;
    }

}
