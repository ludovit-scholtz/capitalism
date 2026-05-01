using Api.Data;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

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

        var unreadCount = await db.PlayerNotifications
            .Where(notification => notification.PlayerId == playerId && !notification.IsRead)
            .CountAsync();

        var items = await db.PlayerNotifications
            .Where(notification => notification.PlayerId == playerId)
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
        return await db.PlayerNotifications
            .Where(notification => notification.PlayerId == playerId && !notification.IsRead)
            .CountAsync();
    }
}
