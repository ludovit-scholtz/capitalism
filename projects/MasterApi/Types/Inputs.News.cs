namespace MasterApi.Types;

public class MasterServerServiceInput
{
    public string RegistrationKey { get; set; } = string.Empty;

    public string ServerKey { get; set; } = string.Empty;
}

public sealed class GetGameAdministrationAccessInput : MasterServerServiceInput
{
    public string Email { get; set; } = string.Empty;
}

public sealed class GlobalGameAdminGrantInput : MasterServerServiceInput
{
    public string RequesterEmail { get; set; } = string.Empty;

    public string TargetEmail { get; set; } = string.Empty;
}

public sealed class GetGlobalGameAdminGrantsInput : MasterServerServiceInput
{
    public string RequesterEmail { get; set; } = string.Empty;
}

public sealed class GetGameNewsFeedInput : MasterServerServiceInput
{
    public string? PlayerEmail { get; set; }

    public int Limit { get; set; } = 50;

    public bool IncludeDrafts { get; set; }

    public string? RequesterEmail { get; set; }
}

public sealed class MarkGameNewsReadInput : MasterServerServiceInput
{
    public string PlayerEmail { get; set; } = string.Empty;

    public List<Guid> EntryIds { get; set; } = [];
}

public sealed class MarkAllGameNewsReadInput : MasterServerServiceInput
{
    public string PlayerEmail { get; set; } = string.Empty;
}

public sealed class UpsertGameNewsEntryInput : MasterServerServiceInput
{
    public Guid? EntryId { get; set; }

    public string? RequesterEmail { get; set; }

    public string EntryType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? TargetServerKey { get; set; }

    public List<GameNewsLocalizationInput> Localizations { get; set; } = [];
}

public sealed class GameNewsLocalizationInput
{
    public string Locale { get; set; } = "en";

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string HtmlContent { get; set; } = string.Empty;
}

public sealed class GameAdministrationAccessInfo
{
    public string Email { get; set; } = string.Empty;

    public bool IsRootAdministrator { get; set; }

    public bool HasGlobalAdminRole { get; set; }

    public bool CanAccessEveryGameDashboard { get; set; }
}

public sealed class GlobalGameAdminGrantInfo
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string GrantedByEmail { get; set; } = string.Empty;

    public DateTime GrantedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class GameNewsFeedResult
{
    public int UnreadCount { get; set; }

    public List<GameNewsEntryInfo> Items { get; set; } = [];
}

public sealed class GameNewsEntryInfo
{
    public Guid Id { get; set; }

    public string EntryType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? TargetServerKey { get; set; }

    public string CreatedByEmail { get; set; } = string.Empty;

    public string UpdatedByEmail { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public bool IsRead { get; set; }

    public List<GameNewsLocalizationInfo> Localizations { get; set; } = [];
}

public sealed class GameNewsLocalizationInfo
{
    public string Locale { get; set; } = "en";

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string HtmlContent { get; set; } = string.Empty;
}
