namespace MasterApi.Data.Entities;

public sealed class PasswordResetToken
{
    public Guid Id { get; set; }

    public Guid PlayerAccountId { get; set; }

    public PlayerAccount? PlayerAccount { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
