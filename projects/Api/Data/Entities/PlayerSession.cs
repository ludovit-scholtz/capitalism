namespace Api.Data.Entities;

/// <summary>
/// Tracks authenticated JWT sessions for security review and revocation.
/// </summary>
public sealed class PlayerSession
{
    public string Jti { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public string? LastSeenIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public Player? Player { get; set; }
}
