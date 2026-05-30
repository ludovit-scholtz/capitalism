using System.Net;
using System.Text;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

public interface IMasterEmailService
{
    Task SendRegistrationEmailIfNeededAsync(PlayerAccount player, string? accessedUrl, string? locale, CancellationToken cancellationToken);
    Task<bool> SendAdminTestEmailAsync(
        string recipientEmail,
        string recipientDisplayName,
        string locale,
        string message,
        string adminEmail,
        CancellationToken cancellationToken);
    Task<bool> SendSupportTicketCreatedEmailAsync(PlayerAccount owner, SupportTicket ticket, CancellationToken cancellationToken);
    Task<bool> SendSupportTicketUpdatedEmailAsync(
        PlayerAccount owner,
        SupportTicket ticket,
        string changeNote,
        CancellationToken cancellationToken);
    Task<bool> SendAccountDeletionRequestedEmailAsync(
        PlayerAccount player,
        DateTime scheduledAtUtc,
        CancellationToken cancellationToken);
    Task<bool> SendAccountDeletionCompletedEmailAsync(
        string recipientEmail,
        string recipientDisplayName,
        string locale,
        CancellationToken cancellationToken);
}

public sealed class MasterEmailService(
    MasterDbContext db,
    IEmailTemplateRenderer renderer,
    IEmailSender sender,
    IOptions<MasterApi.Configuration.EmailOptions> emailOptions,
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
        var legalNote = WebUtility.HtmlEncode(EmailLocalizations.RegistrationLegalNote(normalizedLocale));
        var bodyHtml = $"""
            <p style="margin:0 0 18px;">{greeting}</p>
            <p style="margin:0 0 18px;">{escapedIntro}</p>
            <p style="margin:0 0 8px;font-weight:700;color:#162033;">{escapedLabel}</p>
            <p style="margin:0 0 22px;"><a href="{escapedUrl}" style="color:#0f766e;text-decoration:none;">{escapedUrl}</a></p>
            <p style="margin:0 0 18px;color:#526070;">{legalNote}</p>
            <p style="margin:0;">{signoff}</p>
            """.Trim();
        var html = await renderer.RenderAsync(
            new EmailTemplateModel(normalizedLocale, copy.Subject, copy.Headline, bodyHtml, copy.Footer),
            cancellationToken);
        var text = BuildRegistrationText(player, copy);

        var attachments = BuildLegalAttachments(normalizedLocale);

        bool sent;
        try
        {
            sent = await sender.SendAsync(
                new EmailMessageRequest(player.Email, player.DisplayName, copy.Subject, html, text, attachments),
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

    public async Task<bool> SendAdminTestEmailAsync(
        string recipientEmail,
        string recipientDisplayName,
        string locale,
        string message,
        string adminEmail,
        CancellationToken cancellationToken)
    {
        var normalizedLocale = EmailLocalizations.NormalizeLocale(locale);
        var copy = EmailLocalizations.AdminTest(normalizedLocale);
        var adminLine = normalizedLocale switch
        {
            "sk" => $"Odoslal administrátor: {adminEmail}",
            "de" => $"Gesendet von Administrator: {adminEmail}",
            _ => $"Sent by administrator: {adminEmail}",
        };
        var bodyHtml = $"""
            <p style="margin:0 0 18px;">{WebUtility.HtmlEncode(copy.Intro)}</p>
            <p style="margin:0 0 8px;font-weight:700;color:#162033;">{WebUtility.HtmlEncode(copy.SectionTitle)}</p>
            <p style="margin:0 0 18px;white-space:pre-wrap;">{WebUtility.HtmlEncode(message)}</p>
            <p style="margin:0;color:#526070;">{WebUtility.HtmlEncode(adminLine)}</p>
            """.Trim();
        var text = $"{copy.Headline}{Environment.NewLine}{Environment.NewLine}{copy.Intro}{Environment.NewLine}{Environment.NewLine}{copy.SectionTitle}{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}{adminLine}";

        return await SendRenderedEmailAsync(
            recipientEmail,
            recipientDisplayName,
            normalizedLocale,
            copy,
            bodyHtml,
            text,
            cancellationToken);
    }

    public async Task<bool> SendSupportTicketCreatedEmailAsync(
        PlayerAccount owner,
        SupportTicket ticket,
        CancellationToken cancellationToken)
    {
        var locale = EmailLocalizations.NormalizeLocale(owner.PreferredLocale);
        var copy = EmailLocalizations.SupportTicketCreated(locale);
        return await SendSupportTicketEmailAsync(owner, ticket, copy, locale, null, cancellationToken);
    }

    public async Task<bool> SendSupportTicketUpdatedEmailAsync(
        PlayerAccount owner,
        SupportTicket ticket,
        string changeNote,
        CancellationToken cancellationToken)
    {
        var locale = EmailLocalizations.NormalizeLocale(owner.PreferredLocale);
        var copy = EmailLocalizations.SupportTicketUpdated(locale);
        return await SendSupportTicketEmailAsync(owner, ticket, copy, locale, changeNote, cancellationToken);
    }

    public async Task<bool> SendAccountDeletionRequestedEmailAsync(
        PlayerAccount player,
        DateTime scheduledAtUtc,
        CancellationToken cancellationToken)
    {
        var locale = EmailLocalizations.NormalizeLocale(player.PreferredLocale);
        var copy = EmailLocalizations.AccountDeletionRequested(locale);
        var portalUrl = NormalizePortalUrl(emailOptions.Value.PortalBaseUrl);
        var scheduledText = scheduledAtUtc.ToString("yyyy-MM-dd HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        var lossItems = EmailLocalizations.AccountDeletionLossItems(locale);
        var scheduledNote = EmailLocalizations.AccountDeletionScheduledNote(locale, scheduledText);
        var cancelNote = EmailLocalizations.AccountDeletionCancelNote(locale, portalUrl);
        var greeting = WebUtility.HtmlEncode(GetGreeting(locale, player.DisplayName));
        var signoff = WebUtility.HtmlEncode(GetSignoff(locale));

        var lossListHtml = new StringBuilder();
        foreach (var item in lossItems)
        {
            lossListHtml.Append($"<li style=\"margin:0 0 6px;\">{WebUtility.HtmlEncode(item)}</li>");
        }

        var bodyHtml = $"""
            <p style="margin:0 0 18px;">{greeting}</p>
            <p style="margin:0 0 18px;">{WebUtility.HtmlEncode(copy.Intro)}</p>
            <p style="margin:0 0 8px;font-weight:700;color:#162033;">{WebUtility.HtmlEncode(copy.SectionTitle)}</p>
            <ul style="margin:0 0 18px;padding-left:20px;">{lossListHtml}</ul>
            <p style="margin:0 0 18px;">{WebUtility.HtmlEncode(scheduledNote)}</p>
            <p style="margin:0 0 18px;">{WebUtility.HtmlEncode(cancelNote)}</p>
            <p style="margin:0 0 22px;"><a href="{WebUtility.HtmlEncode(portalUrl)}" style="color:#0f766e;text-decoration:none;">{WebUtility.HtmlEncode(portalUrl)}</a></p>
            <p style="margin:0;">{signoff}</p>
            """.Trim();

        var text = new StringBuilder();
        text.AppendLine(copy.Headline);
        text.AppendLine();
        text.AppendLine(GetGreeting(locale, player.DisplayName));
        text.AppendLine(copy.Intro);
        text.AppendLine();
        text.AppendLine(copy.SectionTitle);
        foreach (var item in lossItems)
        {
            text.AppendLine($"- {item}");
        }

        text.AppendLine();
        text.AppendLine(scheduledNote);
        text.AppendLine(cancelNote);
        text.AppendLine(portalUrl);
        text.AppendLine();
        text.AppendLine(GetSignoff(locale));

        return await SendRenderedEmailAsync(
            player.Email,
            player.DisplayName,
            locale,
            copy,
            bodyHtml,
            text.ToString(),
            cancellationToken);
    }

    public async Task<bool> SendAccountDeletionCompletedEmailAsync(
        string recipientEmail,
        string recipientDisplayName,
        string locale,
        CancellationToken cancellationToken)
    {
        var normalizedLocale = EmailLocalizations.NormalizeLocale(locale);
        var copy = EmailLocalizations.AccountDeletionCompleted(normalizedLocale);
        var portalUrl = NormalizePortalUrl(emailOptions.Value.PortalBaseUrl);
        var portalNote = EmailLocalizations.AccountDeletionPortalNote(normalizedLocale, portalUrl);
        var greeting = WebUtility.HtmlEncode(GetGreeting(normalizedLocale, recipientDisplayName));
        var signoff = WebUtility.HtmlEncode(GetSignoff(normalizedLocale));

        var bodyHtml = $"""
            <p style="margin:0 0 18px;">{greeting}</p>
            <p style="margin:0 0 18px;">{WebUtility.HtmlEncode(copy.Intro)}</p>
            <p style="margin:0 0 8px;font-weight:700;color:#162033;">{WebUtility.HtmlEncode(copy.SectionTitle)}</p>
            <p style="margin:0 0 18px;">{WebUtility.HtmlEncode(portalNote)}</p>
            <p style="margin:0 0 22px;"><a href="{WebUtility.HtmlEncode(portalUrl)}" style="color:#0f766e;text-decoration:none;">{WebUtility.HtmlEncode(portalUrl)}</a></p>
            <p style="margin:0;">{signoff}</p>
            """.Trim();

        var text = new StringBuilder();
        text.AppendLine(copy.Headline);
        text.AppendLine();
        text.AppendLine(GetGreeting(normalizedLocale, recipientDisplayName));
        text.AppendLine(copy.Intro);
        text.AppendLine();
        text.AppendLine(copy.SectionTitle);
        text.AppendLine(portalNote);
        text.AppendLine(portalUrl);
        text.AppendLine();
        text.AppendLine(GetSignoff(normalizedLocale));

        return await SendRenderedEmailAsync(
            recipientEmail,
            recipientDisplayName,
            normalizedLocale,
            copy,
            bodyHtml,
            text.ToString(),
            cancellationToken);
    }

    private static string NormalizePortalUrl(string? portalBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(portalBaseUrl))
        {
            return "https://capitalism.de-4.biatec.io";
        }

        var trimmed = portalBaseUrl.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return "https://capitalism.de-4.biatec.io";
        }

        return trimmed;
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

    private async Task<bool> SendSupportTicketEmailAsync(
        PlayerAccount owner,
        SupportTicket ticket,
        EmailCopy copy,
        string locale,
        string? changeNote,
        CancellationToken cancellationToken)
    {
        var titleLabel = EmailLocalizations.SupportTicketTitleLabel(locale);
        var typeLabel = EmailLocalizations.SupportTicketTypeLabel(locale);
        var statusLabel = EmailLocalizations.SupportTicketStatusLabel(locale);
        var changeHtml = string.IsNullOrWhiteSpace(changeNote)
            ? string.Empty
            : $"""<p style="margin:0 0 8px;font-weight:700;color:#162033;">{WebUtility.HtmlEncode(EmailLocalizations.SupportTicketChangeLabel(locale))}</p><p style="margin:0 0 18px;">{WebUtility.HtmlEncode(changeNote)}</p>""";
        var bodyHtml = $"""
            <p style="margin:0 0 18px;">{WebUtility.HtmlEncode(copy.Intro)}</p>
            <dl style="margin:0 0 18px;">
              <dt style="font-weight:700;color:#162033;">{WebUtility.HtmlEncode(titleLabel)}</dt>
              <dd style="margin:0 0 8px;">{WebUtility.HtmlEncode(ticket.Title)}</dd>
              <dt style="font-weight:700;color:#162033;">{WebUtility.HtmlEncode(typeLabel)}</dt>
              <dd style="margin:0 0 8px;">{WebUtility.HtmlEncode(ticket.TicketType)}</dd>
              <dt style="font-weight:700;color:#162033;">{WebUtility.HtmlEncode(statusLabel)}</dt>
              <dd style="margin:0;">{WebUtility.HtmlEncode(ticket.Status)}</dd>
            </dl>
            {changeHtml}
            <p style="margin:0 0 8px;font-weight:700;color:#162033;">{WebUtility.HtmlEncode(copy.SectionTitle)}</p>
            <p style="margin:0;white-space:pre-wrap;">{WebUtility.HtmlEncode(ticket.MarkdownSource)}</p>
            """.Trim();
        var text = BuildSupportTicketText(ticket, copy, locale, changeNote);

        return await SendRenderedEmailAsync(
            owner.Email,
            owner.DisplayName,
            locale,
            copy,
            bodyHtml,
            text,
            cancellationToken);
    }

    private async Task<bool> SendRenderedEmailAsync(
        string recipientEmail,
        string recipientDisplayName,
        string locale,
        EmailCopy copy,
        string bodyHtml,
        string text,
        CancellationToken cancellationToken)
    {
        try
        {
            var html = await renderer.RenderAsync(
                new EmailTemplateModel(locale, copy.Subject, copy.Headline, bodyHtml, copy.Footer),
                cancellationToken);

            return await sender.SendAsync(
                new EmailMessageRequest(recipientEmail, recipientDisplayName, copy.Subject, html, text),
                cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Email send failed for {Email}.", recipientEmail);
            return false;
        }
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

    private static IReadOnlyList<EmailAttachmentContent> BuildLegalAttachments(string locale)
    {
        var attachments = new List<EmailAttachmentContent>();
        foreach (var document in LegalDocuments.All(locale))
        {
            attachments.Add(new EmailAttachmentContent(
                LegalDocuments.FileName(document.Kind, locale),
                "application/pdf",
                LegalPdfGenerator.Generate(document)));
        }

        return attachments;
    }

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
        builder.AppendLine();
        builder.AppendLine(EmailLocalizations.RegistrationLegalNote(locale));
        builder.AppendLine(GetSignoff(locale));
        return builder.ToString();
    }

    private static string BuildSupportTicketText(
        SupportTicket ticket,
        EmailCopy copy,
        string locale,
        string? changeNote)
    {
        var builder = new StringBuilder();
        builder.AppendLine(copy.Headline);
        builder.AppendLine();
        builder.AppendLine(copy.Intro);
        builder.AppendLine();
        builder.AppendLine($"{EmailLocalizations.SupportTicketTitleLabel(locale)}: {ticket.Title}");
        builder.AppendLine($"{EmailLocalizations.SupportTicketTypeLabel(locale)}: {ticket.TicketType}");
        builder.AppendLine($"{EmailLocalizations.SupportTicketStatusLabel(locale)}: {ticket.Status}");
        if (!string.IsNullOrWhiteSpace(changeNote))
        {
            builder.AppendLine();
            builder.AppendLine($"{EmailLocalizations.SupportTicketChangeLabel(locale)}: {changeNote}");
        }

        builder.AppendLine();
        builder.AppendLine(copy.SectionTitle);
        builder.AppendLine(ticket.MarkdownSource);
        return builder.ToString();
    }
}
