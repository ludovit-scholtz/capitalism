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

/// <summary>Input for admin setting a player's gold balance.</summary>
public sealed class AdminSetPlayerGoldBalanceInput
{
    [Required]
    public string PlayerEmail { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    [Required]
    public string Note { get; set; } = string.Empty;
}
