using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Represents a building on the game map. Buildings are owned by companies
/// and contain a 4x4 unit grid for internal configuration.
/// </summary>
public sealed class Building
{
    /// <summary>Unique identifier for the building.</summary>
    public Guid Id { get; set; }

    /// <summary>The company that owns this building.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Navigation property to the owning company.</summary>
    public Company Company { get; set; } = null!;

    /// <summary>The city where this building is located.</summary>
    public Guid CityId { get; set; }

    /// <summary>Navigation property to the city.</summary>
    public City City { get; set; } = null!;

    /// <summary>Building type: MINE, FACTORY, SALES_SHOP, etc.</summary>
    [Required, MaxLength(30)]
    public string Type { get; set; } = string.Empty;

    /// <summary>Display name of the building.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Latitude position on the game map.</summary>
    public double Latitude { get; set; }

    /// <summary>Longitude position on the game map.</summary>
    public double Longitude { get; set; }

    /// <summary>Current building level affecting stats.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Power consumption in MW.</summary>
    public decimal PowerConsumption { get; set; }

    /// <summary>Whether the building is listed for sale to other players.</summary>
    public bool IsForSale { get; set; }

    /// <summary>Asking price if the building is for sale.</summary>
    public decimal? AskingPrice { get; set; }

    /// <summary>Price per m² for apartment and commercial buildings.</summary>
    public decimal? PricePerSqm { get; set; }

    /// <summary>
    /// Pending (future) rent per m² set by the player. Applied on the tick
    /// specified by <see cref="PendingPriceActivationTick"/> (one in-game day = 24 ticks after submission).
    /// </summary>
    public decimal? PendingPricePerSqm { get; set; }

    /// <summary>
    /// Tick number at which <see cref="PendingPricePerSqm"/> becomes the active
    /// <see cref="PricePerSqm"/>. Null when no pending change is queued.
    /// </summary>
    public long? PendingPriceActivationTick { get; set; }

    /// <summary>Occupancy percentage (0-100) for apartment/commercial buildings.</summary>
    public decimal? OccupancyPercent { get; set; }

    /// <summary>Total area in m² for apartment/commercial buildings.</summary>
    public decimal? TotalAreaSqm { get; set; }

    /// <summary>Power plant type: COAL, GAS, NUCLEAR, SOLAR, WIND (only for power plants).</summary>
    [MaxLength(20)]
    public string? PowerPlantType { get; set; }

    /// <summary>Power output in MW (only for power plants).</summary>
    public decimal? PowerOutput { get; set; }

    /// <summary>
    /// Power supply status set each tick by the PowerDistributionPhase.
    /// Values: POWERED, CONSTRAINED, OFFLINE.
    /// Power plants themselves are always POWERED (they produce, not consume).
    /// </summary>
    [MaxLength(20)]
    public string PowerStatus { get; set; } = Entities.PowerStatus.Powered;

    /// <summary>Media type: NEWSPAPER, RADIO, TV (only for media houses).</summary>
    [MaxLength(20)]
    public string? MediaType { get; set; }

    /// <summary>
    /// Accumulated content budget for media house buildings.
    /// Increases when the media house spends on content each tick; decays 0.5% per tick.
    /// Used to compute content ranking across competitors in the same city and media category.
    /// </summary>
    public decimal ContentValue { get; set; }

    /// <summary>
    /// Per-tick content spending amount configured by the media house owner.
    /// Each tick this amount is deducted from the company cash and converted into
    /// <see cref="ContentValue"/> using the level-based efficiency formula:
    /// efficiency = 1 – 1/(level+1)  (50% at level 1, 66% at level 2, …).
    /// Null or zero means no active content investment.
    /// </summary>
    public decimal? ContentBudgetPerTick { get; set; }

    /// <summary>
    /// True when this building is a government-owned media house seeded as the baseline
    /// city media market.  Government outlets are always visible and operational but players
    /// cannot buy or configure them.
    /// </summary>
    public bool IsGovernmentOwned { get; set; }

    /// <summary>Interest rate percentage for banks (legacy; use DepositInterestRatePercent/LendingInterestRatePercent).</summary>
    public decimal? InterestRate { get; set; }

    /// <summary>Annual interest rate (%) the bank pays to depositors. Null for non-bank buildings.</summary>
    public decimal? DepositInterestRatePercent { get; set; }

    /// <summary>Annual interest rate (%) the bank charges on loans. Null for non-bank buildings.</summary>
    public decimal? LendingInterestRatePercent { get; set; }

    /// <summary>
    /// Cached total of active deposit-account balances in this bank.
    /// Summed from BankAccount.Balance where BankBuildingId matches and ClosedAtUtc is null.
    /// Updated on each deposit/withdrawal mutation. Only meaningful for BANK buildings.
    /// </summary>
    public decimal TotalDeposits { get; set; }

    /// <summary>
    /// True when the bank's $10,000,000 base-capital deposit has been created.
    /// Prevents double-charging on any re-initialisation path.
    /// </summary>
    public bool BaseCapitalDeposited { get; set; }

    /// <summary>
    /// Outstanding debt owed to the central bank as emergency liquidity funding.
    /// Accumulates when the bank cannot meet deposit-interest or withdrawal obligations from its own cash.
    /// Repaid automatically when depositors add funds or the bank has surplus cash.
    /// Only meaningful for BANK buildings.
    /// </summary>
    public decimal CentralBankDebt { get; set; }

    /// <summary>UTC timestamp when the building was constructed.</summary>
    public DateTime BuiltAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When true the building is currently under construction and cannot be operated or configured.
    /// The building becomes operational on tick <see cref="ConstructionCompletesAtTick"/>.
    /// </summary>
    public bool IsUnderConstruction { get; set; }

    /// <summary>
    /// Tick at which construction completes and the building transitions to operational.
    /// Null for buildings that were not started via the construction-order flow.
    /// </summary>
    public long? ConstructionCompletesAtTick { get; set; }

    /// <summary>
    /// Cash cost (in game currency) charged to the company when construction was ordered.
    /// Stored for auditing and future refund/cancel features.
    /// </summary>
    public decimal ConstructionCost { get; set; }

    /// <summary>
    /// The bank account used to fund operating costs (labor, energy) for this building.
    /// Null means no account has been assigned yet; provisioning paths should assign a
    /// company-owned account in the building city currency before the building starts operating.
    /// </summary>
    public Guid? BankAccountId { get; set; }

    /// <summary>Navigation property to the building's assigned bank account.</summary>
    public BankAccount? BankAccount { get; set; }

    /// <summary>
    /// True when the building's bank account had insufficient funds to cover operating costs
    /// on the most recent tick. The building is skipped (all operations suspended) for that tick.
    /// Reset to false as soon as the next tick finds sufficient funds.
    /// </summary>
    public bool IsSuspendedForFunds { get; set; }

    /// <summary>
    /// Human-readable reason the building is suspended, set alongside <see cref="IsSuspendedForFunds"/>.
    /// Null when the building is not suspended.
    /// Values: "MISSING_BANK_ACCOUNT" | "INSUFFICIENT_FUNDS:&lt;amount&gt;" | null.
    /// </summary>
    [MaxLength(200)]
    public string? SuspendedReason { get; set; }

    /// <summary>Units installed in this building's 4x4 grid.</summary>
    public ICollection<BuildingUnit> Units { get; set; } = [];

    /// <summary>Queued building configuration that will replace the active units on a future tick.</summary>
    public BuildingConfigurationPlan? PendingConfiguration { get; set; }
}

/// <summary>Valid media types for MEDIA_HOUSE buildings.</summary>
public static class MediaType
{
    public const string Newspaper = "NEWSPAPER";
    public const string Radio = "RADIO";
    public const string Tv = "TV";

    public static readonly string[] All = [Newspaper, Radio, Tv];

    /// <summary>
    /// Channel effectiveness multiplier applied to brand-awareness gain per unit of budget spent.
    /// TV has the widest reach; radio has moderate; newspaper is the baseline.
    /// </summary>
    public static decimal EffectivenessMultiplier(string? mediaType) => mediaType switch
    {
        Newspaper => 1.0m,
        Radio     => 1.5m,
        Tv        => 2.0m,
        _         => 1.0m
    };
}
