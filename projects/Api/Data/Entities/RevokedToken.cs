namespace Api.Data.Entities;

/// <summary>
/// Revoked JWT token identifiers that must be rejected until token expiry.
/// </summary>
public sealed class RevokedToken
{
    public string Jti { get; set; } = string.Empty;
    public Guid PlayerId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime RevokedAtUtc { get; set; }
}
