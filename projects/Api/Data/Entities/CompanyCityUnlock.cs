namespace Api.Data.Entities;

/// <summary>
/// Permanent unlock state for a company-city pair after the company reaches the required threshold.
/// </summary>
public sealed class CompanyCityUnlock
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid CityId { get; set; }
    public City City { get; set; } = null!;

    public long UnlockedAtTick { get; set; }
    public DateTime UnlockedAtUtc { get; set; } = DateTime.UtcNow;
}
