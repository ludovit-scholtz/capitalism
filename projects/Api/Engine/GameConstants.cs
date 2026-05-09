using Api.Data.Entities;

namespace Api.Engine;

/// <summary>
/// Game balance constants for the tick-based economy simulation.
/// All capacity, rate, and demand values are tunable here.
/// </summary>
public static partial class GameConstants
{
    public const int GameStartYear = 2000;
    public const int TicksPerDay = 24;
    public const int DaysPerYear = 365;
    public const int TicksPerYear = TicksPerDay * DaysPerYear;

    /// <summary>Game ticks in one in-game week (7 days × 24 ticks/day).</summary>
    public const int TicksPerWeek = TicksPerDay * 7;

    /// <summary>Game ticks in one in-game month (30 days × 24 ticks/day).</summary>
    public const int TicksPerMonth = TicksPerDay * 30;

    /// <summary>
    /// Game ticks in one in-game quarter (one-fourth of a game year).
    /// Q1 = Jan–Mar, Q2 = Apr–Jun, Q3 = Jul–Sep, Q4 = Oct–Dec.
    /// Used for seasonal demand multiplier calculation.
    /// </summary>
    public const int TicksPerQuarter = TicksPerYear / 4;

    /// <summary>Base holding capacity (units) per unit level for purchase, sales, mining, and manufacturing units.</summary>
    public static decimal StorageCapacity(int level) => level switch
    {
        1 => 100m,
        2 => 250m,
        3 => 500m,
        4 => 1000m,
        _ => 100m * (decimal)Math.Pow(2, Math.Max(level - 1, 0))
    };

    /// <summary>
    /// Maximum holding capacity for dedicated STORAGE units per level.
    /// Storage units hold 10× the base capacity so players have a meaningful logistical
    /// advantage when placing dedicated storage between production and sales stages.
    /// </summary>
    public static decimal StorageUnitHoldingCapacity(int level) => level switch
    {
        1 => 1000m,
        2 => 2500m,
        3 => 5000m,
        4 => 10000m,
        _ => 1000m * (decimal)Math.Pow(2, Math.Max(level - 1, 0))
    };

    /// <summary>
    /// Returns the holding capacity for a unit of the given type and level.
    /// STORAGE units have 10× the capacity of purchase, sales, mining, and manufacturing units.
    /// The parameter is typed as <see langword="string"/> for GraphQL interop — unit type values
    /// are stored and transmitted as strings matching the <see cref="Data.Entities.UnitType"/> constants.
    /// </summary>
    public static decimal GetUnitHoldingCapacity(string unitType, int level) =>
        unitType == Data.Entities.UnitType.Storage
            ? StorageUnitHoldingCapacity(level)
            : StorageCapacity(level);

    /// <summary>Mining production per tick per unit level (multiplied by city abundance).</summary>
    public static decimal MiningRate(int level) => level switch
    {
        1 => 10m,
        2 => 25m,
        3 => 50m,
        4 => 100m,
        _ => 10m * (decimal)Math.Pow(2, Math.Max(level - 1, 0))
    };

    /// <summary>Manufacturing batches per tick per unit level.</summary>
    public static int ManufacturingBatches(int level) => level switch
    {
        1 => 1,
        2 => 2,
        3 => 4,
        4 => 8,
        _ => (int)Math.Pow(2, Math.Max(level - 1, 0))
    };

    /// <summary>Public sales capacity (units sold) per tick per unit level.</summary>
    public static decimal SalesCapacity(int level) => level switch
    {
        1 => 20m,
        2 => 50m,
        3 => 100m,
        4 => 200m,
        _ => 20m * (decimal)Math.Pow(2, Math.Max(level - 1, 0))
    };

    /// <summary>Purchase capacity (units bought) per tick per unit level.</summary>
    public static decimal PurchaseCapacity(int level) => level switch
    {
        1 => 50m,
        2 => 100m,
        3 => 200m,
        4 => 400m,
        _ => 50m * (decimal)Math.Pow(2, Math.Max(level - 1, 0))
    };

    /// <summary>Base demand per capita per product per tick.</summary>
    public const decimal BaseDemandPerCapita = 0.001m;

    /// <summary>Interval used for macro-cycle/event re-evaluation (one in-game month).</summary>
    public const int EconomicCycleEvaluationIntervalTicks = TicksPerMonth;

    /// <summary>Lead time for recession warning notifications.</summary>
    public const int RecessionWarningLeadTicks = 48;

    public const decimal EconomicCycleIntensityMin = 0.5m;
    public const decimal EconomicCycleIntensityMax = 1.5m;
    public const decimal MarketEventMultiplierMin = 0.3m;
    public const decimal MarketEventMultiplierMax = 2.0m;

    /// <summary>
    /// Reference city salary used to normalise purchasing-power demand.
    /// Cities with a <see cref="Api.Data.Entities.City.BaseSalaryPerManhour"/> equal to this
    /// value produce a purchasing-power factor of 1.0 (no boost or penalty).
    /// Higher-wage cities attract proportionally more consumer spending.
    /// </summary>
    public const decimal ReferenceSalaryPerManhour = 20m;

    /// <summary>
    /// Number of past ticks included in the "recent salary" window used to
    /// compute dynamic purchasing-power demand (ROADMAP: "game currency
    /// collected by salaries in past 10 ticks").
    /// </summary>
    public const int RecentSalaryWindowTicks = 10;

    /// <summary>
    /// Expected fraction of a city's population that generates LaborCost
    /// ledger entries per tick through player-owned companies.  Used to
    /// normalise the dynamic salary spending signal into a [0.5, 2.0]
    /// purchasing-power factor.  0.001 = 0.1 % of population employed.
    /// </summary>
    public const decimal ExpectedSalaryParticipationRate = 0.001m;

    /// <summary>Standard cost of electricity used by unit operations and manufacturing.</summary>
    public const decimal EnergyPricePerMwh = 55m;

    /// <summary>Minimum number of purchasable lands maintained per building type and city.</summary>
    public const int MinimumAvailableLotsPerBuildingType = 10;

    /// <summary>Brand awareness increment per unit of marketing budget spent.</summary>
    public const decimal BrandAwarenessPerBudget = 0.0001m;

    /// <summary>
    /// Marketing-quality gain per unit of marketing budget spent.
    /// Much slower than awareness (1/20th) — brand prestige builds over many ticks of sustained investment.
    /// Combined with channel effectiveness and R&amp;D efficiency multipliers in MarketingPhase.
    /// </summary>
    public const decimal BrandMarketingQualityPerBudget = 0.000005m;

    /// <summary>
    /// Decay rate for marketing-driven brand quality per tick (0.03% per tick).
    /// Slower than research-budget decay (0.1%) so prestige erodes gradually when investment stops.
    /// </summary>
    public const decimal BrandMarketingQualityDecayRate = 0.0003m;

    /// <summary>
    /// Passive brand awareness gain rate per tick for public sales when product quality
    /// is above the city average for that product, or when the company is the only seller
    /// in the city for that product.
    /// This is much slower than marketing spend so it rewards R&amp;D investment without
    /// making dedicated marketing redundant.
    /// </summary>
    public const decimal PassiveBrandAwarenessGainRate = 0.0005m;

    /// <summary>
    /// Passive brand awareness decay rate per tick for public sales when product quality
    /// is below the city average for that product. Inferior products slowly erode
    /// consumer perception even without active negative signals.
    /// </summary>
    public const decimal PassiveBrandAwarenessDecayRate = 0.0003m;

    /// <summary>
    /// Number of recent ticks included in the campaign analytics window.
    /// All revenue, spend, and performance metrics in <c>CampaignAnalyticsResult</c>
    /// are computed over this window so players see current-campaign performance.
    /// </summary>
    public const int CampaignAnalyticsWindowTicks = 10;

    /// <summary>
    /// Maximum demand boost factor that combined brand quality (R&amp;D + marketing) can provide
    /// on top of the awareness-based brand factor.
    /// At full combined quality (1.0) the brand factor receives a 50% bonus multiplier.
    /// Formula: effectiveBrandFactor = awarenessBasedFactor × (1 + combinedQuality × BrandQualityBoostFactor).
    /// </summary>
    public const decimal BrandQualityBoostFactor = 0.5m;

    /// <summary>
    /// Maximum marketing efficiency multiplier achievable through BRAND_QUALITY R&amp;D.
    /// At full research saturation the company's marketing budget is this many times more effective.
    /// </summary>
    public const decimal MaxMarketingEfficiencyMultiplier = 2m;

    // ── Media house content constants ─────────────────────────────────────────

    /// <summary>
    /// Fraction of ContentValue that decays every tick for all media house buildings.
    /// 0.005 = 0.5% per tick as specified by the ROADMAP.
    /// </summary>
    public const decimal MediaHouseContentDecayRate = 0.005m;

    /// <summary>
    /// Converts a media house building level to its content-accumulation efficiency.
    /// Level 1 → 50%, level 2 → 66.7%, level 3 → 75%, …
    /// Formula: efficiency = 1 – 1/(level+1)
    /// </summary>
    public static decimal MediaHouseContentEfficiency(int level) =>
        1m - 1m / ((decimal)level + 1m);

    // ── Media house building upgrade constants ────────────────────────────────

    /// <summary>Maximum upgrade level for a media house building.</summary>
    public const int MaxMediaHouseLevel = 5;

    /// <summary>
    /// Cash cost (in EUR, FX-adjusted at purchase time) to upgrade a media house
    /// from <paramref name="currentLevel"/> to the next level.
    /// Level 1→2: €50 000, 2→3: €150 000, 3→4: €400 000, 4→5: €1 000 000.
    /// </summary>
    public static decimal MediaHouseUpgradeCost(int currentLevel) => currentLevel switch
    {
        1 => 50_000m,
        2 => 150_000m,
        3 => 400_000m,
        4 => 1_000_000m,
        _ => 50_000m * (decimal)Math.Pow(3, Math.Max(currentLevel - 1, 0))
    };

    /// <summary>
    /// Ticks required to complete a media house upgrade from <paramref name="currentLevel"/>
    /// to the next level.  1 tick = 1 hour; 24 ticks = 1 in-game day.
    /// Level 1→2: 48 ticks (2 days), 2→3: 168 ticks (1 week),
    /// 3→4: 720 ticks (30 days), 4→5: 2160 ticks (90 days).
    /// </summary>
    public static int MediaHouseUpgradeTicks(int currentLevel) => currentLevel switch
    {
        1 => 48,
        2 => 168,
        3 => 720,
        4 => 2160,
        _ => 48 * (int)Math.Pow(4, Math.Max(currentLevel - 1, 0))
    };

    /// <summary>
    /// Range of the brand-awareness boost from content ranking.
    /// A top-ranked outlet (100%) multiplies marketing effectiveness by
    /// 1.0 + ContentRankingMarketingBoostRange.  A zero-ranked outlet
    /// multiplies by 0.5 (half the base).
    /// The full range runs from 0.5× to (1.0 + range)×.
    /// </summary>
    public const decimal ContentRankingMarketingBoostRange = 1.0m;

    /// <summary>
    /// Base effectiveness multiplier applied even when content ranking is zero.
    /// Combined formula: multiplier = ContentRankingBaseMultiplier + rankingFraction * ContentRankingMarketingBoostRange
    /// At 0% ranking → 0.5×; at 100% ranking → 1.5×.
    /// </summary>
    public const decimal ContentRankingBaseMultiplier = 0.5m;

    /// <summary>
    /// Campaign-reach multipliers for Media House units.
    /// Newspaper is baseline, Radio has 1.8× reach, TV has 3.0× reach.
    /// </summary>
    public static decimal MediaHouseCampaignMultiplier(string? mediaType) => mediaType switch
    {
        Data.Entities.MediaType.Newspaper => 1.0m,
        Data.Entities.MediaType.Radio => 1.8m,
        Data.Entities.MediaType.Tv => 3.0m,
        _ => 1.0m
    };

    /// <summary>Estimated labor cost for a media-house campaign unit per tick.</summary>
    public static decimal MediaHouseLaborCostPerTick(decimal campaignBudgetPerTick) =>
        decimal.Round(Math.Max(25m, campaignBudgetPerTick * 0.08m), 2, MidpointRounding.AwayFromZero);

    /// <summary>Estimated energy cost for a media-house campaign unit per tick.</summary>
    public static decimal MediaHouseEnergyCostPerTick(decimal campaignBudgetPerTick) =>
        decimal.Round(Math.Max(10m, campaignBudgetPerTick * 0.03m), 2, MidpointRounding.AwayFromZero);

    /// <summary>R&amp;D efficiency multiplier increment per tick per unit level (for BRAND_QUALITY research).</summary>
    public static decimal ResearchEfficiencyRate(int level) => level switch
    {
        1 => 0.0005m,
        2 => 0.001m,
        3 => 0.002m,
        4 => 0.004m,
        _ => 0.0005m * (decimal)Math.Pow(2, Math.Max(level - 1, 0))
    };

    /// <summary>R&amp;D quality increment per tick per unit level (for PRODUCT_QUALITY research).</summary>
    public static decimal ResearchQualityRate(int level) => level switch
    {
        1 => 0.001m,
        2 => 0.002m,
        3 => 0.004m,
        4 => 0.008m,
        _ => 0.001m * (decimal)Math.Pow(2, Math.Max(level - 1, 0))
    };

    /// <summary>
    /// Fraction of a PRODUCT_QUALITY unit's operating costs accumulated as research budget per tick.
    /// Formula: 1 - 1/(level+1) — Level 1: 50%, Level 2: 66.7%, Level 3: 75%, Level 4: 80%.
    /// Upgrades improve conversion efficiency as described in the ROADMAP.
    /// </summary>
    public static decimal ResearchBudgetConversionRate(int level)
    {
        var l = Math.Max(1, level);
        return 1m - 1m / (l + 1m);
    }

    /// <summary>
    /// Fraction of accumulated research budget lost per tick (decay rate).
    /// 0.001 = 0.1% — a company that stops investing in R&amp;D will slowly lose quality over time.
    /// </summary>
    public const decimal ResearchDecayRate = 0.001m;

    /// <summary>
    /// Minimum accumulated research budget required to reach 100% product quality when uncontested
    /// (i.e., the company is the sole researcher for that product).
    /// Computed as max(5 000, basePrice × 1 000) to give cheaper products a sensible floor.
    /// </summary>
    public static decimal ResearchBaseQualityBudget(decimal basePrice) =>
        Math.Max(5_000m, basePrice * 1_000m);

    /// <summary>
    /// Number of game ticks between resource replenishment events (one game year = 8 760 ticks).
    /// Every this many ticks a fraction of fully-depleted mine lots in each city is partially restored.
    /// </summary>
    public const int ReplenishmentIntervalTicks = TicksPerYear;

    /// <summary>Minimum fraction of <see cref="BuildingLot.OriginalMaterialQuantity"/> restored per replenishment event (20%).</summary>
    public const decimal ReplenishmentMinRestoreFraction = 0.10m;

    /// <summary>Maximum fraction of <see cref="BuildingLot.OriginalMaterialQuantity"/> restored per replenishment event (30%).</summary>
    public const decimal ReplenishmentMaxRestoreFraction = 0.30m;

    /// <summary>Minimum fraction of depleted lots selected for replenishment per city per cycle (20%).</summary>
    public const decimal ReplenishmentMinLotFraction = 0.20m;

    /// <summary>Maximum fraction of depleted lots selected for replenishment per city per cycle (30%).</summary>
    public const decimal ReplenishmentMaxLotFraction = 0.30m;

    /// <summary>
    /// Depletion risk threshold: a mine lot is flagged as "Depletion Risk" when remaining
    /// quantity falls below this fraction of the original deposit.
    /// </summary>
    public const decimal DepletionRiskThreshold = 0.20m;

    /// <summary>Rate at which occupancy adjusts per tick toward equilibrium.</summary>
    public const decimal OccupancyAdjustmentRate = 0.5m;

    /// <summary>
    /// When a property is priced above the location-adjusted market rate, occupancy
    /// drifts toward this floor value (ROADMAP: "slowly decrease to 50%").
    /// </summary>
    public const decimal OccupancyOverpricedFloor = 50m;

    /// <summary>
    /// Price ratio threshold below which a building can achieve 100% occupancy
    /// (ROADMAP: "below 60% of the city rate").
    /// </summary>
    public const decimal OccupancyFullCapPriceRatio = 0.60m;

    /// <summary>
    /// Price ratio threshold at which max occupancy is capped at 90%
    /// (ROADMAP: "current city rate adjusted by the location index plus 10%").
    /// </summary>
    public const decimal OccupancyNinetyPctCapPriceRatio = 1.10m;

    /// <summary>
    /// Maximum occupancy achievable when priced above the 60% threshold but at or below
    /// the +10% threshold (interpolated from 100% down to 90%).
    /// </summary>
    public const decimal OccupancyNinetyPctCap = 90m;

    /// <summary>
    /// Constant operating cost ratio for APARTMENT and COMMERCIAL buildings.
    /// Cost per tick = PricePerSqm × TotalAreaSqm × this ratio, which equals
    /// rent income at 75% occupancy (ROADMAP: "costs equal to earning at 75% occupancy").
    /// </summary>
    public const decimal PropertyBreakevenOccupancy = 0.75m;

    /// <summary>
    /// Initial occupancy for newly purchased apartment/commercial buildings.
    /// The value is always numeric (never null) so UI can consistently render 0%.
    /// </summary>
    public const decimal PropertyInitialOccupancyPercent = 0m;

    /// <summary>
    /// Default total area in m² for new apartment buildings.
    /// Used when no explicit area has been configured yet.
    /// </summary>
    public const decimal DefaultApartmentTotalAreaSqm = 1800m;

    /// <summary>
    /// Default total area in m² for new commercial buildings.
    /// Used when no explicit area has been configured yet.
    /// </summary>
    public const decimal DefaultCommercialTotalAreaSqm = 1400m;

    /// <summary>
    /// Returns the default total area (m²) for the given property building type.
    /// Returns null for non-property building types.
    /// </summary>
    public static decimal? DefaultPropertyAreaSqm(string buildingType) => buildingType switch
    {
        Data.Entities.BuildingType.Apartment => DefaultApartmentTotalAreaSqm,
        Data.Entities.BuildingType.Commercial => DefaultCommercialTotalAreaSqm,
        _ => null
    };

    // ── Building foreclosure / destruction ───────────────────────────────────

    /// <summary>
    /// Number of ticks a defaulted loan's collateral building stays listed for sale
    /// before it is automatically destroyed (3 game days × 24 ticks/day = 72 ticks).
    /// </summary>
    public const long ForeclosureWindowTicks = 72L;

    /// <summary>
    /// Fraction of the collateral's appraised value refunded to the owner's company
    /// settlement bank account when the building is destroyed after foreclosure.
    /// </summary>
    public const decimal ForeclosureRefundFraction = 0.80m;

    /// <summary>
    /// Auto-listing discount applied to a collateral building when a loan defaults.
    /// The building is listed at <c>(1 − ForeclosureAutoListDiscount) × appraisedValue</c>.
    /// </summary>
    public const decimal ForeclosureAutoListDiscount = 0.10m;
}
