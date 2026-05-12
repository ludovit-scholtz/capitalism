namespace MasterApi.Data.Entities;

/// <summary>
/// Revoked JWT token IDs retained until their natural expiration.
/// </summary>
public sealed class MasterRevokedToken
{
    public string Jti { get; set; } = string.Empty;
    public Guid PlayerAccountId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime RevokedAtUtc { get; set; }
}
