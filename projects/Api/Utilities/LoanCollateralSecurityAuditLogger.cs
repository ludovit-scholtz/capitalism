using Api.Data;
using Api.Data.Entities;

namespace Api.Utilities;

public static class LoanCollateralSecurityAuditLogger
{
    public static void Add(
        AppDbContext db,
        Guid playerId,
        string action,
        string reason,
        Guid? loanId = null,
        Guid? buildingId = null,
        string? detail = null,
        bool isDeadLetter = false)
    {
        db.LoanCollateralSecurityAuditLogs.Add(new LoanCollateralSecurityAuditLog
        {
            Id = Guid.NewGuid(),
            LoanId = loanId,
            BuildingId = buildingId,
            PlayerId = playerId,
            Action = action,
            RejectionReason = reason,
            Detail = detail,
            IsDeadLetter = isDeadLetter,
            OccurredAtUtc = DateTime.UtcNow,
        });
    }
}
