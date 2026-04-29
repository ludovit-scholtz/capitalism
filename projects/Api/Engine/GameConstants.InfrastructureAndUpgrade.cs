using Api.Data.Entities;

namespace Api.Engine;

public static partial class GameConstants
{
    // ── Power grid constants ──────────────────────────────────────────────────

    /// <summary>
    /// Power demand in MW for a building of the given type and level.
    /// Power plants themselves consume no power (they produce it).
    /// </summary>
    public static decimal PowerDemandMw(string buildingType, int level) => buildingType switch
    {
        Data.Entities.BuildingType.Mine                => 2m * level,
        Data.Entities.BuildingType.Factory             => 5m * level,
        Data.Entities.BuildingType.SalesShop           => 1m * level,
        Data.Entities.BuildingType.ResearchDevelopment => 3m * level,
        Data.Entities.BuildingType.Apartment           => 0.5m * level,
        Data.Entities.BuildingType.Commercial          => 1m * level,
        Data.Entities.BuildingType.MediaHouse          => 2m * level,
        Data.Entities.BuildingType.Bank                => 1m * level,
        Data.Entities.BuildingType.Exchange            => 1m * level,
        Data.Entities.BuildingType.PowerPlant          => 0m,
        _                                              => 1m * level
    };

    /// <summary>
    /// Default power output in MW for a power plant by plant type.
    /// Used when a power plant building is created without an explicit output.
    /// </summary>
    public static decimal DefaultPowerOutputMw(string? powerPlantType) => powerPlantType switch
    {
        PowerPlantType.Coal    => 50m,
        PowerPlantType.Gas     => 40m,
        PowerPlantType.Solar   => 20m,
        PowerPlantType.Wind    => 25m,
        PowerPlantType.Nuclear => 200m,
        _                      => 30m
    };

    /// <summary>
    /// Fraction of capacity a CONSTRAINED building operates at.
    /// Applied to manufacturing batches, mining rate, and sales capacity.
    /// </summary>
    public const decimal ConstrainedEfficiencyFactor = 0.5m;

    // ── Power plant unit constants ────────────────────────────────────────────

    /// <summary>
    /// Additional MW output per level of a POWER_GENERATION unit installed in a power plant.
    /// A Level 1 unit adds 10 MW to the plant's rated output before weather scaling.
    /// </summary>
    public const decimal PowerGenerationUnitBoostMwPerLevel = 10m;

    /// <summary>
    /// MW of effective supply smoothing provided per level of a BATTERY_STORAGE unit.
    /// Battery units reduce the effective shortage seen by the grid fine calculator.
    /// </summary>
    public const decimal BatterySmoothingMwPerLevel = 5m;

    /// <summary>
    /// Additional MW of fuel-based output capacity per level of a FUEL_PURCHASE unit.
    /// Represents the fuel procurement and supply chain for coal/gas plants.
    /// </summary>
    public const decimal FuelPurchaseBoostMwPerLevel = 10m;

    /// <summary>
    /// Additional MW of wind-generated output per level of a WIND_TURBINE unit.
    /// This value is scaled by the current weather wind percentage each tick.
    /// </summary>
    public const decimal WindTurbineBoostMwPerLevel = 8m;

    /// <summary>
    /// Additional MW of steady hydro-electric output per level of a WATER_TURBINE unit.
    /// Water turbines are not affected by weather and provide reliable baseline generation.
    /// </summary>
    public const decimal WaterTurbineBoostMwPerLevel = 12m;

    /// <summary>
    /// MW of mechanical-energy smoothing per level of an ENERGY_STORAGE unit.
    /// Stores wind/water mechanical force to buffer variable renewable output,
    /// reducing exposure to shortage fines during low-generation periods.
    /// </summary>
    public const decimal EnergyStorageSmoothingMwPerLevel = 8m;

    /// <summary>
    /// Additional MW of converted electricity per level of an ENERGY_PRODUCING unit.
    /// The main electricity conversion stage: consumes fuel or mechanical force to generate
    /// grid-ready electricity. Highest output boost per unit but requires upstream units.
    /// </summary>
    public const decimal EnergyProducingBoostMwPerLevel = 20m;

    // ── Grid economics constants ──────────────────────────────────────────────

    /// <summary>
    /// Revenue earned per MW of surplus power delivered to the grid per tick.
    /// Paid proportionally to each plant's share of installed capacity.
    /// </summary>
    public const decimal GridSurplusIncomePerMwTick = 5m;

    /// <summary>
    /// Government fine per MW of power shortage per tick.
    /// Charged proportionally to each operator's share of installed capacity.
    /// </summary>
    public const decimal GridFinePerMwTick = 8m;

    // ── Fuel procurement constants (COAL/GAS plants) ──────────────────────────

    /// <summary>
    /// Whether a power-plant type uses thermal fuel (COAL or GAS).
    /// Only thermal plants have fuel procurement and fuel reserve mechanics.
    /// Renewable (SOLAR, WIND, WATER) and nuclear plants do not procure fuel.
    /// </summary>
    public static bool IsThermalPlant(string? powerPlantType) =>
        powerPlantType is PowerPlantType.Coal or PowerPlantType.Gas;

    /// <summary>
    /// Base cost (in EUR) per MWh of fuel procured by a FUEL_PURCHASE unit for COAL plants.
    /// Scaled by <c>City.FuelPriceIndex</c> and the city FX rate to give the
    /// local-currency cost per tick.
    /// </summary>
    public const decimal FuelCostPerMwhBase = 3m;

    /// <summary>
    /// Gas turbine fuel-cost multiplier applied on top of <see cref="FuelCostPerMwhBase"/>.
    /// Natural gas burns cleaner but is priced at a premium over coal.
    /// A GAS plant therefore pays 20% more per MWh of fuel than a COAL plant.
    /// </summary>
    public const decimal GasFuelCostMultiplier = 1.2m;

    /// <summary>
    /// Returns the effective fuel cost per MWh (in EUR, before city FX scaling) for a given
    /// thermal plant type. GAS plants pay a 20% premium over COAL.
    /// </summary>
    public static decimal FuelCostPerMwhForPlantType(string? plantType) =>
        plantType == Data.Entities.PowerPlantType.Gas
            ? FuelCostPerMwhBase * GasFuelCostMultiplier
            : FuelCostPerMwhBase;

    /// <summary>
    /// Maximum fuel reserve capacity per level of a FUEL_PURCHASE unit, in MWh.
    /// A single level-1 FUEL_PURCHASE unit can hold up to 50 MWh of reserve fuel.
    /// Reserve caps prevent infinite fuel hoarding and give dispatch decisions meaning.
    /// </summary>
    public const decimal FuelReserveCapacityPerUnitLevel = 50m;

    // ── Market trend constants ────────────────────────────────────────────────

    /// <summary>
    /// Neutral trend factor, no boost or penalty.
    /// </summary>
    public const decimal TrendNeutral = 1.0m;

    /// <summary>Minimum trend factor (maximum cold-market penalty).</summary>
    public const decimal TrendMin = 0.5m;

    /// <summary>Maximum trend factor (maximum hot-market boost).</summary>
    public const decimal TrendMax = 1.5m;

    /// <summary>
    /// Amount the trend factor rises per tick when sales utilisation exceeds
    /// <see cref="TrendStrongUtilisationThreshold"/>.
    /// </summary>
    public const decimal TrendRiseRate = 0.03m;

    /// <summary>
    /// Amount the trend factor falls per tick when sales utilisation is below
    /// <see cref="TrendWeakUtilisationThreshold"/> while stock was ample.
    /// </summary>
    public const decimal TrendFallRate = 0.03m;

    /// <summary>
    /// Fraction of the gap between the current trend factor and neutral (1.0)
    /// that decays per tick during moderate sales performance.
    /// </summary>
    public const decimal TrendDecayFraction = 0.10m;

    /// <summary>
    /// Utilisation ratio above which sales are classified as strong,
    /// causing the trend to rise.
    /// </summary>
    public const decimal TrendStrongUtilisationThreshold = 0.75m;

    /// <summary>
    /// Utilisation ratio below which sales are classified as weak,
    /// causing the trend to fall (only when stock was ample to rule out supply constraints).
    /// </summary>
    public const decimal TrendWeakUtilisationThreshold = 0.25m;

    /// <summary>
    /// Amplitude of the bounded random demand variation applied per tick.
    /// The demand multiplier is drawn from [1 - amplitude, 1 + amplitude].
    /// 0.08 means plus/minus 8% variation around baseline city demand.
    /// </summary>
    public const decimal TrendRandomAmplitude = 0.08m;

    // ── Analytics constants ───────────────────────────────────────────────────

    /// <summary>
    /// Revenue change threshold (as a fraction) used when computing trend direction
    /// for public-sales analytics. Changes within plus/minus 5% of the prior 5-tick average
    /// are classified as FLAT. Changes above or below this band are UP or DOWN.
    /// </summary>
    public const decimal FlatTrendThresholdPct = 0.05m;

    // ── Unit upgrade constants ────────────────────────────────────────────────

    /// <summary>Maximum upgrade level for building units.</summary>
    public const int MaxUnitLevel = 4;

    /// <summary>
    /// Maximum number of units that can be upgrading simultaneously within one building.
    /// A second upgrade can be queued while the first is still in progress.
    /// </summary>
    public const int MaxConcurrentUnitUpgrades = 2;

    /// <summary>
    /// Ticks required to upgrade a unit from <paramref name="currentLevel"/> to the next level.
    /// Level 1->2 = 10 ticks, 2->3 = 100 ticks, 3->4 = 1000 ticks.
    /// </summary>
    public static int UnitUpgradeTicks(int currentLevel) => currentLevel switch
    {
        1 => 10,
        2 => 100,
        3 => 1000,
        _ => 10 * (int)Math.Pow(10, Math.Max(currentLevel - 1, 0))
    };

    /// <summary>
    /// Cash cost to upgrade a unit of the given type from <paramref name="currentLevel"/> to the next level.
    /// Costs scale with unit type strategic importance and level.
    /// </summary>
    public static decimal UnitUpgradeCost(string unitType, int currentLevel)
    {
        decimal baseCost = unitType switch
        {
            Data.Entities.UnitType.Mining          => 5_000m,
            Data.Entities.UnitType.Storage         => 2_000m,
            Data.Entities.UnitType.Manufacturing   => 8_000m,
            Data.Entities.UnitType.Purchase        => 3_000m,
            Data.Entities.UnitType.PublicSales     => 6_000m,
            Data.Entities.UnitType.B2BSales        => 4_000m,
            Data.Entities.UnitType.ProductQuality  => 10_000m,
            Data.Entities.UnitType.BrandQuality    => 10_000m,
            Data.Entities.UnitType.Marketing       => 5_000m,
            Data.Entities.UnitType.Branding        => 5_000m,
            Data.Entities.UnitType.PowerGeneration => 15_000m,
            Data.Entities.UnitType.BatteryStorage  => 12_000m,
            Data.Entities.UnitType.FuelPurchase    => 12_000m,
            Data.Entities.UnitType.WindTurbine     => 18_000m,
            Data.Entities.UnitType.WaterTurbine    => 25_000m,
            Data.Entities.UnitType.EnergyStorage   => 14_000m,
            Data.Entities.UnitType.EnergyProducing => 22_000m,
            _                                      => 4_000m
        };

        // Each subsequent level costs 5x more: L1->L2 = base, L2->L3 = 5x base, L3->L4 = 25x base.
        return baseCost * (decimal)Math.Pow(5, Math.Max(currentLevel - 1, 0));
    }

    /// <summary>Returns true when the given unit type supports level upgrades.</summary>
    public static bool IsUpgradableUnitType(string unitType) => unitType switch
    {
        Data.Entities.UnitType.Mining          => true,
        Data.Entities.UnitType.Storage         => true,
        Data.Entities.UnitType.Manufacturing   => true,
        Data.Entities.UnitType.Purchase        => true,
        Data.Entities.UnitType.PublicSales     => true,
        Data.Entities.UnitType.B2BSales        => true,
        Data.Entities.UnitType.ProductQuality  => true,
        Data.Entities.UnitType.BrandQuality    => true,
        Data.Entities.UnitType.PowerGeneration => true,
        Data.Entities.UnitType.BatteryStorage  => true,
        Data.Entities.UnitType.FuelPurchase    => true,
        Data.Entities.UnitType.WindTurbine     => true,
        Data.Entities.UnitType.WaterTurbine    => true,
        Data.Entities.UnitType.EnergyStorage   => true,
        Data.Entities.UnitType.EnergyProducing => true,
        _                                      => false
    };

    /// <summary>Returns the primary stat value for a unit type at the given level (for UI display).</summary>
    public static decimal GetUnitStat(string unitType, int level) => unitType switch
    {
        Data.Entities.UnitType.Mining          => MiningRate(level),
        Data.Entities.UnitType.Storage         => StorageUnitHoldingCapacity(level),
        Data.Entities.UnitType.Manufacturing   => ManufacturingBatches(level),
        Data.Entities.UnitType.Purchase        => PurchaseCapacity(level),
        Data.Entities.UnitType.PublicSales     => SalesCapacity(level),
        Data.Entities.UnitType.B2BSales        => SalesCapacity(level),
        Data.Entities.UnitType.ProductQuality  => ResearchQualityRate(level) * 1000m,
        Data.Entities.UnitType.BrandQuality    => ResearchEfficiencyRate(level) * 1000m,
        Data.Entities.UnitType.PowerGeneration => PowerGenerationUnitBoostMwPerLevel * level,
        Data.Entities.UnitType.BatteryStorage  => BatterySmoothingMwPerLevel * level,
        Data.Entities.UnitType.FuelPurchase    => FuelPurchaseBoostMwPerLevel * level,
        Data.Entities.UnitType.WindTurbine     => WindTurbineBoostMwPerLevel * level,
        Data.Entities.UnitType.WaterTurbine    => WaterTurbineBoostMwPerLevel * level,
        Data.Entities.UnitType.EnergyStorage   => EnergyStorageSmoothingMwPerLevel * level,
        Data.Entities.UnitType.EnergyProducing => EnergyProducingBoostMwPerLevel * level,
        _                                      => (decimal)level
    };

    /// <summary>Returns a human-readable label for the primary stat of the given unit type.</summary>
    public static string GetUnitStatLabel(string unitType) => unitType switch
    {
        Data.Entities.UnitType.Mining          => "units/tick",
        Data.Entities.UnitType.Storage         => "capacity",
        Data.Entities.UnitType.Manufacturing   => "batches/tick",
        Data.Entities.UnitType.Purchase        => "units/tick",
        Data.Entities.UnitType.PublicSales     => "units/tick",
        Data.Entities.UnitType.B2BSales        => "units/tick",
        Data.Entities.UnitType.ProductQuality  => "quality‰/tick",
        Data.Entities.UnitType.BrandQuality    => "efficiency‰/tick",
        Data.Entities.UnitType.PowerGeneration => "MW output boost",
        Data.Entities.UnitType.BatteryStorage  => "MW buffer",
        Data.Entities.UnitType.FuelPurchase    => "MW fuel capacity",
        Data.Entities.UnitType.WindTurbine     => "MW wind output",
        Data.Entities.UnitType.WaterTurbine    => "MW hydro output",
        Data.Entities.UnitType.EnergyStorage   => "MW force storage",
        Data.Entities.UnitType.EnergyProducing => "MW generation",
        _                                      => "level"
    };

    // ── Construction constants ────────────────────────────────────────────────

    /// <summary>
    /// Number of ticks required to construct a building of the given type.
    /// One tick = one hour; 24 ticks = one in-game day.
    /// </summary>
    public static int ConstructionTicks(string buildingType) => buildingType switch
    {
        Data.Entities.BuildingType.Mine                => 24,
        Data.Entities.BuildingType.Factory             => 48,
        Data.Entities.BuildingType.SalesShop           => 24,
        Data.Entities.BuildingType.ResearchDevelopment => 72,
        Data.Entities.BuildingType.Apartment           => 96,
        Data.Entities.BuildingType.Commercial          => 48,
        Data.Entities.BuildingType.MediaHouse          => 48,
        Data.Entities.BuildingType.Bank                => 72,
        Data.Entities.BuildingType.Exchange            => 96,
        Data.Entities.BuildingType.PowerPlant          => 120,
        _                                              => 24
    };

    /// <summary>
    /// Construction cost (in game currency) for a building of the given type.
    /// This is charged in addition to the land purchase price.
    /// </summary>
    public static decimal ConstructionCost(string buildingType) => buildingType switch
    {
        Data.Entities.BuildingType.Mine                => 5_000m,
        Data.Entities.BuildingType.Factory             => 15_000m,
        Data.Entities.BuildingType.SalesShop           => 8_000m,
        Data.Entities.BuildingType.ResearchDevelopment => 25_000m,
        Data.Entities.BuildingType.Apartment           => 40_000m,
        Data.Entities.BuildingType.Commercial          => 20_000m,
        Data.Entities.BuildingType.MediaHouse          => 30_000m,
        Data.Entities.BuildingType.Bank                => 50_000m,
        Data.Entities.BuildingType.Exchange            => 60_000m,
        Data.Entities.BuildingType.PowerPlant          => 80_000m,
        _                                              => 10_000m
    };

    // ── Stock exchange tax constants ──────────────────────────────────────────

    /// <summary>
    /// Fraction of stock-sale proceeds reserved for taxes when selling from a personal account.
    /// 15% of each natural-person stock sale is held in <see cref="Api.Data.Entities.Player.PersonalTaxReserve"/>
    /// until the year-end tax settlement.
    /// </summary>
    public const decimal PersonalStockSaleTaxRate = 0.15m;

    // ── Currency / FX constants ───────────────────────────────────────────────

    /// <summary>
    /// Minimum EUR-based FX multiplier at which a city is considered high-rate and lot prices
    /// must be expressed in local currency (not EUR).
    /// </summary>
    public const decimal HighFxRateThreshold = 1.5m;

    /// <summary>
    /// Any unowned lot whose <c>BasePrice</c> is below this value in a high-FX-rate city is assumed
    /// to have been generated before currency-scaling was introduced (EUR-anchored value).
    /// </summary>
    public const decimal EurAnchoredLotBasePriceThreshold = 500_000m;

    /// <summary>
    /// Email address used as the requester identity when the game server automatically
    /// publishes system-generated entries (e.g. city market reports) to the MasterApi newsroom.
    /// </summary>
    public const string SystemRequesterEmail = "system@capitalism.internal";
}
