using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    [Authorize]
    public async Task<bool> MarkPlayerNotificationsRead(
        MarkPlayerNotificationsReadInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        if (input.NotificationIds.Count == 0)
        {
            return true;
        }

        var notifications = await db.PlayerNotifications
            .Where(notification => notification.PlayerId == playerId
                && input.NotificationIds.Contains(notification.Id)
                && !notification.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return true;
    }

    [Authorize]
    public async Task<int> MarkAllPlayerNotificationsRead(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var notifications = await db.PlayerNotifications
            .Where(notification => notification.PlayerId == playerId && !notification.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
        return notifications.Count;
    }

    [Authorize]
    public async Task<BankAccountAlertThresholdResult> SetBankAccountAlertThreshold(
        SetBankAccountAlertThresholdInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var account = await db.BankAccounts
            .Include(bankAccount => bankAccount.Company)
            .FirstOrDefaultAsync(bankAccount => bankAccount.Id == input.BankAccountId
                && ((bankAccount.PlayerId.HasValue && bankAccount.PlayerId == playerId)
                    || (bankAccount.Company != null && bankAccount.Company.PlayerId == playerId)));

        if (account is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank account not found.")
                    .SetCode("BANK_ACCOUNT_NOT_FOUND")
                    .Build());
        }

        if (input.MinBalanceThreshold.HasValue && input.MinBalanceThreshold.Value < 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Minimum balance threshold must be zero or positive.")
                    .SetCode("INVALID_THRESHOLD")
                    .Build());
        }

        account.AlertMinBalanceThreshold = input.MinBalanceThreshold;
        if (!input.MinBalanceThreshold.HasValue || input.MinBalanceThreshold.Value <= 0m)
        {
            account.IsLowBalanceAlertActive = false;
        }

        await db.SaveChangesAsync();

        return new BankAccountAlertThresholdResult
        {
            BankAccountId = account.Id,
            AlertMinBalanceThreshold = account.AlertMinBalanceThreshold,
        };
    }

    [Authorize]
    public async Task<PublicSalesAlertThresholdResult> SetPublicSalesInventoryAlertThreshold(
        SetPublicSalesInventoryAlertThresholdInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var unit = await db.BuildingUnits
            .Include(buildingUnit => buildingUnit.Building)
            .ThenInclude(building => building.Company)
            .FirstOrDefaultAsync(buildingUnit => buildingUnit.Id == input.BuildingUnitId
                && buildingUnit.UnitType == UnitType.PublicSales
                && buildingUnit.Building.Company.PlayerId == playerId);

        if (unit is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Public sales unit not found.")
                    .SetCode("PUBLIC_SALES_UNIT_NOT_FOUND")
                    .Build());
        }

        if (input.MinInventoryThreshold.HasValue && input.MinInventoryThreshold.Value < 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Minimum inventory threshold must be zero or positive.")
                    .SetCode("INVALID_THRESHOLD")
                    .Build());
        }

        unit.LowInventoryAlertThreshold = input.MinInventoryThreshold;
        if (!input.MinInventoryThreshold.HasValue || input.MinInventoryThreshold.Value <= 0m)
        {
            unit.IsLowInventoryAlertActive = false;
        }

        await db.SaveChangesAsync();

        return new PublicSalesAlertThresholdResult
        {
            BuildingUnitId = unit.Id,
            LowInventoryAlertThreshold = unit.LowInventoryAlertThreshold,
        };
    }
}
