namespace Api.Data.Entities;

/// <summary>
/// Tracks generated weekly and monthly market reports for each city.
/// Prevents duplicate reports across server restarts.
/// </summary>
public sealed class CityMarketReport
{
    public Guid Id { get; set; }

    public Guid CityId { get; set; }

    public City City { get; set; } = null!;

    /// <summary>"WEEKLY" or "MONTHLY".</summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>First tick included in the aggregation window.</summary>
    public long TickFrom { get; set; }

    /// <summary>Last tick included in the aggregation window.</summary>
    public long TickTo { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID of the news entry published to MasterApi.
    /// Null if not yet published or master server is not configured.
    /// </summary>
    public Guid? MasterNewsEntryId { get; set; }

    /// <summary>Serialized JSON snapshot of the report data used to regenerate HTML on demand.</summary>
    public string ReportDataJson { get; set; } = "{}";
}

public static class MarketReportType
{
    public const string Weekly = "WEEKLY";
    public const string Monthly = "MONTHLY";
}
