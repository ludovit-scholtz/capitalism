namespace Api.Data.Entities;

/// <summary>
/// Defines the raw materials required to manufacture a product.
/// Each recipe entry specifies one input resource and the quantity needed.
/// </summary>
public sealed class ProductRecipe
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The product that requires this ingredient.</summary>
    public Guid ProductTypeId { get; set; }

    /// <summary>Navigation property to the product type.</summary>
    public ProductType ProductType { get; set; } = null!;

    /// <summary>The raw material required.</summary>
    public Guid ResourceTypeId { get; set; }

    /// <summary>Navigation property to the resource type.</summary>
    public ResourceType ResourceType { get; set; } = null!;

    /// <summary>Quantity of the resource needed per product unit.</summary>
    public decimal Quantity { get; set; } = 1m;
}
