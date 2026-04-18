using System.Globalization;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterApi.Data;

/// <summary>
/// Represents one parsed row from CHANGELOG.csv (id;date;en;sk;de).
/// </summary>
public sealed class ChangelogCsvRow
{
    public required Guid Id { get; init; }
    public required DateTime Date { get; init; }
    public required string En { get; init; }
    public required string Sk { get; init; }
    public required string De { get; init; }
}

/// <summary>
/// Parses CHANGELOG.csv and imports entries into the database without creating duplicates.
/// </summary>
public sealed class ChangelogCsvImporter(MasterDbContext db)
{
    // Must stay within GameNewsEntryLocalization column length constraints
    private const int MaxTitleLength = 220;
    private const int MaxSummaryLength = 1000;

    /// <summary>
    /// Parses CHANGELOG.csv content into structured rows.
    /// Format: id;date;en;sk;de (semicolons within field text are permitted because only the
    /// first four semicolons on each line are used as field delimiters).
    /// Malformed rows (non-parseable GUID or date, empty English text) are silently skipped
    /// so that a single bad row does not abort the entire import.
    /// </summary>
    public static IReadOnlyList<ChangelogCsvRow> ParseCsv(string csvContent)
    {
        var rows = new List<ChangelogCsvRow>();

        var lines = csvContent
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Line 0 is the header (id;date;en;sk;de) – skip it.
        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrEmpty(line)) continue;

            // Split on the first four semicolons only; remaining semicolons belong to field text.
            var parts = line.Split(';', 5);
            if (parts.Length < 3) continue;

            if (!Guid.TryParse(parts[0].Trim(), out var id)) continue;

            if (!DateTime.TryParse(
                    parts[1].Trim(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var date))
            {
                continue;
            }

            var en = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(en)) continue;

            var sk = parts.Length > 3 ? parts[3].Trim() : string.Empty;
            var de = parts.Length > 4 ? parts[4].Trim() : string.Empty;

            rows.Add(new ChangelogCsvRow
            {
                Id = id,
                Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
                En = en,
                Sk = sk,
                De = de,
            });
        }

        return rows;
    }

    /// <summary>
    /// Imports parsed rows into the database.  Rows whose ID already exists are skipped
    /// so that calling this method on every server restart is fully idempotent.
    /// </summary>
    /// <returns>The number of newly inserted entries.</returns>
    public async Task<int> ImportAsync(IReadOnlyList<ChangelogCsvRow> rows, CancellationToken ct = default)
    {
        int imported = 0;

        foreach (var row in rows)
        {
            if (await db.GameNewsEntries.AnyAsync(e => e.Id == row.Id, ct))
            {
                continue;
            }

            db.GameNewsEntries.Add(CreateEntry(row));
            await db.SaveChangesAsync(ct);
            imported++;
        }

        return imported;
    }

    private static GameNewsEntry CreateEntry(ChangelogCsvRow row)
    {
        return new GameNewsEntry
        {
            Id = row.Id,
            EntryType = GameNewsEntryType.Changelog,
            Status = GameNewsEntryStatus.Published,
            TargetServerKey = null,
            CreatedByEmail = "system@capitalism.local",
            UpdatedByEmail = "system@capitalism.local",
            CreatedAtUtc = row.Date,
            UpdatedAtUtc = row.Date,
            PublishedAtUtc = row.Date,
            Localizations = CreateLocalizations(row),
        };
    }

    private static List<GameNewsEntryLocalization> CreateLocalizations(ChangelogCsvRow row)
    {
        return
        [
            CreateLocalization("en", row.En),
            CreateLocalization("sk", string.IsNullOrWhiteSpace(row.Sk) ? row.En : row.Sk),
            CreateLocalization("de", string.IsNullOrWhiteSpace(row.De) ? row.En : row.De),
        ];
    }

    private static GameNewsEntryLocalization CreateLocalization(string locale, string text)
    {
        return new GameNewsEntryLocalization
        {
            Id = Guid.NewGuid(),
            Locale = locale,
            Title = TruncateAtWordBoundary(text, MaxTitleLength),
            Summary = TruncateAtWordBoundary(text, MaxSummaryLength),
            HtmlContent = $"<p>{System.Web.HttpUtility.HtmlEncode(text)}</p>",
        };
    }

    /// <summary>Truncates <paramref name="text"/> to at most <paramref name="maxLength"/> characters,
    /// breaking at the last space before the limit to avoid cutting mid-word.</summary>
    internal static string TruncateAtWordBoundary(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;

        var lastSpace = text.LastIndexOf(' ', maxLength);
        var cutAt = lastSpace > maxLength / 2 ? lastSpace : maxLength;
        return text[..cutAt] + "…";
    }
}
