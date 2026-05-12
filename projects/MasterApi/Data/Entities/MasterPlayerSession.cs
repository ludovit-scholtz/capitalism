namespace MasterApi.Data.Entities;

/// <summary>
/// Tracks authenticated master-portal sessions represented by JWT tokens.
/// </summary>
public sealed class MasterPlayerSession
{
    public string Jti { get; set; } = string.Empty;
    public Guid PlayerAccountId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public string? LastSeenIpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }
    public PlayerAccount? PlayerAccount { get; set; }
}
