namespace MasterApi.Data.Entities;

public sealed class GlobalGameAdminGrant
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string GrantedByEmail { get; set; } = string.Empty;

    public DateTime GrantedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}