using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine;

public sealed partial class TickContext
{
    private readonly Dictionary<string, HashSet<(Guid PlayerId, Guid? RelatedEntityId, Guid? CompanyId, Guid? BuildingId, Guid? LoanId)>> _unreadNotificationsByType = [];

    /// <summary>
    /// Whether the player already has an unread, unexpired notification of this type for exactly this subject. Phases ask this for every
    /// building or item they might notify about, which used to be one synchronous database query per question (the power grid phase
    /// alone cost about 0.65 s per tick); now the saved unread notifications of a type are read once per tick (#646).
    /// Like the query it replaces, this sees only saved rows: notifications added during the tick are not visible until the next one.
    /// </summary>
    public bool HasUnreadNotification(
        Guid playerId,
        string type,
        Guid? relatedEntityId = null,
        Guid? companyId = null,
        Guid? buildingId = null,
        Guid? loanId = null)
    {
        if (!_unreadNotificationsByType.TryGetValue(type, out var keys))
        {
            var now = DateTime.UtcNow;
            keys = Db.PlayerNotifications
                .AsNoTracking()
                .Where(notification => notification.Type == type
                    && !notification.IsRead
                    && (!notification.ExpiresAtUtc.HasValue || notification.ExpiresAtUtc > now))
                .Select(notification => new { notification.PlayerId, notification.RelatedEntityId, notification.CompanyId, notification.BuildingId, notification.LoanId })
                .AsEnumerable()
                .Select(notification => (notification.PlayerId, notification.RelatedEntityId, notification.CompanyId, notification.BuildingId, notification.LoanId))
                .ToHashSet();
            _unreadNotificationsByType[type] = keys;
        }

        return keys.Contains((playerId, relatedEntityId, companyId, buildingId, loanId));
    }
}
