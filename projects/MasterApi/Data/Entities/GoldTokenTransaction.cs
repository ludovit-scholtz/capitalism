namespace MasterApi.Data.Entities;

/// <summary>Audit record for every gold token balance adjustment made by an administrator.</summary>
public sealed class GoldTokenTransaction
{
    public Guid Id { get; set; }

    public Guid PlayerAccountId { get; set; }

    public string PlayerEmail { get; set; } = string.Empty;

    /// <summary>Positive = top-up, negative = deduction.</summary>
    public decimal Amount { get; set; }

    public decimal BalanceBefore { get; set; }

    public decimal BalanceAfter { get; set; }

    public string AdminEmail { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public PlayerAccount PlayerAccount { get; set; } = null!;
}
