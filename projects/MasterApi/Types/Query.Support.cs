using System.Security.Claims;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Query
{
    [HotChocolate.Authorization.Authorize]
    public async Task<List<SupportTicketInfo>> GetMySupportTickets(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        ListSupportTicketsInput? input = null)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var tickets = await BuildSupportTicketQuery(db, input)
            .Where(ticket => ticket.CreatedByPlayerAccountId == player.Id)
            .Include(ticket => ticket.AuditEvents)
            .OrderByDescending(ticket => ticket.CreatedAtUtc)
            .ThenByDescending(ticket => ticket.UpdatedAtUtc)
            .Skip(GetOffset(input))
            .Take(GetLimit(input))
            .AsSplitQuery()
            .ToListAsync();

        return tickets.Select(ticket => ToSupportTicketInfo(ticket, canViewRaw: true, canViewPreview: true)).ToList();
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<List<SupportTicketInfo>> GetSupportTicketsAdmin(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        ListSupportTicketsInput? input = null)
    {
        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Support ticket administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        IQueryable<SupportTicket> query = BuildSupportTicketQuery(db, input).Include(ticket => ticket.AuditEvents);
        query = ApplyAdminSort(query, input);

        var tickets = await query
            .Skip(GetOffset(input))
            .Take(GetLimit(input))
            .AsSplitQuery()
            .ToListAsync();

        return tickets.Select(ticket => ToSupportTicketInfo(ticket, canViewRaw: true, canViewPreview: true)).ToList();
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<SupportTicketInfo> GetSupportTicket(
        Guid ticketId,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);

        var ticket = await db.SupportTickets
            .Include(item => item.AuditEvents)
            .FirstOrDefaultAsync(item => item.Id == ticketId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Support ticket not found.")
                    .SetCode("SUPPORT_TICKET_NOT_FOUND")
                    .Build());

        var canAdminRead = access.CanAccessEveryGameDashboard;
        var isOwner = ticket.CreatedByPlayerAccountId == player.Id;
        if (!canAdminRead && !isOwner)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You can only access your own support tickets.")
                    .SetCode("SUPPORT_TICKET_FORBIDDEN")
                    .Build());
        }

        return ToSupportTicketInfo(ticket, canViewRaw: true, canViewPreview: canAdminRead || isOwner);
    }

    internal static IQueryable<SupportTicket> BuildSupportTicketQuery(MasterDbContext db, ListSupportTicketsInput? input)
    {
        var query = db.SupportTickets.AsNoTracking().AsQueryable();

        if (input is null)
        {
            return query;
        }

        if (!string.IsNullOrWhiteSpace(input.TicketType))
        {
            var type = input.TicketType.Trim().ToUpperInvariant();
            if (!SupportTicketType.All.Contains(type))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Ticket type must be SUGGESTION, BUG, or OTHER.")
                        .SetCode("INVALID_SUPPORT_TICKET_TYPE")
                        .Build());
            }

            query = query.Where(ticket => ticket.TicketType == type);
        }

        if (!string.IsNullOrWhiteSpace(input.Status))
        {
            var status = input.Status.Trim().ToUpperInvariant();
            if (!SupportTicketStatus.All.Contains(status))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Ticket status must be SUBMITTED, IN_PROGRESS, or FINISHED.")
                        .SetCode("INVALID_SUPPORT_TICKET_STATUS")
                        .Build());
            }

            query = query.Where(ticket => ticket.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(input.SearchTitle))
        {
            var search = input.SearchTitle.Trim().ToLowerInvariant();
            query = query.Where(ticket => ticket.Title.ToLower().Contains(search));
        }

        if (input.CreatedFromUtc.HasValue)
        {
            query = query.Where(ticket => ticket.CreatedAtUtc >= input.CreatedFromUtc.Value);
        }

        if (input.CreatedToUtc.HasValue)
        {
            query = query.Where(ticket => ticket.CreatedAtUtc <= input.CreatedToUtc.Value);
        }

        if (input.UnsafeOnly is true)
        {
            query = query.Where(ticket => ticket.ContainsUnsafeContent);
        }

        return query;
    }

    internal static IQueryable<SupportTicket> ApplyAdminSort(IQueryable<SupportTicket> query, ListSupportTicketsInput? input)
    {
        if (input is null)
        {
            return query.OrderByDescending(ticket => ticket.CreatedAtUtc);
        }

        var sortBy = input.SortBy.Trim().ToUpperInvariant();
        var sortDirection = input.SortDirection.Trim().ToUpperInvariant();
        var desc = sortDirection != "ASC";

        return sortBy switch
        {
            "TITLE" => desc
                ? query.OrderByDescending(ticket => ticket.Title).ThenByDescending(ticket => ticket.CreatedAtUtc)
                : query.OrderBy(ticket => ticket.Title).ThenByDescending(ticket => ticket.CreatedAtUtc),
            "UPDATED_AT" => desc
                ? query.OrderByDescending(ticket => ticket.UpdatedAtUtc).ThenByDescending(ticket => ticket.CreatedAtUtc)
                : query.OrderBy(ticket => ticket.UpdatedAtUtc).ThenByDescending(ticket => ticket.CreatedAtUtc),
            _ => desc
                ? query.OrderByDescending(ticket => ticket.CreatedAtUtc).ThenByDescending(ticket => ticket.UpdatedAtUtc)
                : query.OrderBy(ticket => ticket.CreatedAtUtc).ThenByDescending(ticket => ticket.UpdatedAtUtc),
        };
    }

    internal static int GetLimit(ListSupportTicketsInput? input) => Math.Clamp(input?.Limit ?? 50, 1, 200);

    internal static int GetOffset(ListSupportTicketsInput? input) => Math.Max(0, input?.Offset ?? 0);

    internal static SupportTicketInfo ToSupportTicketInfo(SupportTicket ticket, bool canViewRaw, bool canViewPreview)
    {
        var moderationApproved = ticket.ModerationState == SupportTicketModerationState.Approved;
        var preview = canViewPreview && moderationApproved ? ticket.SanitizedPreviewHtml : null;

        return new SupportTicketInfo
        {
            Id = ticket.Id,
            TicketType = ticket.TicketType,
            Status = ticket.Status,
            Title = ticket.Title,
            MarkdownSource = canViewRaw ? ticket.MarkdownSource : string.Empty,
            SanitizedPreviewHtml = preview,
            ContainsUnsafeContent = ticket.ContainsUnsafeContent,
            ModerationState = ticket.ModerationState,
            ModerationReason = ticket.ModerationReason,
            ModeratedByEmail = ticket.ModeratedByEmail,
            ModeratedAtUtc = ticket.ModeratedAtUtc,
            CreatedByEmail = ticket.CreatedByEmail,
            CreatedByDisplayName = ticket.CreatedByDisplayName,
            CreatedAtUtc = ticket.CreatedAtUtc,
            UpdatedAtUtc = ticket.UpdatedAtUtc,
            StatusUpdatedAtUtc = ticket.StatusUpdatedAtUtc,
            ExtractedUrls = DeserializeStringList(ticket.ExtractedUrlsJson),
            ExtractedImages = DeserializeStringList(ticket.ExtractedImagesJson),
            Activity = ticket.AuditEvents
                .OrderByDescending(eventItem => eventItem.CreatedAtUtc)
                .Select(eventItem => new SupportTicketAuditEventInfo
                {
                    Id = eventItem.Id,
                    EventType = eventItem.EventType,
                    ActorEmail = eventItem.ActorEmail,
                    ActorDisplayName = eventItem.ActorDisplayName,
                    Note = eventItem.Note,
                    MetadataJson = eventItem.MetadataJson,
                    CreatedAtUtc = eventItem.CreatedAtUtc,
                })
                .ToList(),
        };
    }

    internal static string SerializeStringList(IReadOnlyCollection<string> values)
    {
        return JsonSerializer.Serialize(values);
    }

    internal static List<string> DeserializeStringList(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(raw) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
