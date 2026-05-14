using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Emits player alerts for threshold-based conditions and upcoming loan repayments.
/// </summary>
public sealed class PlayerAlertPhase : ITickPhase
{
    public string Name => "PlayerAlerts";
    public int Order => 960;

    private const long LoanDueSoonWindowTicks = 10;

    public Task ProcessAsync(TickContext context)
    {
        EmitBankLowBalanceAlerts(context);
        EmitPublicSalesLowInventoryAlerts(context);
        EmitLoanDueSoonAlerts(context);
        return Task.CompletedTask;
    }

    private static void EmitBankLowBalanceAlerts(TickContext context)
    {
        foreach (var account in context.BankAccountsById.Values)
        {
            if (!account.AlertMinBalanceThreshold.HasValue || account.AlertMinBalanceThreshold.Value <= 0m)
            {
                account.IsLowBalanceAlertActive = false;
                continue;
            }

            var threshold = account.AlertMinBalanceThreshold.Value;
            var isBelow = account.Balance < threshold;
            var playerId = ResolveAccountPlayerId(context, account);
            if (!playerId.HasValue)
            {
                account.IsLowBalanceAlertActive = false;
                continue;
            }

            if (isBelow && !account.IsLowBalanceAlertActive)
            {
                PlayerNotificationService.Add(
                    context.Db,
                    playerId.Value,
                    PlayerNotificationType.BankAccountLowBalance,
                    "Low bank balance",
                    $"Account {account.AccountNumber} dropped to {account.Balance:0.00} {account.CurrencyCode} (threshold {threshold:0.00} {account.CurrencyCode}).",
                    context.CurrentTick,
                    account.CompanyId,
                    bankAccountId: account.Id);
            }

            account.IsLowBalanceAlertActive = isBelow;
        }
    }

    private static Guid? ResolveAccountPlayerId(TickContext context, BankAccount account)
    {
        if (account.PlayerId.HasValue)
        {
            return account.PlayerId.Value;
        }

        if (account.CompanyId.HasValue && context.CompaniesById.TryGetValue(account.CompanyId.Value, out var company))
        {
            return company.PlayerId;
        }

        return null;
    }

    private static void EmitPublicSalesLowInventoryAlerts(TickContext context)
    {
        foreach (var (buildingId, units) in context.UnitsByBuilding)
        {
            if (!context.BuildingsById.TryGetValue(buildingId, out var building))
            {
                continue;
            }

            if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company))
            {
                continue;
            }

            foreach (var unit in units)
            {
                if (unit.UnitType != UnitType.PublicSales)
                {
                    continue;
                }

                if (!unit.LowInventoryAlertThreshold.HasValue || unit.LowInventoryAlertThreshold.Value <= 0m)
                {
                    unit.IsLowInventoryAlertActive = false;
                    continue;
                }

                var threshold = unit.LowInventoryAlertThreshold.Value;
                var quantity = context.InventoryByUnit.TryGetValue(unit.Id, out var inventory)
                    ? inventory.Sum(item => item.Quantity)
                    : 0m;
                var isBelow = quantity < threshold;

                if (isBelow && !unit.IsLowInventoryAlertActive)
                {
                    PlayerNotificationService.Add(
                        context.Db,
                        company.PlayerId,
                        PlayerNotificationType.PublicSalesInventoryLow,
                        "Public sales inventory is low",
                        $"{building.Name} has only {quantity:0.####} units in PUBLIC_SALES inventory (threshold {threshold:0.####}).",
                        context.CurrentTick,
                        company.Id,
                        building.Id,
                        unit.Id,
                        building.BankAccountId);
                }

                unit.IsLowInventoryAlertActive = isBelow;
            }
        }
    }

    private static void EmitLoanDueSoonAlerts(TickContext context)
    {
        var dueSoonTick = context.CurrentTick + LoanDueSoonWindowTicks;
        var dueSoonLoans = context.Db.Loans
            .Where(loan => (loan.Status == LoanStatus.Active || loan.Status == LoanStatus.Overdue)
                && loan.NextPaymentTick > context.CurrentTick
                && loan.NextPaymentTick <= dueSoonTick)
            .ToList();

        foreach (var loan in dueSoonLoans)
        {
            if (!context.CompaniesById.TryGetValue(loan.BorrowerCompanyId, out var borrower))
            {
                continue;
            }

            if (loan.DueSoonAlertForPaymentTick == loan.NextPaymentTick)
            {
                continue;
            }

            var ticksLeft = loan.NextPaymentTick - context.CurrentTick;
            PlayerNotificationService.Add(
                context.Db,
                borrower.PlayerId,
                PlayerNotificationType.LoanPaymentDue,
                "Loan repayment due soon",
                $"{borrower.Name} has a scheduled repayment due in {ticksLeft} ticks.",
                context.CurrentTick,
                borrower.Id,
                loanId: loan.Id,
                bankAccountId: loan.BorrowerBankAccountId,
                severity: PlayerNotificationSeverity.Warning,
                relatedEntityType: "LOAN",
                relatedEntityId: loan.Id);

            loan.DueSoonAlertForPaymentTick = loan.NextPaymentTick;
        }
    }
}
