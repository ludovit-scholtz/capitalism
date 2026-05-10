using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Structured security audit log for optimistic-concurrency conflicts in the building secondary market.
/// </summary>
public sealed class BuildingOfferSecurityAuditLog
{
    public Guid Id { get; set; }
    public Guid OfferId { get; set; }
    public Guid BuyerPlayerId { get; set; }
    public Guid ActorPlayerId { get; set; }

    [Required, MaxLength(40)]
    public string Action { get; set; } = string.Empty;

    public Guid? ExpectedOfferVersion { get; set; }
    public Guid? ActualOfferVersion { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
