using System.Net;
using System.Text;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

public interface IWeeklyEmailReportService
{
    Task<int> SendDueWeeklyReportsAsync(DateTime nowUtc, CancellationToken cancellationToken);
}

public sealed class WeeklyEmailReportService(
    MasterDbContext db,
    IOptions<EmailOptions> emailOptions,
    IEmailTemplateRenderer renderer,
    IEmailSender sender,
    ILogger<WeeklyEmailReportService> logger) : IWeeklyEmailReportService
{
    public async Task<int> SendDueWeeklyReportsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var options = emailOptions.Value;
        if (!options.Enabled || !options.WeeklyReportsEnabled)
        {
            return 0;
        }

        var weekStartUtc = nowUtc.AddDays(-7);
        var duePlayers = await db.PlayerAccounts
            .Where(player => player.HasReceivedRegistrationEmail)
            .Where(player => player.LastWeeklyEmailSentAtUtc == null || player.LastWeeklyEmailSentAtUtc < weekStartUtc)
            .OrderBy(player => player.Email)
            .ToListAsync(cancellationToken);

        var sentCount = 0;
        foreach (var player in duePlayers)
        {
            if (await SendWeeklyReportAsync(player, weekStartUtc, nowUtc, cancellationToken))
            {
                sentCount++;
            }
        }

        return sentCount;
    }

    private async Task<bool> SendWeeklyReportAsync(
        PlayerAccount player,
        DateTime weekStartUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var locale = EmailLocalizations.NormalizeLocale(player.PreferredLocale);
        var copy = EmailLocalizations.WeeklyReport(locale);
        var servers = await BuildServerRowsAsync(player, locale, weekStartUtc, nowUtc, cancellationToken);
        var bountyPoints = await db.MasterRankingRewardRecords
            .AsNoTracking()
            .Where(record => record.PlayerAccountId == player.Id)
            .Where(record => record.Status == RankingRewardStatus.Awarded)
            .Where(record => record.AwardedAtUtc >= weekStartUtc && record.AwardedAtUtc <= nowUtc)
            .SumAsync(record => record.PointsAwarded, cancellationToken);
        var changelogRows = await BuildChangelogRowsAsync(locale, weekStartUtc, nowUtc, cancellationToken);

        var bodyHtml = BuildWeeklyBodyHtml(locale, copy, servers, bountyPoints, changelogRows);
        var html = await renderer.RenderAsync(
            new EmailTemplateModel(locale, copy.Subject, copy.Headline, bodyHtml, copy.Footer),
            cancellationToken);
        var text = BuildWeeklyText(locale, copy, servers, bountyPoints, changelogRows);
        var sent = await sender.SendAsync(
            new EmailMessageRequest(player.Email, player.DisplayName, copy.Subject, html, text),
            cancellationToken);
        if (!sent)
        {
            logger.LogInformation("Weekly report email was skipped for {Email}.", player.Email);
            return false;
        }

        player.LastWeeklyEmailSentAtUtc = nowUtc;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<List<WeeklyServerRow>> BuildServerRowsAsync(
        PlayerAccount player,
        string locale,
        DateTime weekStartUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var activeServers = await db.GameServers
            .AsNoTracking()
            .Where(server => server.IsActive && server.ExpiresAtUtc > nowUtc)
            .OrderBy(server => server.DisplayName)
            .ToListAsync(cancellationToken);
        var rewards = await db.MasterRankingRewardRecords
            .AsNoTracking()
            .Where(record => record.PlayerAccountId == player.Id)
            .Where(record => record.Status == RankingRewardStatus.Awarded)
            .Where(record => record.AwardedAtUtc >= weekStartUtc && record.AwardedAtUtc <= nowUtc)
            .Where(record => record.ServerKey != null)
            .GroupBy(record => record.ServerKey!)
            .Select(group => new { ServerKey = group.Key, Points = group.Sum(record => record.PointsAwarded) })
            .ToDictionaryAsync(item => item.ServerKey, item => item.Points, StringComparer.Ordinal, cancellationToken);
        var snapshot = await db.MasterRankingPlayerSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.PlayerAccountId == player.Id, cancellationToken);

        return activeServers.Select(server => new WeeklyServerRow(
                server.DisplayName,
                server.FrontendUrl,
                EmailLocalizations.WeeklyProfitUnavailable(locale),
                rewards.GetValueOrDefault(server.ServerKey),
                snapshot?.GlobalRank ?? 0))
            .ToList();
    }

    private async Task<List<WeeklyChangelogRow>> BuildChangelogRowsAsync(
        string locale,
        DateTime weekStartUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var entries = await db.GameNewsEntries
            .AsNoTracking()
            .Include(entry => entry.Localizations)
            .Where(entry => entry.EntryType == "CHANGELOG" && entry.Status == "PUBLISHED")
            .Where(entry => entry.PublishedAtUtc >= weekStartUtc && entry.PublishedAtUtc <= nowUtc)
            .OrderByDescending(entry => entry.PublishedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        return entries.Select(entry =>
            {
                var localization = entry.Localizations.FirstOrDefault(item => item.Locale == locale)
                    ?? entry.Localizations.FirstOrDefault(item => item.Locale == "en")
                    ?? entry.Localizations.FirstOrDefault();
                return new WeeklyChangelogRow(
                    localization?.Title ?? EmailLocalizations.WeeklyChangelogFallbackTitle(locale),
                    localization?.Summary ?? string.Empty);
            })
            .ToList();
    }

    private static string BuildWeeklyBodyHtml(
        string locale,
        EmailCopy copy,
        List<WeeklyServerRow> servers,
        decimal bountyPoints,
        List<WeeklyChangelogRow> changelogRows)
    {
        var builder = new StringBuilder();
        builder.Append($"<p style=\"margin:0 0 18px;\">{WebUtility.HtmlEncode(copy.Intro)}</p>");
        builder.Append($"<h2 style=\"font-size:18px;margin:24px 0 12px;color:#162033;\">{WebUtility.HtmlEncode(copy.SectionTitle)}</h2>");
        builder.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin-bottom:22px;\">");
        foreach (var server in servers)
        {
            builder.Append("<tr>");
            builder.Append($"<td style=\"padding:10px;border-bottom:1px solid #e5edf3;\"><a href=\"{WebUtility.HtmlEncode(server.FrontendUrl)}\" style=\"color:#0f766e;text-decoration:none;font-weight:700;\">{WebUtility.HtmlEncode(server.DisplayName)}</a></td>");
            builder.Append($"<td style=\"padding:10px;border-bottom:1px solid #e5edf3;\">{WebUtility.HtmlEncode(EmailLocalizations.WeeklyProfitLabel(locale))}: {WebUtility.HtmlEncode(server.Profit)}</td>");
            builder.Append($"<td style=\"padding:10px;border-bottom:1px solid #e5edf3;\">{WebUtility.HtmlEncode(EmailLocalizations.WeeklyRankLabel(locale))}: {(server.Rank == 0 ? "-" : server.Rank)}</td>");
            builder.Append($"<td style=\"padding:10px;border-bottom:1px solid #e5edf3;\">{WebUtility.HtmlEncode(EmailLocalizations.WeeklyBountiesLabel(locale))}: {server.BountyPoints:N0}</td>");
            builder.Append("</tr>");
        }
        if (servers.Count == 0)
        {
            builder.Append($"<tr><td style=\"padding:10px;border-bottom:1px solid #e5edf3;\">{WebUtility.HtmlEncode(EmailLocalizations.WeeklyNoActiveServers(locale))}</td></tr>");
        }
        builder.Append("</table>");
        builder.Append($"<p style=\"margin:0 0 18px;\"><strong>{WebUtility.HtmlEncode(EmailLocalizations.WeeklyMasterBountyLabel(locale))}:</strong> {bountyPoints:N0}</p>");
        if (changelogRows.Count > 0)
        {
            builder.Append("<h2 style=\"font-size:18px;margin:24px 0 12px;color:#162033;\">Changelog</h2><ul style=\"padding-left:22px;margin:0;\">");
            foreach (var row in changelogRows)
            {
                builder.Append($"<li><strong>{WebUtility.HtmlEncode(row.Title)}</strong> — {WebUtility.HtmlEncode(row.Summary)}</li>");
            }
            builder.Append("</ul>");
        }
        return builder.ToString();
    }

    private static string BuildWeeklyText(
        string locale,
        EmailCopy copy,
        List<WeeklyServerRow> servers,
        decimal bountyPoints,
        List<WeeklyChangelogRow> changelogRows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(copy.Headline);
        builder.AppendLine(copy.Intro);
        builder.AppendLine(copy.SectionTitle);
        foreach (var server in servers)
        {
            builder.AppendLine($"- {server.DisplayName}: {EmailLocalizations.WeeklyProfitLabel(locale)} {server.Profit}, {EmailLocalizations.WeeklyRankLabel(locale)} {(server.Rank == 0 ? "-" : server.Rank)}, {EmailLocalizations.WeeklyBountiesLabel(locale)} {server.BountyPoints:N0}");
        }
        builder.AppendLine($"{EmailLocalizations.WeeklyMasterBountyLabel(locale)}: {bountyPoints:N0}");
        foreach (var row in changelogRows)
        {
            builder.AppendLine($"- {row.Title}: {row.Summary}");
        }
        return builder.ToString();
    }

    private sealed record WeeklyServerRow(string DisplayName, string FrontendUrl, string Profit, decimal BountyPoints, int Rank);

    private sealed record WeeklyChangelogRow(string Title, string Summary);
}
