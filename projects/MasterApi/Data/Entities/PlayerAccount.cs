namespace MasterApi.Data.Entities;

public sealed class PlayerAccount
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public DateTime? StartupPackClaimedAtUtc { get; set; }

    /// <summary>Current gold token balance (grams of gold). Cannot go negative.</summary>
    public decimal GoldTokenBalance { get; set; } = 0m;

    public ICollection<ProSubscription> Subscriptions { get; set; } = [];

    public ICollection<GoldTokenTransaction> GoldTokenTransactions { get; set; } = [];
}
