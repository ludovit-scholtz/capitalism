using Api.Data.Entities;

namespace Api.Types;

/// <summary>
/// Aggregated payload returned by the <c>encyclopediaResource</c> query.
/// Bundles the requested resource with all products that directly consume it so the
/// encyclopedia detail view can be rendered in a single round-trip.
/// </summary>
public sealed class EncyclopediaResourceDetail
{
    /// <summary>The requested resource type (always non-null when the query succeeds).</summary>
    public ResourceType Resource { get; set; } = null!;

    /// <summary>
    /// All product types that include this resource as a direct ingredient
    /// (i.e. at least one <see cref="ProductRecipe"/> whose <c>ResourceTypeId</c> matches).
    /// Ordered by product name. Access metadata (<c>isUnlockedForCurrentPlayer</c>) is already
    /// applied based on the caller's Pro subscription status.
    /// </summary>
    public List<ProductType> ProductsUsingResource { get; set; } = [];
}

public sealed class EncyclopediaResourcePage
{
    public int Page { get; set; }

    public int TotalPages { get; set; }

    public int TotalCount { get; set; }

    public List<EncyclopediaCatalogEntry> Items { get; set; } = [];
}

public sealed class EncyclopediaCatalogEntry
{
    public Guid Id { get; set; }

    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Industry { get; set; }

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsPerishable { get; set; }

    public bool IsProOnly { get; set; }

    public bool IsUnlockedForCurrentPlayer { get; set; }

    public decimal BasePrice { get; set; }

    public decimal? WeightPerUnit { get; set; }

    public int? BaseCraftTicks { get; set; }

    public decimal? OutputQuantity { get; set; }

    public decimal? EnergyConsumptionMwh { get; set; }

    public decimal? BasicLaborHours { get; set; }

    public string UnitName { get; set; } = string.Empty;

    public string UnitSymbol { get; set; } = string.Empty;
}

public sealed class EncyclopediaRecipeCard
{
    public string Id { get; set; } = string.Empty;

    public string RecipeName { get; set; } = string.Empty;

    public string BuildingType { get; set; } = string.Empty;

    public decimal OutputQuantity { get; set; }

    public EncyclopediaCatalogEntry Output { get; set; } = null!;

    public List<EncyclopediaRecipeIngredient> Inputs { get; set; } = [];
}

public sealed class EncyclopediaRecipeIngredient
{
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Industry { get; set; }

    public string? ImageUrl { get; set; }

    public decimal Quantity { get; set; }

    public string UnitName { get; set; } = string.Empty;

    public string UnitSymbol { get; set; } = string.Empty;

    public bool IsPerishable { get; set; }

    public bool IsProOnly { get; set; }

    public bool IsUnlockedForCurrentPlayer { get; set; }
}

public sealed class EncyclopediaEntryDetail
{
    public EncyclopediaCatalogEntry Entry { get; set; } = null!;

    public List<EncyclopediaRecipeCard> ProducedByRecipes { get; set; } = [];

    public List<EncyclopediaRecipeCard> UsedInRecipes { get; set; } = [];
}
