namespace MasterApi.Types;

public sealed class SupportTicketInfo
{
    public Guid Id { get; set; }

    public string TicketType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string MarkdownSource { get; set; } = string.Empty;

    public string? SanitizedPreviewHtml { get; set; }

    public bool ContainsUnsafeContent { get; set; }

    public string ModerationState { get; set; } = string.Empty;

    public string? ModerationReason { get; set; }

    public string? ModeratedByEmail { get; set; }

    public DateTime? ModeratedAtUtc { get; set; }

    public string CreatedByEmail { get; set; } = string.Empty;

    public string CreatedByDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime StatusUpdatedAtUtc { get; set; }

    public List<string> ExtractedUrls { get; set; } = [];

    public List<string> ExtractedImages { get; set; } = [];

    public List<SupportTicketAuditEventInfo> Activity { get; set; } = [];
}

public sealed class SupportTicketAuditEventInfo
{
    public Guid Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string ActorEmail { get; set; } = string.Empty;

    public string ActorDisplayName { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CreateSupportTicketInput
{
    public string TicketType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string MarkdownSource { get; set; } = string.Empty;
}

public sealed class UpdateSupportTicketContentInput
{
    public Guid TicketId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string MarkdownSource { get; set; } = string.Empty;
}

public sealed class UpdateSupportTicketStatusInput
{
    public Guid TicketId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Note { get; set; }
}

public sealed class ModerateSupportTicketInput
{
    public Guid TicketId { get; set; }

    public bool Approve { get; set; }

    public string? Note { get; set; }
}

public sealed class ListSupportTicketsInput
{
    public string? TicketType { get; set; }

    public string? Status { get; set; }

    public string? SearchTitle { get; set; }

    public DateTime? CreatedFromUtc { get; set; }

    public DateTime? CreatedToUtc { get; set; }

    public string SortBy { get; set; } = "CREATED_AT";

    public string SortDirection { get; set; } = "DESC";

    public int Limit { get; set; } = 50;

    public int Offset { get; set; }

    public bool? UnsafeOnly { get; set; }
}
