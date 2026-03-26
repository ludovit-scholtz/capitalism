using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Defines a product that can be manufactured and sold.
/// Products have quality ratings that affect sales performance.
/// </summary>
public sealed class ProductType
{
    /// <summary>Unique identifier for the product type.</summary>
    public Guid Id { get; set; }

    /// <summary>Name of the product (e.g., "Wooden Chair", "Steel Beam").</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly identifier.</summary>
    [Required, MaxLength(200)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Industry category: FURNITURE, FOOD, HEALTHCARE, etc.</summary>
    [Required, MaxLength(50)]
    public string Industry { get; set; } = string.Empty;

    /// <summary>Base market price per unit.</summary>
    public decimal BasePrice { get; set; }

    /// <summary>Base manufacturing time in ticks per unit.</summary>
    public int BaseCraftTicks { get; set; } = 1;

    /// <summary>Whether this product is available to Pro subscribers only.</summary>
    public bool IsProOnly { get; set; }

    /// <summary>Description shown in the encyclopaedia.</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Required raw materials to manufacture this product.</summary>
    public ICollection<ProductRecipe> Recipes { get; set; } = [];
}

/// <summary>Defines industry categories for products.</summary>
public static class Industry
{
    public const string Furniture = "FURNITURE";
    public const string FoodProcessing = "FOOD_PROCESSING";
    public const string Healthcare = "HEALTHCARE";
    public const string Electronics = "ELECTRONICS";
    public const string Construction = "CONSTRUCTION";

    public static readonly string[] StarterIndustries = [Furniture, FoodProcessing, Healthcare];
}
