namespace Api.Data.Entities;

/// <summary>
/// Tracks that a referred player registered via a specific referral code.
/// </summary>
public sealed class ReferralRegistration
{
    public Guid Id { get; set; }

    public Guid ReferralCodeId { get; set; }

    public Guid ReferredPlayerId { get; set; }

    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;

    public ReferralCode ReferralCode { get; set; } = null!;

    public Player ReferredPlayer { get; set; } = null!;
}
