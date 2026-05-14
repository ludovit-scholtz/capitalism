using Api.Data;
using Api.Data.Entities;

namespace Api.Utilities;

public static class PlayerNotificationService
{
    public static void Add(
        AppDbContext db,
        Guid playerId,
        string type,
        string title,
        string message,
        long currentTick,
        Guid? companyId = null,
        Guid? buildingId = null,
        Guid? buildingUnitId = null,
        Guid? bankAccountId = null,
        Guid? loanId = null,
        string severity = PlayerNotificationSeverity.Info,
        string? titleKey = null,
        string? bodyKey = null,
        string? bodyParamsJson = null,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        DateTime? expiresAtUtc = null)
    {
        db.PlayerNotifications.Add(new PlayerNotification
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = type,
            Title = title,
            Message = message,
            Severity = severity,
            TitleKey = titleKey,
            BodyKey = bodyKey,
            BodyParamsJson = bodyParamsJson,
            IsRead = false,
            CreatedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAtUtc,
            CompanyId = companyId,
            BuildingId = buildingId,
            BuildingUnitId = buildingUnitId,
            BankAccountId = bankAccountId,
            LoanId = loanId,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
        });
    }

    public static bool HasUnreadDuplicate(
        AppDbContext db,
        Guid playerId,
        string type,
        Guid? relatedEntityId = null,
        Guid? companyId = null,
        Guid? buildingId = null,
        Guid? loanId = null)
    {
        return db.PlayerNotifications.Any(notification =>
            notification.PlayerId == playerId
            && notification.Type == type
            && !notification.IsRead
            && notification.RelatedEntityId == relatedEntityId
            && notification.CompanyId == companyId
            && notification.BuildingId == buildingId
            && notification.LoanId == loanId
            && (!notification.ExpiresAtUtc.HasValue || notification.ExpiresAtUtc > DateTime.UtcNow));
    }
}
