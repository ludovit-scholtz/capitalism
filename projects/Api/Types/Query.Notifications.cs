using Api.Data;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace Api.Types;

public sealed partial class Query
{
    [Authorize]
    public async Task<PlayerNotificationInbox> PlayerNotificationInbox(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        int limit = 20)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var safeLimit = Math.Clamp(limit <= 0 ? 20 : limit, 1, 20);
        var now = DateTime.UtcNow;

        var baseQuery = db.PlayerNotifications
            .Where(notification => notification.PlayerId == playerId
                && (!notification.ExpiresAtUtc.HasValue || notification.ExpiresAtUtc > now));

        var unreadCount = await baseQuery
            .Where(notification => !notification.IsRead)
            .CountAsync();

        var items = await baseQuery
            .OrderByDescending(notification => notification.CreatedAtTick)
            .ThenByDescending(notification => notification.CreatedAtUtc)
            .Take(safeLimit)
            .Select(notification => new PlayerNotificationItem
            {
                Id = notification.Id,
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                IsRead = notification.IsRead,
                CreatedAtTick = notification.CreatedAtTick,
                CreatedAtUtc = notification.CreatedAtUtc,
                CompanyId = notification.CompanyId,
                BuildingId = notification.BuildingId,
                BuildingUnitId = notification.BuildingUnitId,
                BankAccountId = notification.BankAccountId,
                LoanId = notification.LoanId,
                Severity = notification.Severity,
                TitleKey = notification.TitleKey,
                BodyKey = notification.BodyKey,
                BodyParamsJson = notification.BodyParamsJson,
                ExpiresAtUtc = notification.ExpiresAtUtc,
                RelatedEntityType = notification.RelatedEntityType,
                RelatedEntityId = notification.RelatedEntityId,
            })
            .ToListAsync();

        return new PlayerNotificationInbox
        {
            UnreadCount = unreadCount,
            Items = items,
        };
    }

    [Authorize]
    public async Task<int> PlayerNotificationUnreadCount(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var now = DateTime.UtcNow;
        return await db.PlayerNotifications
            .Where(notification => notification.PlayerId == playerId
                && !notification.IsRead
                && (!notification.ExpiresAtUtc.HasValue || notification.ExpiresAtUtc > now))
            .CountAsync();
    }

    [Authorize]
    public async Task<NotificationConnection> MyNotifications(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        int first = 20,
        string? after = null,
        bool onlyUnread = false)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var safeFirst = Math.Clamp(first <= 0 ? 20 : first, 1, 50);
        var now = DateTime.UtcNow;

        var query = db.PlayerNotifications
            .Where(notification => notification.PlayerId == playerId
                && (!notification.ExpiresAtUtc.HasValue || notification.ExpiresAtUtc > now));

        if (onlyUnread)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        var totalCount = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(after))
        {
            var decoded = DecodeCursor(after);
            if (decoded is not null)
            {
                var (cursorTick, _) = decoded.Value;
                query = query.Where(notification => notification.CreatedAtTick < cursorTick);
            }
        }

        var entities = await query
            .OrderByDescending(notification => notification.CreatedAtTick)
            .ThenByDescending(notification => notification.Id)
            .Take(safeFirst + 1)
            .ToListAsync();

        var hasNextPage = entities.Count > safeFirst;
        var pageEntities = hasNextPage ? entities.Take(safeFirst).ToList() : entities;

        var edges = pageEntities
            .Select(notification => new NotificationEdge
            {
                Cursor = EncodeCursor(notification.CreatedAtTick, notification.Id),
                Node = new PlayerNotificationItem
                {
                    Id = notification.Id,
                    Type = notification.Type,
                    Title = notification.Title,
                    Message = notification.Message,
                    IsRead = notification.IsRead,
                    CreatedAtTick = notification.CreatedAtTick,
                    CreatedAtUtc = notification.CreatedAtUtc,
                    CompanyId = notification.CompanyId,
                    BuildingId = notification.BuildingId,
                    BuildingUnitId = notification.BuildingUnitId,
                    BankAccountId = notification.BankAccountId,
                    LoanId = notification.LoanId,
                    Severity = notification.Severity,
                    TitleKey = notification.TitleKey,
                    BodyKey = notification.BodyKey,
                    BodyParamsJson = notification.BodyParamsJson,
                    ExpiresAtUtc = notification.ExpiresAtUtc,
                    RelatedEntityType = notification.RelatedEntityType,
                    RelatedEntityId = notification.RelatedEntityId,
                },
            })
            .ToList();

        return new NotificationConnection
        {
            TotalCount = totalCount,
            Edges = edges,
            PageInfo = new NotificationPageInfo
            {
                HasNextPage = hasNextPage,
                EndCursor = edges.LastOrDefault()?.Cursor,
            },
        };
    }

    [Authorize]
    public Task<int> NotificationCount(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
        => PlayerNotificationUnreadCount(db, httpContextAccessor);

    private static string EncodeCursor(long tick, Guid id)
    {
        var payload = string.Create(CultureInfo.InvariantCulture, $"{tick}:{id}");
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    private static (long Tick, Guid Id)? DecodeCursor(string cursor)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = decoded.Split(':', 2);
            if (parts.Length != 2
                || !long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick)
                || !Guid.TryParse(parts[1], out var id))
            {
                return null;
            }

            return (tick, id);
        }
        catch
        {
            return null;
        }
    }
}
