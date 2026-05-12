using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

public sealed class DividendGovernanceSettlementPhase : ITickPhase
{
    public string Name => "DividendGovernanceSettlement";
    public int Order => 1005;

    public async Task ProcessAsync(TickContext context)
    {
        var currentTick = context.CurrentTick;
        var proposals = await context.Db.DividendProposals
            .Include(proposal => proposal.Company)
                .ThenInclude(company => company.BankAccounts)
            .Where(proposal =>
                proposal.Status == DividendProposalStatus.Voting
                && proposal.VotingCloseTick <= currentTick)
            .OrderBy(proposal => proposal.VotingCloseTick)
            .ToListAsync();

        if (proposals.Count == 0)
        {
            return;
        }

        var proposalIds = proposals.Select(proposal => proposal.Id).ToList();
        var votes = await context.Db.DividendVotes
            .Where(vote => proposalIds.Contains(vote.ProposalId))
            .ToListAsync();
        var companyIds = proposals.Select(proposal => proposal.CompanyId).Distinct().ToList();
        var shareholdings = await context.Db.Shareholdings
            .Where(holding => companyIds.Contains(holding.CompanyId) && holding.ShareCount > 0m)
            .ToListAsync();
        var ownerCompanyIds = shareholdings
            .Where(holding => holding.OwnerCompanyId.HasValue)
            .Select(holding => holding.OwnerCompanyId!.Value)
            .Distinct()
            .ToList();
        var ownerCompaniesById = ownerCompanyIds.Count == 0
            ? new Dictionary<Guid, Company>()
            : await context.Db.Companies
                .Include(company => company.BankAccounts)
                .Where(company => ownerCompanyIds.Contains(company.Id))
                .ToDictionaryAsync(company => company.Id);
        var playersById = await context.Db.Players
            .ToDictionaryAsync(player => player.Id);

        foreach (var proposal in proposals)
        {
            var proposalVotes = votes.Where(vote => vote.ProposalId == proposal.Id).ToList();
            var forVotes = proposalVotes
                .Where(vote => vote.VoteChoice == DividendVoteChoice.For)
                .Sum(vote => vote.SharesVoted);
            var againstVotes = proposalVotes
                .Where(vote => vote.VoteChoice == DividendVoteChoice.Against)
                .Sum(vote => vote.SharesVoted);

            if (proposal.TotalPayout <= 0m)
            {
                await SettleDividendPolicyProposalAsync(context, proposal, forVotes, againstVotes, currentTick);
                continue;
            }

            var passed = forVotes > againstVotes;
            proposal.SettledAtTick = currentTick;
            proposal.SettledAtUtc = DateTime.UtcNow;

            if (passed)
            {
                await SettleApprovedProposalAsync(context, proposal, shareholdings, ownerCompaniesById, playersById, currentTick);
            }
            else
            {
                proposal.Status = DividendProposalStatus.Rejected;
                CompanyBankingService.TryCredit(proposal.Company.BankAccounts, proposal.TotalPayout, null, out _);
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = proposal.CompanyId,
                    Category = LedgerCategory.Dividend,
                    Description = $"Released dividend reserve for rejected vote ({proposal.DividendPerShare:0.####} per share).",
                    Amount = proposal.TotalPayout,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
                await NotifyShareholdersAsync(
                    context,
                    proposal.CompanyId,
                    currentTick,
                    "Dividend proposal rejected",
                    $"{proposal.Company.Name} dividend proposal was rejected by shareholder vote.",
                    PlayerNotificationType.DividendProposalSettled);
            }
        }
    }

    private static async Task SettleDividendPolicyProposalAsync(
        TickContext context,
        DividendProposal proposal,
        decimal forVotes,
        decimal againstVotes,
        long currentTick)
    {
        var threshold = proposal.Company.TotalSharesIssued / 2m;
        proposal.SettledAtTick = currentTick;
        proposal.SettledAtUtc = DateTime.UtcNow;

        if (forVotes > threshold)
        {
            proposal.Company.DividendPayoutRatio = decimal.Round(
                Math.Clamp(proposal.DividendPerShare, 0m, 1m),
                4,
                MidpointRounding.AwayFromZero);
            proposal.Status = DividendProposalStatus.Settled;
            await NotifyShareholdersAsync(
                context,
                proposal.CompanyId,
                currentTick,
                "Dividend policy approved",
                $"{proposal.Company.Name} dividend policy was updated to {(proposal.Company.DividendPayoutRatio * 100m):0.##}%.",
                PlayerNotificationType.DividendProposalSettled);
            return;
        }

        proposal.Status = againstVotes > threshold
            ? DividendProposalStatus.Rejected
            : DividendProposalStatus.Cancelled;
        await NotifyShareholdersAsync(
            context,
            proposal.CompanyId,
            currentTick,
            "Dividend policy vote closed",
            $"{proposal.Company.Name} dividend policy proposal expired without majority approval.",
            PlayerNotificationType.DividendProposalSettled);
    }

    private static async Task SettleApprovedProposalAsync(
        TickContext context,
        DividendProposal proposal,
        IReadOnlyCollection<Shareholding> allShareholdings,
        IReadOnlyDictionary<Guid, Company> ownerCompaniesById,
        IReadOnlyDictionary<Guid, Player> playersById,
        long currentTick)
    {
        proposal.Status = DividendProposalStatus.Settled;

        var holdings = allShareholdings
            .Where(holding => holding.CompanyId == proposal.CompanyId && holding.ShareCount > 0m)
            .ToList();
        foreach (var holding in holdings)
        {
            var payout = decimal.Round(holding.ShareCount * proposal.DividendPerShare, 4, MidpointRounding.AwayFromZero);
            if (payout <= 0m)
            {
                continue;
            }

            if (holding.OwnerPlayerId is Guid playerId && playersById.TryGetValue(playerId, out var player))
            {
                await PersonalBankAccountService.CreditTrackedGrossCashAsync(context.Db, player, payout);
                context.Db.DividendPayments.Add(new DividendPayment
                {
                    Id = Guid.NewGuid(),
                    CompanyId = proposal.CompanyId,
                    RecipientPlayerId = playerId,
                    ShareCount = holding.ShareCount,
                    AmountPerShare = proposal.DividendPerShare,
                    TotalAmount = payout,
                    GameYear = GameTime.GetGameYear(currentTick),
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    Description = $"Dividend vote payout at tick {currentTick}",
                });

                PlayerNotificationService.Add(
                    context.Db,
                    playerId,
                    PlayerNotificationType.DividendProposalSettled,
                    "Dividend payout settled",
                    $"{proposal.Company.Name} paid {payout:0.####} to your personal account ({proposal.DividendPerShare:0.####} per share).",
                    currentTick,
                    companyId: proposal.CompanyId);
            }
            else if (holding.OwnerCompanyId is Guid ownerCompanyId && ownerCompaniesById.TryGetValue(ownerCompanyId, out var ownerCompany))
            {
                if (!CompanyBankingService.TryCredit(ownerCompany.BankAccounts, payout, null, out var creditedAccount))
                {
                    continue;
                }

                context.Db.DividendPayments.Add(new DividendPayment
                {
                    Id = Guid.NewGuid(),
                    CompanyId = proposal.CompanyId,
                    RecipientCompanyId = ownerCompanyId,
                    ShareCount = holding.ShareCount,
                    AmountPerShare = proposal.DividendPerShare,
                    TotalAmount = payout,
                    GameYear = GameTime.GetGameYear(currentTick),
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    Description = $"Dividend vote payout at tick {currentTick}",
                });

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = ownerCompanyId,
                    BankAccountId = creditedAccount?.Id,
                    Category = LedgerCategory.Dividend,
                    Description = $"Dividend income from {proposal.Company.Name} ({proposal.DividendPerShare:0.####} per share).",
                    Amount = payout,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });

                PlayerNotificationService.Add(
                    context.Db,
                    ownerCompany.PlayerId,
                    PlayerNotificationType.DividendProposalSettled,
                    "Dividend payout settled",
                    $"{ownerCompany.Name} received {payout:0.####} from {proposal.Company.Name} dividend vote.",
                    currentTick,
                    companyId: proposal.CompanyId);
            }
        }

        var latestPrice = await context.Db.SharePriceHistoryEntries
            .AsNoTracking()
            .Where(entry => entry.CompanyId == proposal.CompanyId)
            .OrderByDescending(entry => entry.RecordedAtTick)
            .ThenByDescending(entry => entry.RecordedAtUtc)
            .Select(entry => (decimal?)entry.SharePrice)
            .FirstOrDefaultAsync() ?? 0m;
        var fallbackPrice = proposal.DividendPerShare > 0m ? proposal.DividendPerShare : 1m;
        var adjustedPrice = Math.Max(
            0.0001m,
            decimal.Round((latestPrice > 0m ? latestPrice : fallbackPrice) - proposal.DividendPerShare, 4, MidpointRounding.AwayFromZero));
        context.Db.SharePriceHistoryEntries.Add(new SharePriceHistoryEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = proposal.CompanyId,
            SharePrice = adjustedPrice,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        await NotifyShareholdersAsync(
            context,
            proposal.CompanyId,
            currentTick,
            "Dividend proposal approved",
            $"{proposal.Company.Name} dividend proposal was approved and settled at tick {currentTick}.",
            PlayerNotificationType.DividendProposalSettled);
    }

    private static async Task NotifyShareholdersAsync(
        TickContext context,
        Guid companyId,
        long currentTick,
        string title,
        string message,
        string notificationType)
    {
        var shareholdings = await context.Db.Shareholdings
            .AsNoTracking()
            .Where(holding => holding.CompanyId == companyId && holding.ShareCount > 0m)
            .ToListAsync();
        var ownerCompanyIds = shareholdings
            .Where(holding => holding.OwnerCompanyId.HasValue)
            .Select(holding => holding.OwnerCompanyId!.Value)
            .Distinct()
            .ToList();
        var ownerCompanyPlayerById = ownerCompanyIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : await context.Db.Companies
                .AsNoTracking()
                .Where(company => ownerCompanyIds.Contains(company.Id))
                .ToDictionaryAsync(company => company.Id, company => company.PlayerId);

        var playerIds = shareholdings
            .Where(holding => holding.OwnerPlayerId.HasValue)
            .Select(holding => holding.OwnerPlayerId!.Value)
            .Concat(shareholdings
                .Where(holding => holding.OwnerCompanyId.HasValue)
                .Select(holding => ownerCompanyPlayerById.GetValueOrDefault(holding.OwnerCompanyId!.Value)))
            .Where(playerId => playerId != Guid.Empty)
            .Distinct();
        foreach (var playerId in playerIds)
        {
            PlayerNotificationService.Add(
                context.Db,
                playerId,
                notificationType,
                title,
                message,
                currentTick,
                companyId: companyId);
        }
    }
}
