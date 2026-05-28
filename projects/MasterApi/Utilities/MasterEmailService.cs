using System.Net;
using System.Text;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterApi.Utilities;

public interface IMasterEmailService
{
    Task SendRegistrationEmailIfNeededAsync(PlayerAccount player, string? accessedUrl, string? locale, CancellationToken cancellationToken);
}

public sealed class MasterEmailService(
    MasterDbContext db,
    IEmailTemplateRenderer renderer,
    IEmailSender sender,
    ILogger<MasterEmailService> logger) : IMasterEmailService
{
    public async Task SendRegistrationEmailIfNeededAsync(
        PlayerAccount player,
        string? accessedUrl,
        string? locale,
        CancellationToken cancellationToken)
    {
        var normalizedLocale = EmailLocalizations.NormalizeLocale(locale);
        player.PreferredLocale = normalizedLocale;
        player.PreferredLocaleUpdatedAtUtc = DateTime.UtcNow;
        player.LastAccessedUrl = NormalizeUrl(accessedUrl);

        if (player.HasReceivedRegistrationEmail)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var copy = EmailLocalizations.Registration(normalizedLocale);
        var escapedIntro = WebUtility.HtmlEncode(copy.Intro);
        var escapedLabel = WebUtility.HtmlEncode(copy.SectionTitle);
        var escapedUrl = WebUtility.HtmlEncode(player.LastAccessedUrl ?? "-");
        var greeting = WebUtility.HtmlEncode(GetGreeting(normalizedLocale, player.DisplayName));
        var signoff = WebUtility.HtmlEncode(GetSignoff(normalizedLocale));
        var bodyHtml = $"""
            <p style="margin:0 0 18px;">{greeting}</p>
            <p style="margin:0 0 18px;">{escapedIntro}</p>
            <p style="margin:0 0 8px;font-weight:700;color:#162033;">{escapedLabel}</p>
            <p style="margin:0 0 22px;"><a href="{escapedUrl}" style="color:#0f766e;text-decoration:none;">{escapedUrl}</a></p>
            <p style="margin:0;">{signoff}</p>
            """.Trim();
        var html = await renderer.RenderAsync(
            new EmailTemplateModel(normalizedLocale, copy.Subject, copy.Headline, bodyHtml, copy.Footer),
            cancellationToken);
        var text = BuildRegistrationText(player, copy);

        bool sent;
        try
        {
            sent = await sender.SendAsync(
                new EmailMessageRequest(player.Email, player.DisplayName, copy.Subject, html, text),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Registration email send failed for {Email}.", player.Email);
            sent = false;
        }
        if (!sent)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        player.HasReceivedRegistrationEmail = true;
        player.RegistrationEmailSentAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeUrl(string? accessedUrl)
    {
        if (string.IsNullOrWhiteSpace(accessedUrl))
        {
            return null;
        }

        var trimmed = accessedUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private static string GetGreeting(string locale, string displayName) => locale switch
    {
        "sk" => $"Dobrý deň, {displayName},",
        "de" => $"Hallo {displayName},",
        _ => $"Hello {displayName},",
    };

    private static string GetSignoff(string locale) => locale switch
    {
        "sk" => "Veľa šťastia na trhu!",
        "de" => "Viel Erfolg am Markt!",
        _ => "Good luck in the market!",
    };

    private static string BuildRegistrationText(PlayerAccount player, EmailCopy copy)
    {
        var builder = new StringBuilder();
        var locale = EmailLocalizations.NormalizeLocale(player.PreferredLocale);
        builder.AppendLine(copy.Headline);
        builder.AppendLine();
        builder.AppendLine(GetGreeting(locale, player.DisplayName));
        builder.AppendLine(copy.Intro);
        builder.AppendLine();
        builder.AppendLine(copy.SectionTitle);
        builder.AppendLine(player.LastAccessedUrl ?? "-");
        builder.AppendLine(GetSignoff(locale));
        return builder.ToString();
    }
}
