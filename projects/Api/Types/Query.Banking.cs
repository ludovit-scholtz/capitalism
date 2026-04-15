using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Bank building queries: public bank info, deposit listings for depositors and owners.
/// </summary>
public sealed partial class Query
{
    // ── Public Bank Discovery ─────────────────────────────────────────────────

    /// <summary>
    /// Returns public information about a specific bank building.
    /// Includes deposit rate, lending rate, total deposits, lendable capacity, and reserve status.
    /// Available to all players (no auth required) so players can compare banks before depositing.
    /// </summary>
    public async Task<BankInfoSummary?> GetBankInfo(
        Guid bankBuildingId,
        [Service] AppDbContext db)
    {
        var bank = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == bankBuildingId && b.Type == BuildingType.Bank);

        if (bank is null)
            return null;

        return await Mutation.BuildBankInfoAsync(db, bank);
    }

    /// <summary>
    /// Returns all bank buildings in the game with their rates and deposit/lending capacity.
    /// Used to populate the bank discovery list in the loan/deposit marketplace.
    /// </summary>
    public async Task<List<BankInfoSummary>> GetAllBanks(
        [Service] AppDbContext db)
    {
        var banks = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .Where(b => b.Type == BuildingType.Bank && b.BaseCapitalDeposited)
            .AsNoTracking()
            .ToListAsync();

        var results = new List<BankInfoSummary>(banks.Count);
        foreach (var bank in banks)
        {
            // Load outstanding loans per bank in one query
            var outstandingPrincipal = await db.Loans
                .Where(l => l.BankBuildingId == bank.Id && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
                .SumAsync(l => (decimal?)l.RemainingPrincipal) ?? 0m;

            var lendable = bank.TotalDeposits * 0.90m;
            var available = Math.Max(0m, lendable - outstandingPrincipal);

            results.Add(new BankInfoSummary
            {
                BankBuildingId = bank.Id,
                BankBuildingName = bank.Name,
                CityId = bank.CityId,
                CityName = bank.City?.Name ?? string.Empty,
                LenderCompanyId = bank.CompanyId,
                LenderCompanyName = bank.Company?.Name ?? string.Empty,
                DepositInterestRatePercent = bank.DepositInterestRatePercent ?? 0m,
                LendingInterestRatePercent = bank.LendingInterestRatePercent ?? 0m,
                TotalDeposits = bank.TotalDeposits,
                LendableCapacity = lendable,
                OutstandingLoanPrincipal = outstandingPrincipal,
                AvailableLendingCapacity = available,
                BaseCapitalDeposited = bank.BaseCapitalDeposited,
            });
        }

        return results;
    }

    // ── Deposit Queries ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns all active deposits owned by the authenticated player's companies.
    /// </summary>
    [Authorize]
    public async Task<List<BankDepositSummary>> GetMyDeposits(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var companyIds = await db.Companies
            .Where(c => c.PlayerId == userId)
            .Select(c => c.Id)
            .ToListAsync();

        var deposits = await db.BankDeposits
            .Include(d => d.BankBuilding)
            .Include(d => d.DepositorCompany)
            .Where(d => companyIds.Contains(d.DepositorCompanyId) && d.IsActive)
            .AsNoTracking()
            .ToListAsync();

        return deposits.Select(d => Mutation.MapToDepositSummary(d, d.BankBuilding, d.DepositorCompany)).ToList();
    }

    /// <summary>
    /// Returns all deposits in a specific bank building (owner view only).
    /// Only the bank's owning player can access this.
    /// </summary>
    [Authorize]
    public async Task<List<BankDepositSummary>> GetBankDeposits(
        Guid bankBuildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var bank = await db.Buildings
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == bankBuildingId && b.Type == BuildingType.Bank);

        if (bank is null || bank.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank building not found or you do not own it.")
                    .SetCode("BANK_NOT_FOUND")
                    .Build());
        }

        var deposits = await db.BankDeposits
            .Include(d => d.DepositorCompany)
            .Where(d => d.BankBuildingId == bankBuildingId && d.IsActive)
            .AsNoTracking()
            .ToListAsync();

        return deposits.Select(d => Mutation.MapToDepositSummary(d, bank, d.DepositorCompany)).ToList();
    }
}
