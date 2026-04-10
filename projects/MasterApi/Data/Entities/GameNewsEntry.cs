namespace MasterApi.Data.Entities;

public sealed class GameNewsEntry
{
    public Guid Id { get; set; }

    public string EntryType { get; set; } = GameNewsEntryType.News;

    public string Status { get; set; } = GameNewsEntryStatus.Draft;

    public string? TargetServerKey { get; set; }

    public string CreatedByEmail { get; set; } = string.Empty;

    public string UpdatedByEmail { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? PublishedAtUtc { get; set; }

    public ICollection<GameNewsEntryLocalization> Localizations { get; set; } = [];

    public ICollection<GameNewsReadReceipt> ReadReceipts { get; set; } = [];
}

public sealed class GameNewsEntryLocalization
{
    public Guid Id { get; set; }

    public Guid GameNewsEntryId { get; set; }

    public GameNewsEntry GameNewsEntry { get; set; } = null!;

    public string Locale { get; set; } = "en";

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string HtmlContent { get; set; } = string.Empty;
}

public sealed class GameNewsReadReceipt
{
    public Guid Id { get; set; }

    public Guid GameNewsEntryId { get; set; }

    public GameNewsEntry GameNewsEntry { get; set; } = null!;

    public string PlayerEmail { get; set; } = string.Empty;

    public string ServerKey { get; set; } = string.Empty;

    public DateTime ReadAtUtc { get; set; }
}

public static class GameNewsEntryType
{
    public const string News = "NEWS";
    public const string Changelog = "CHANGELOG";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        News,
        Changelog,
    };
}

public static class GameNewsEntryStatus
{
    public const string Draft = "DRAFT";
    public const string Published = "PUBLISHED";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Draft,
        Published,
    };
}