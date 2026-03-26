using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Maps available natural resources to cities.
/// Determines what raw materials can be mined in a given location.
/// </summary>
public sealed class CityResource
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The city that has this resource.</summary>
    public Guid CityId { get; set; }

    /// <summary>Navigation property to the city.</summary>
    public City City { get; set; } = null!;

    /// <summary>The resource type available.</summary>
    public Guid ResourceTypeId { get; set; }

    /// <summary>Navigation property to the resource type.</summary>
    public ResourceType ResourceType { get; set; } = null!;

    /// <summary>Abundance level (0.0-1.0) affecting mining yield.</summary>
    public decimal Abundance { get; set; } = 0.5m;
}
