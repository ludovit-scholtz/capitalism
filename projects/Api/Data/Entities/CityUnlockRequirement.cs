using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Configurable per-city unlock threshold expressed in USD-equivalent company net worth.
/// A zero threshold means the city is available immediately.
/// </summary>
public sealed class CityUnlockRequirement
{
    public Guid Id { get; set; }

    public Guid CityId { get; set; }
    public City City { get; set; } = null!;

    [Range(0, double.MaxValue)]
    public decimal RequiredNetWorthUsd { get; set; }
}
