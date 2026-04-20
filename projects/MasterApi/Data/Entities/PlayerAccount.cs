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

    /// <summary>
    /// Optimistic concurrency token refreshed on every gold balance write.
    /// EF Core uses this to detect concurrent modifications and throw
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
    /// when two adjustments race against the same balance snapshot.
    /// </summary>
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public ICollection<ProSubscription> Subscriptions { get; set; } = [];

    public ICollection<GoldTokenTransaction> GoldTokenTransactions { get; set; } = [];
}
