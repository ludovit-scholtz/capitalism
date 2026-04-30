namespace MasterApi.Data.Entities;

public sealed class SupportTicket
{
    public Guid Id { get; set; }

    public Guid CreatedByPlayerAccountId { get; set; }

    public PlayerAccount CreatedByPlayerAccount { get; set; } = null!;

    public string CreatedByEmail { get; set; } = string.Empty;

    public string CreatedByDisplayName { get; set; } = string.Empty;

    public string TicketType { get; set; } = SupportTicketType.Other;

    public string Status { get; set; } = SupportTicketStatus.Submitted;

    public string Title { get; set; } = string.Empty;

    public string MarkdownSource { get; set; } = string.Empty;

    public string? SanitizedPreviewHtml { get; set; }

    public string ExtractedUrlsJson { get; set; } = "[]";

    public string ExtractedImagesJson { get; set; } = "[]";

    public bool ContainsUnsafeContent { get; set; }

    public string ModerationState { get; set; } = SupportTicketModerationState.Pending;

    public string? ModerationReason { get; set; }

    public string? ModeratedByEmail { get; set; }

    public DateTime? ModeratedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime StatusUpdatedAtUtc { get; set; }

    public ICollection<SupportTicketAuditEvent> AuditEvents { get; set; } = [];
}

public sealed class SupportTicketAuditEvent
{
    public Guid Id { get; set; }

    public Guid SupportTicketId { get; set; }

    public SupportTicket SupportTicket { get; set; } = null!;

    public string EventType { get; set; } = string.Empty;

    public string ActorEmail { get; set; } = string.Empty;

    public string ActorDisplayName { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; }
}

public static class SupportTicketType
{
    public const string Suggestion = "SUGGESTION";
    public const string Bug = "BUG";
    public const string Other = "OTHER";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Suggestion,
        Bug,
        Other,
    };
}

public static class SupportTicketStatus
{
    public const string Submitted = "SUBMITTED";
    public const string InProgress = "IN_PROGRESS";
    public const string Finished = "FINISHED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Submitted,
        InProgress,
        Finished,
    };

    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [Submitted] = new HashSet<string>(StringComparer.Ordinal)
            {
                InProgress,
                Finished,
            },
            [InProgress] = new HashSet<string>(StringComparer.Ordinal)
            {
                Finished,
            },
            [Finished] = new HashSet<string>(StringComparer.Ordinal),
        };
}

public static class SupportTicketModerationState
{
    public const string Pending = "PENDING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Pending,
        Approved,
        Rejected,
    };
}
