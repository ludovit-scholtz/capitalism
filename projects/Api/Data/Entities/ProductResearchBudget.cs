namespace Api.Data.Entities;

/// <summary>
/// Tracks the accumulated R&amp;D research budget a company has invested into a specific product.
/// Budget grows each tick when a PRODUCT_QUALITY unit operates on that product,
/// decays by 0.1% per tick, and is used to derive product brand quality relative
/// to the strongest competing researcher.
/// </summary>
public sealed class ProductResearchBudget
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The company that owns this research investment.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Navigation property to the owning company.</summary>
    public Company Company { get; set; } = null!;

    /// <summary>The product type being researched.</summary>
    public Guid ProductTypeId { get; set; }

    /// <summary>Navigation property to the researched product.</summary>
    public ProductType ProductType { get; set; } = null!;

    /// <summary>
    /// Total accumulated research spending for this product (in game currency).
    /// Increases each tick by <c>unitOperatingCost × ResearchBudgetConversionRate(level)</c>.
    /// Decays by 0.1% (<see cref="Api.Engine.GameConstants.ResearchDecayRate"/>) per tick.
    /// Reaching <see cref="Api.Engine.GameConstants.ResearchBaseQualityBudget"/> while uncontested
    /// yields 100% product quality.
    /// </summary>
    public decimal AccumulatedBudget { get; set; }
}
