namespace MasterApi.Types;

public sealed class BuildingLayoutTemplateInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BuildingType { get; set; } = string.Empty;
    public string UnitsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class SaveBuildingLayoutInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string BuildingType { get; set; } = string.Empty;
    public string UnitsJson { get; set; } = "[]";
    /// <summary>If provided, update the existing template instead of creating a new one.</summary>
    public Guid? ExistingId { get; set; }
}

public sealed class DeleteBuildingLayoutInput
{
    public Guid Id { get; set; }
}

// ── Gold token administration ──────────────────────────────────────────────

/// <summary>Player-facing view of their own gold token account.</summary>
public sealed class PlayerGoldAccountInfo
{
    /// <summary>Current gold token balance in grams.</summary>
    public decimal GoldTokenBalance { get; set; }

    /// <summary>UTC timestamp of the most recent transaction, or null if no transactions exist.</summary>
    public DateTime? LastUpdatedAtUtc { get; set; }

    /// <summary>The player's most recent gold token transactions (up to 10), newest first.</summary>
    public List<PlayerGoldTransactionInfo> RecentTransactions { get; set; } = [];
}

/// <summary>A single gold token transaction visible to the owning player.</summary>
public sealed class PlayerGoldTransactionInfo
{
    public Guid Id { get; set; }

    /// <summary>Positive = received, negative = deducted.</summary>
    public decimal Amount { get; set; }

    public decimal BalanceBefore { get; set; }

    public decimal BalanceAfter { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class AdjustGoldTokenInput
{
    /// <summary>Email of the target player whose balance will be adjusted.</summary>
    public string TargetEmail { get; set; } = string.Empty;

    /// <summary>Positive value to add, negative value to deduct.</summary>
    public decimal Amount { get; set; }

    /// <summary>Optional administrator note for the audit trail.</summary>
    public string? Note { get; set; }
}

public sealed class GoldTokenBalanceInfo
{
    public Guid PlayerId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public decimal GoldTokenBalance { get; set; }
}

public sealed class GoldTokenTransactionInfo
{
    public Guid Id { get; set; }

    public string PlayerEmail { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal BalanceBefore { get; set; }

    public decimal BalanceAfter { get; set; }

    public string AdminEmail { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CreateGoldTokenDepositRequestInput
{
    public string Network { get; set; } = "ALGORAND";

    public decimal Amount { get; set; }

    public string? SenderAddress { get; set; }
}

public sealed class CreateGoldTokenWithdrawalRequestInput
{
    public string Network { get; set; } = "ALGORAND";

    public decimal Amount { get; set; }

    public string DestinationAddress { get; set; } = string.Empty;
}

public sealed class ProcessGoldTokenDepositRequestInput
{
    public Guid RequestId { get; set; }

    public string? AdminNote { get; set; }
}

public sealed class ProcessGoldTokenWithdrawalRequestInput
{
    public Guid RequestId { get; set; }

    public string? AdminNote { get; set; }
}

public sealed class GoldTokenDepositRequestInfo
{
    public Guid Id { get; set; }

    public Guid PlayerAccountId { get; set; }

    public string PlayerEmail { get; set; } = string.Empty;

    public string Network { get; set; } = "ALGORAND";

    public long AssetId { get; set; }

    public string DepositAddress { get; set; } = string.Empty;

    public string? SenderAddress { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = "PENDING";

    public DateTime RequestedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public string? ProcessedByEmail { get; set; }

    public string? AdminNote { get; set; }

    /// <summary>
    /// The note text to include in the blockchain transaction note field for automatic matching.
    /// </summary>
    public string NoteText { get; set; } = string.Empty;
}

public sealed class GoldTokenWithdrawalRequestInfo
{
    public Guid Id { get; set; }

    public Guid PlayerAccountId { get; set; }

    public string PlayerEmail { get; set; } = string.Empty;

    public string Network { get; set; } = "ALGORAND";

    public long AssetId { get; set; }

    public string DestinationAddress { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Status { get; set; } = "PENDING";

    public DateTime RequestedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public string? ProcessedByEmail { get; set; }

    public string? AdminNote { get; set; }
}
