using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// One paid first-week campaign reward (#602). The unique (PlayerId, GoalCode) index is the idempotency and concurrency guard:
/// of two simultaneous claims for the same goal exactly one insert succeeds and only that one credits the account.
/// </summary>
public sealed class PlayerCampaignClaim
{
    public Guid Id { get; set; }

    public Guid PlayerId { get; set; }

    public Player Player { get; set; } = null!;

    /// <summary>The company whose bank account received the reward.</summary>
    public Guid CompanyId { get; set; }

    [Required, MaxLength(60)]
    public string GoalCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    public long ClaimedAtTick { get; set; }

    public DateTime ClaimedAtUtc { get; set; } = DateTime.UtcNow;
}
