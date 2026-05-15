using System.ComponentModel.DataAnnotations;
using Api.Data.Entities;

namespace Api.Types;

public sealed class StartAdminImpersonationInput
{
    public Guid TargetPlayerId { get; set; }

    [Required, MaxLength(20)]
    public string AccountType { get; set; } = AccountContextType.Person;

    public Guid? CompanyId { get; set; }
}

public sealed class SetPlayerInvisibleInChatInput
{
    public Guid PlayerId { get; set; }

    public bool IsInvisibleInChat { get; set; }
}

public sealed class SetLocalGameAdminRoleInput
{
    public Guid PlayerId { get; set; }

    public bool IsAdmin { get; set; }
}

public sealed class ManageGlobalGameAdminRoleInput
{
    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;
}

public sealed class GameNewsLocalizationInput
{
    [Required, MaxLength(10)]
    public string Locale { get; set; } = "en";

    [Required, MaxLength(220)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Summary { get; set; } = string.Empty;

    [Required]
    public string HtmlContent { get; set; } = string.Empty;
}

public sealed class UpsertGameNewsEntryInput
{
    public Guid? EntryId { get; set; }

    [Required, MaxLength(20)]
    public string EntryType { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = string.Empty;

    [Required]
    public List<GameNewsLocalizationInput> Localizations { get; set; } = [];
}

public sealed class MarkGameNewsReadInput
{
    [Required]
    public List<Guid> EntryIds { get; set; } = [];
}

public sealed class UpdateRealWorldBillionaireInput
{
    [Required]
    public Guid Id { get; set; }

    [Range(1, 10)]
    public int Rank { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [Range(1, double.MaxValue)]
    public decimal WealthUsd { get; set; }
}

/// <summary>Input for admin setting a player's gold balance.</summary>
public sealed class AdminSetPlayerGoldBalanceInput
{
    [Required]
    public string PlayerEmail { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    [Required]
    public string Note { get; set; } = string.Empty;
}

/// <summary>Input for manually ending a game shard (admin override).</summary>
public sealed class EndShardManuallyInput
{
    /// <summary>Optional reason for the manual end (shown in moderation logs).</summary>
    [MaxLength(500)]
    public string? Reason { get; set; }
}

/// <summary>Input for forcing shard conclusion with a victory newsletter.</summary>
public sealed class ForceShardConclusionInput
{
    /// <summary>Required reason for the forced conclusion (shown in news feed and logs).</summary>
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
