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
        Guid? loanId = null)
    {
        db.PlayerNotifications.Add(new PlayerNotification
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
            CompanyId = companyId,
            BuildingId = buildingId,
            BuildingUnitId = buildingUnitId,
            BankAccountId = bankAccountId,
            LoanId = loanId,
        });
    }
}
