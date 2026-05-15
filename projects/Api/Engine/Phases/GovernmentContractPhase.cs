using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Evaluates government tenders, awards expired bidding windows, notifies winners, and keeps the market replenished.
/// </summary>
public sealed class GovernmentContractPhase : ITickPhase
{
    public string Name => "GovernmentContracts";
    public int Order => 800;

    public async Task ProcessAsync(TickContext context)
    {
        var currentTick = context.CurrentTick;
        var dueContracts = await context.Db.GovernmentContracts
            .Include(contract => contract.City)
            .Include(contract => contract.ProductType)
            .Include(contract => contract.Bids)
            .Include(contract => contract.WinnerCompany)
            .Include(contract => contract.Fulfillment)
            .Where(contract =>
                contract.Status == GovernmentContractStatus.Open
                && contract.DeadlineTick <= currentTick)
            .ToListAsync();

        foreach (var contract in dueContracts)
        {
            await EvaluateAwardAsync(context, contract, currentTick);
        }

        await SendDeadlineWarningsAsync(context, currentTick);

        await GovernmentContractService.EnsureOpenContractsPerCityAsync(context.Db, currentTick, CancellationToken.None);
    }

    private static async Task EvaluateAwardAsync(TickContext context, GovernmentContract contract, long currentTick)
    {
        var candidates = new List<(ContractBid Bid, decimal QualityLevel)>();
        foreach (var bid in contract.Bids.OrderBy(bid => bid.BidPricePerUnit).ThenBy(bid => bid.SubmittedAtTick))
        {
            if (bid.BidPricePerUnit > contract.BudgetCap)
            {
                continue;
            }

            var eligibility = await GovernmentContractService.EvaluateCompanyEligibilityAsync(context.Db, contract, bid.CompanyId, CancellationToken.None);
            if (!eligibility.IsEligible)
            {
                continue;
            }

            candidates.Add((bid, eligibility.CurrentQualityLevel));
        }

        if (candidates.Count == 0)
        {
            contract.Status = GovernmentContractStatus.Expired;
            return;
        }

        var winner = candidates
            .OrderBy(candidate => candidate.Bid.BidPricePerUnit)
            .ThenBy(candidate => candidate.Bid.SubmittedAtTick)
            .First();

        contract.Status = GovernmentContractStatus.Awarded;
        contract.WinnerCompanyId = winner.Bid.CompanyId;

        if (contract.Fulfillment is null)
        {
            context.Db.ContractFulfillments.Add(new ContractFulfillment
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                CompanyId = winner.Bid.CompanyId,
                QuantityDelivered = 0m,
                QuantityRequired = contract.QuantityRequired,
                LastShipmentTick = currentTick,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        var winnerCompany = await context.Db.Companies.AsNoTracking().FirstOrDefaultAsync(company => company.Id == winner.Bid.CompanyId);
        if (winnerCompany is null)
        {
            return;
        }

        PlayerNotificationService.Add(
            context.Db,
            winnerCompany.PlayerId,
            PlayerNotificationType.ContractAwarded,
            "Government contract awarded",
            $"Your company won '{contract.Title}' at {winner.Bid.BidPricePerUnit:N2} per unit.",
            currentTick,
            winnerCompany.Id,
            severity: PlayerNotificationSeverity.Info,
            relatedEntityType: "GOVERNMENT_CONTRACT",
            relatedEntityId: contract.Id);
    }

    private static async Task SendDeadlineWarningsAsync(TickContext context, long currentTick)
    {
        var warningTick = currentTick + GameConstants.GovernmentContractDeadlineWarningTicks;
        var contracts = await context.Db.GovernmentContracts
            .Include(contract => contract.WinnerCompany)
            .Where(contract =>
                contract.Status == GovernmentContractStatus.Awarded
                && contract.WinnerCompanyId.HasValue
                && !contract.DeadlineWarningSentAtTick.HasValue
                && contract.DeadlineTick <= warningTick
                && contract.DeadlineTick >= currentTick)
            .ToListAsync();

        foreach (var contract in contracts)
        {
            if (contract.WinnerCompany is null)
            {
                continue;
            }

            PlayerNotificationService.Add(
                context.Db,
                contract.WinnerCompany.PlayerId,
                PlayerNotificationType.ContractDeadlineWarning,
                "Government contract deadline approaching",
                $"Contract '{contract.Title}' deadline is at tick {contract.DeadlineTick}.",
                currentTick,
                contract.WinnerCompanyId,
                severity: PlayerNotificationSeverity.Warning,
                relatedEntityType: "GOVERNMENT_CONTRACT",
                relatedEntityId: contract.Id);
            contract.DeadlineWarningSentAtTick = currentTick;
        }
    }
}
