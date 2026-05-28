using System.Security.Claims;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{
    [HotChocolate.Authorization.Authorize]
    public async Task<SupportTicketInfo> CreateSupportTicket(
        CreateSupportTicketInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] MasterRankingService rankingService,
        [Service] IMasterEmailService emailService,
        CancellationToken cancellationToken)
    {
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var now = DateTime.UtcNow;
        var ticketType = NormalizeTicketType(input.TicketType);
        var title = NormalizeSupportTitle(input.Title);
        var markdown = NormalizeSupportMarkdown(input.MarkdownSource);
        var processing = SupportTicketMarkdownProcessor.Process(markdown);

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            CreatedByPlayerAccountId = player.Id,
            CreatedByEmail = player.Email,
            CreatedByDisplayName = player.DisplayName,
            TicketType = ticketType,
            Status = SupportTicketStatus.Submitted,
            Title = title,
            MarkdownSource = markdown,
            SanitizedPreviewHtml = processing.SanitizedHtml,
            ExtractedUrlsJson = Query.SerializeStringList(processing.ExtractedUrls),
            ExtractedImagesJson = Query.SerializeStringList(processing.ExtractedImages),
            ContainsUnsafeContent = processing.ContainsUnsafeContent,
            ModerationState = SupportTicketModerationState.Pending,
            ModerationReason = processing.ContainsUnsafeContent ? processing.UnsafeReason : "Awaiting administrator moderation.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            StatusUpdatedAtUtc = now,
        };

        AddSupportAuditEvent(
            ticket,
            eventType: "CREATED",
            actorEmail: player.Email,
            actorDisplayName: player.DisplayName,
            note: "Support ticket created.",
            metadataJson: "{}");

        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);
        await emailService.SendSupportTicketCreatedEmailAsync(player, ticket, cancellationToken);

        if (ticketType == SupportTicketType.Suggestion)
        {
            await rankingService.IngestEventAsync(
                eventType: MasterRankingBountyCodes.GameImprover,
                playerEmail: player.Email,
                serverKey: null,
                externalEventId: $"support-ticket:{ticket.Id}",
                uniqueScopeKey: $"support-ticket:{ticket.Id}",
                idempotencyKey: null,
                proofReference: null,
                payloadJson: JsonSerializer.Serialize(new { ticketId = ticket.Id, ticketType = ticketType }),
                occurredAtUtc: now);
        }

        var created = await db.SupportTickets
            .AsNoTracking()
            .Include(item => item.AuditEvents)
            .FirstAsync(item => item.Id == ticket.Id);

        return Query.ToSupportTicketInfo(created, canViewRaw: true, canViewPreview: true);
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<SupportTicketInfo> UpdateSupportTicketContent(
        UpdateSupportTicketContentInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IMasterEmailService emailService,
        CancellationToken cancellationToken)
    {
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var ticket = await db.SupportTickets
            .Include(item => item.AuditEvents)
            .FirstOrDefaultAsync(item => item.Id == input.TicketId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Support ticket not found.")
                    .SetCode("SUPPORT_TICKET_NOT_FOUND")
                    .Build());

        if (ticket.CreatedByPlayerAccountId != player.Id)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You can only update your own support tickets.")
                    .SetCode("SUPPORT_TICKET_FORBIDDEN")
                    .Build());
        }

        if (ticket.Status == SupportTicketStatus.Finished)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Finished support tickets cannot be modified.")
                    .SetCode("SUPPORT_TICKET_FINISHED")
                    .Build());
        }

        var title = NormalizeSupportTitle(input.Title);
        var markdown = NormalizeSupportMarkdown(input.MarkdownSource);
        var processing = SupportTicketMarkdownProcessor.Process(markdown);
        var now = DateTime.UtcNow;

        ticket.Title = title;
        ticket.MarkdownSource = markdown;
        ticket.SanitizedPreviewHtml = processing.SanitizedHtml;
        ticket.ExtractedUrlsJson = Query.SerializeStringList(processing.ExtractedUrls);
        ticket.ExtractedImagesJson = Query.SerializeStringList(processing.ExtractedImages);
        ticket.ContainsUnsafeContent = processing.ContainsUnsafeContent;
        ticket.ModerationState = SupportTicketModerationState.Pending;
        ticket.ModerationReason = processing.ContainsUnsafeContent
            ? processing.UnsafeReason
            : "Content changed. Awaiting administrator moderation.";
        ticket.ModeratedAtUtc = null;
        ticket.ModeratedByEmail = null;
        ticket.UpdatedAtUtc = now;

        AddSupportAuditEvent(
            ticket,
            eventType: "CONTENT_UPDATED",
            actorEmail: player.Email,
            actorDisplayName: player.DisplayName,
            note: "Support ticket content was updated.",
            metadataJson: "{}");

        await db.SaveChangesAsync(cancellationToken);
        await emailService.SendSupportTicketUpdatedEmailAsync(
            player,
            ticket,
            EmailLocalizations.SupportTicketContentUpdatedNote(player.PreferredLocale),
            cancellationToken);
        return Query.ToSupportTicketInfo(ticket, canViewRaw: true, canViewPreview: true);
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<SupportTicketInfo> UpdateSupportTicketStatus(
        UpdateSupportTicketStatusInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        [Service] IMasterEmailService emailService,
        CancellationToken cancellationToken)
    {
        var actorEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, actorEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Support ticket status changes require global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var ticket = await db.SupportTickets
            .Include(item => item.CreatedByPlayerAccount)
            .Include(item => item.AuditEvents)
            .FirstOrDefaultAsync(item => item.Id == input.TicketId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Support ticket not found.")
                    .SetCode("SUPPORT_TICKET_NOT_FOUND")
                    .Build());

        var nextStatus = NormalizeTicketStatus(input.Status);
        if (ticket.Status == nextStatus)
        {
            return Query.ToSupportTicketInfo(ticket, canViewRaw: true, canViewPreview: true);
        }

        if (!SupportTicketStatus.AllowedTransitions.TryGetValue(ticket.Status, out var allowed)
            || !allowed.Contains(nextStatus))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid status transition for support ticket lifecycle.")
                    .SetCode("INVALID_SUPPORT_TICKET_STATUS_TRANSITION")
                    .Build());
        }

        var now = DateTime.UtcNow;
        ticket.Status = nextStatus;
        ticket.StatusUpdatedAtUtc = now;
        ticket.UpdatedAtUtc = now;

        var changeNote = string.IsNullOrWhiteSpace(input.Note)
            ? $"Support ticket status changed to {nextStatus}."
            : input.Note.Trim();

        AddSupportAuditEvent(
            ticket,
            eventType: "STATUS_UPDATED",
            actorEmail: actorEmail,
            actorDisplayName: actorEmail,
            note: changeNote,
            metadataJson: JsonSerializer.Serialize(new { status = nextStatus }));

        await db.SaveChangesAsync(cancellationToken);
        if (ticket.CreatedByPlayerAccount is not null)
        {
            await emailService.SendSupportTicketUpdatedEmailAsync(
                ticket.CreatedByPlayerAccount,
                ticket,
                string.IsNullOrWhiteSpace(input.Note)
                    ? EmailLocalizations.SupportTicketStatusChangedNote(ticket.CreatedByPlayerAccount.PreferredLocale, nextStatus)
                    : changeNote,
                cancellationToken);
        }

        return Query.ToSupportTicketInfo(ticket, canViewRaw: true, canViewPreview: true);
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<SupportTicketInfo> ModerateSupportTicket(
        ModerateSupportTicketInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        [Service] IMasterEmailService emailService,
        CancellationToken cancellationToken)
    {
        var actorEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, actorEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Support ticket moderation requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var ticket = await db.SupportTickets
            .Include(item => item.CreatedByPlayerAccount)
            .Include(item => item.AuditEvents)
            .FirstOrDefaultAsync(item => item.Id == input.TicketId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Support ticket not found.")
                    .SetCode("SUPPORT_TICKET_NOT_FOUND")
                    .Build());

        var now = DateTime.UtcNow;
        ticket.ModerationState = input.Approve
            ? SupportTicketModerationState.Approved
            : SupportTicketModerationState.Rejected;
        ticket.ModeratedByEmail = actorEmail;
        ticket.ModeratedAtUtc = now;
        ticket.ModerationReason = string.IsNullOrWhiteSpace(input.Note)
            ? (input.Approve ? "Content approved by administrator." : "Content rejected by administrator.")
            : input.Note.Trim();
        ticket.UpdatedAtUtc = now;

        AddSupportAuditEvent(
            ticket,
            eventType: "MODERATION_UPDATED",
            actorEmail: actorEmail,
            actorDisplayName: actorEmail,
            note: ticket.ModerationReason,
            metadataJson: JsonSerializer.Serialize(new { moderationState = ticket.ModerationState }));

        await db.SaveChangesAsync(cancellationToken);
        if (ticket.CreatedByPlayerAccount is not null)
        {
            await emailService.SendSupportTicketUpdatedEmailAsync(
                ticket.CreatedByPlayerAccount,
                ticket,
                ticket.ModerationReason,
                cancellationToken);
        }

        return Query.ToSupportTicketInfo(ticket, canViewRaw: true, canViewPreview: true);
    }

    private static void AddSupportAuditEvent(
        SupportTicket ticket,
        string eventType,
        string actorEmail,
        string actorDisplayName,
        string note,
        string metadataJson)
    {
        ticket.AuditEvents.Add(new SupportTicketAuditEvent
        {
            SupportTicket = ticket,
            EventType = eventType,
            ActorEmail = actorEmail,
            ActorDisplayName = actorDisplayName,
            Note = note,
            MetadataJson = metadataJson,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static string NormalizeTicketType(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (!SupportTicketType.All.Contains(normalized))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Ticket type must be SUGGESTION, BUG, or OTHER.")
                    .SetCode("INVALID_SUPPORT_TICKET_TYPE")
                    .Build());
        }

        return normalized;
    }

    private static string NormalizeTicketStatus(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (!SupportTicketStatus.All.Contains(normalized))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Ticket status must be SUBMITTED, IN_PROGRESS, or FINISHED.")
                    .SetCode("INVALID_SUPPORT_TICKET_STATUS")
                    .Build());
        }

        return normalized;
    }

    private static string NormalizeSupportTitle(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 5 || normalized.Length > 220)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Support ticket title must be between 5 and 220 characters.")
                    .SetCode("INVALID_SUPPORT_TICKET_TITLE")
                    .Build());
        }

        return normalized;
    }

    private static string NormalizeSupportMarkdown(string value)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 20 || normalized.Length > 20000)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Support ticket content must be between 20 and 20000 characters.")
                    .SetCode("INVALID_SUPPORT_TICKET_CONTENT")
                    .Build());
        }

        return normalized;
    }
}
