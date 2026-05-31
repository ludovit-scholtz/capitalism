using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Company merge and additional company IPO mutations.
/// Handles absorbing one company into another and launching new companies via IPO.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>
    /// Merges a target company (where the player holds ≥90% combined ownership) into a destination
    /// company that the player directly controls. All buildings, lots, cash, and owned shareholdings
    /// are transferred to the destination company. Taxes are settled for the target company at the
    /// time of merge. The target company is then permanently closed.
    /// </summary>
    [Authorize]
    public async Task<MergeCompanyResult> MergeCompany(
        MergeCompanyInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var companies = await db.Companies
            .Include(c => c.BankAccounts)
            .ToListAsync();

        var targetCompany = companies.FirstOrDefault(c => c.Id == input.TargetCompanyId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Target company not found.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());

        var destinationCompany = companies.FirstOrDefault(c => c.Id == input.DestinationCompanyId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Destination company not found.")
                    .SetCode("DESTINATION_COMPANY_NOT_FOUND")
                    .Build());

        if (destinationCompany.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You must directly control the destination company.")
                    .SetCode("DESTINATION_NOT_CONTROLLED")
                    .Build());
        }

        if (targetCompany.Id == destinationCompany.Id)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Target and destination companies must be different.")
                    .SetCode("SAME_COMPANY")
                    .Build());
        }

        var shareholdings = await db.Shareholdings
            .Where(h => h.CompanyId == targetCompany.Id)
            .ToListAsync();

        var combinedRatio = ComputeControlledOwnershipRatio(userId, targetCompany, companies, shareholdings);
        if (combinedRatio < 0.9m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"You need at least 90% combined ownership to merge this company. Current: {combinedRatio:P1}")
                    .SetCode("INSUFFICIENT_OWNERSHIP_FOR_MERGE")
                    .Build());
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;

        // Settle taxes for the target company at merge time
        var taxableEntries = await db.LedgerEntries
            .Where(e => e.CompanyId == targetCompany.Id)
            .ToListAsync();
        var taxableIncome = LedgerCalculator.ComputeTaxableIncome(taxableEntries);
        var taxRate = gameState?.TaxRate ?? 0m;
        if (taxableIncome > 0m && taxRate > 0m)
        {
            // TaxRate is stored as a percentage (e.g. 15 for 15%); use the shared helper so the
            // merge-time tax matches the annual TaxPhase calculation instead of over-charging.
            var taxDue = GameTime.ComputeEstimatedIncomeTax(taxableIncome, taxRate);
            // Clamp the merge-time tax to the target's available balance so cash-poor
            // companies cannot merge to escape tax (matching the TaxPhase convention).
            var availableBalance = CompanyBankingService.GetAvailableBalance(targetCompany.BankAccounts);
            var taxPaid = Math.Min(availableBalance, taxDue);
            if (taxPaid > 0m && CompanyBankingService.TryDebit(targetCompany.BankAccounts, taxPaid))
            {
                db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = targetCompany.Id,
                    Category = LedgerCategory.Tax,
                    Description = $"Merger settlement tax",
                    Amount = -taxPaid,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }
        }

        // Transfer buildings
        var buildings = await db.Buildings
            .Where(b => b.CompanyId == targetCompany.Id)
            .ToListAsync();
        foreach (var building in buildings)
        {
            building.CompanyId = destinationCompany.Id;
        }

        // Transfer building lots
        var lots = await db.BuildingLots
            .Where(l => l.OwnerCompanyId == targetCompany.Id)
            .ToListAsync();
        foreach (var lot in lots)
        {
            lot.OwnerCompanyId = destinationCompany.Id;
        }

        // Transfer shareholdings owned BY the target company
        var ownedShareholdings = await db.Shareholdings
            .Where(h => h.OwnerCompanyId == targetCompany.Id)
            .ToListAsync();
        foreach (var holding in ownedShareholdings)
        {
            // Merge with existing holding in destination if present
            var existingHolding = await db.Shareholdings.FirstOrDefaultAsync(h =>
                h.CompanyId == holding.CompanyId && h.OwnerCompanyId == destinationCompany.Id);
            if (existingHolding is not null)
            {
                existingHolding.ShareCount += holding.ShareCount;
                db.Shareholdings.Remove(holding);
            }
            else
            {
                holding.OwnerCompanyId = destinationCompany.Id;
            }
        }

        var transferredBalances = targetCompany.BankAccounts.Sum(account => Math.Max(account.Balance, 0m));
        foreach (var account in targetCompany.BankAccounts)
        {
            account.CompanyId = destinationCompany.Id;
            destinationCompany.BankAccounts.Add(account);
        }
        targetCompany.BankAccounts.Clear();

        // Record the merger as a ledger entry on destination
        if (transferredBalances > 0m)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = destinationCompany.Id,
                Category = LedgerCategory.Other,
                Description = $"Merger: bank-account balances received from {targetCompany.Name}",
                Amount = transferredBalances,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }

        var buildingsTransferred = buildings.Count;
        var absorbedName = targetCompany.Name;

        // Remove all shareholdings IN the target company (shares become worthless)
        var targetShareholdings = await db.Shareholdings
            .Where(h => h.CompanyId == targetCompany.Id)
            .ToListAsync();
        db.Shareholdings.RemoveRange(targetShareholdings);

        // Remove salary settings and any pending player active-company references
        var salarySettings = await db.CompanyCitySalarySettings
            .Where(s => s.CompanyId == targetCompany.Id)
            .ToListAsync();
        db.CompanyCitySalarySettings.RemoveRange(salarySettings);

        // If any player is currently scoped to the target company, switch them back to person
        var affectedPlayers = await db.Players
            .Where(p => p.ActiveCompanyId == targetCompany.Id)
            .ToListAsync();
        foreach (var p in affectedPlayers)
        {
            p.ActiveAccountType = AccountContextType.Person;
            p.ActiveCompanyId = null;
        }

        db.Companies.Remove(targetCompany);

        await db.SaveChangesAsync();

        return new MergeCompanyResult
        {
            DestinationCompanyId = destinationCompany.Id,
            DestinationCompanyName = destinationCompany.Name,
            AbsorbedCompanyName = absorbedName,
            CashTransferred = transferredBalances,
            BuildingsTransferred = buildingsTransferred,
        };
    }

    // ── Additional Company IPO constants ──────────────────────────────────────
    internal const long MinCompanyAgeTicksConst = 8760L;      // 1 game year
    internal const long ProfitabilityWindowTicksConst = 365L; // minimum window for profitability check
    private const long MinCompanyAgeTicks = MinCompanyAgeTicksConst;
    private const long ProfitabilityWindowTicks = ProfitabilityWindowTicksConst;
    private const int MaxPlayerCompanies = 5;

    /// <summary>
    /// Launches a second (or further) company via the IPO path.
    /// Prerequisites: player has ≥1 existing company that is ≥1 game year old,
    /// the oldest company has been net-profitable over the last 365 ticks,
    /// the player's personal USD balance is ≥ $200 000, and the player does not
    /// already control 5 companies.
    /// </summary>
    [Authorize]
    public async Task<Company> StartAdditionalCompany(
        StartAdditionalCompanyInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext!.RequestAborted;
        var nowUtc = DateTime.UtcNow;

        var player = await db.Players.FindAsync(userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var currentTick = await GetCurrentTickAsync(db);

        // Load all of this player's companies.
        var playerCompanies = await db.Companies
            .Where(c => c.PlayerId == userId)
            .ToListAsync(ct);

        // Enforce the max-companies cap before doing anything else.
        if (playerCompanies.Count >= MaxPlayerCompanies)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"You've reached the maximum of {MaxPlayerCompanies} companies. Close or merge a company to start a new one.")
                    .SetCode("MAX_COMPANIES_REACHED")
                    .Build());
        }

        // Player must have at least one existing company to qualify.
        if (playerCompanies.Count == 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You must complete onboarding and have an existing company before starting an additional one.")
                    .SetCode("NO_EXISTING_COMPANY")
                    .Build());
        }

        // Check that the oldest company is at least 1 game year old.
        var firstCompany = playerCompanies.OrderBy(c => c.FoundedAtTick).First();
        var companyAge = currentTick - firstCompany.FoundedAtTick;
        if (companyAge < MinCompanyAgeTicks)
        {
            var ticksRemaining = MinCompanyAgeTicks - companyAge;
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Your first company must be operational for at least 1 game year ({MinCompanyAgeTicks} ticks). Current age: {companyAge} ticks ({ticksRemaining} ticks remaining).")
                    .SetCode("COMPANY_TOO_YOUNG")
                    .Build());
        }

        // Check profitability: the company's net income over the last 365 ticks must be positive.
        var windowStart = currentTick - ProfitabilityWindowTicks;
        var netIncome = await db.LedgerEntries
            .Where(e => e.CompanyId == firstCompany.Id && e.RecordedAtTick >= windowStart)
            .SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        if (netIncome <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Your first company must be profitable over the last {ProfitabilityWindowTicks} ticks. Current net income in window: {netIncome:N0}.")
                    .SetCode("COMPANY_NOT_PROFITABLE")
                    .Build());
        }

        // Validate company name.
        var trimmedName = input.CompanyName?.Trim() ?? string.Empty;
        if (trimmedName.Length < 3)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company name must be at least 3 characters.")
                    .SetCode("INVALID_COMPANY_NAME")
                    .Build());
        }

        // Validate city.
        var city = await db.Cities.FindAsync(new object[] { input.CityId }, ct)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("City not found.")
                    .SetCode("CITY_NOT_FOUND")
                    .Build());

        // Check personal USD balance ≥ $200 000.
        var personalBalance = await PersonalBankAccountService.GetTrackedBalanceAsync(db, userId, "USD", ct);
        if (personalBalance < StarterFounderContribution)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Your personal account has insufficient balance. You need ${StarterFounderContribution:N0} USD but only have ${personalBalance:N0} USD.")
                    .SetCode("INSUFFICIENT_PERSONAL_FUNDS")
                    .Build());
        }

        // Resolve IPO tier and compute FX-scaled amounts.
        var ipoSelection = ResolveStarterIpoSelection(input.IpoRaiseTarget);
        var fxRate = await Query.ComputeForexRateAsync(db, "USD", city.CurrencyCode);
        var founderContributionLocal = Math.Round(StarterFounderContribution * fxRate, 2);
        var ipoRaiseLocal = Math.Round(ipoSelection.RaiseTarget * fxRate, 2);

        // Create the new company.
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = userId,
            Name = trimmedName,
            TotalSharesIssued = DefaultCompanyShareCount,
            DividendPayoutRatio = DefaultDividendPayoutRatio,
            FoundedAtUtc = nowUtc,
            FoundedAtTick = currentTick,
        };
        db.Companies.Add(company);

        // Provision funding account.
        var fundingAccount = await CompanyBankingService.EnsurePreferredAccountAsync(
            db, company.Id, city.CurrencyCode, ct);
        fundingAccount.Balance += founderContributionLocal + ipoRaiseLocal;

        // Debit founder contribution from player's personal USD settlement account.
        await PersonalBankAccountService.DebitTrackedBalanceAsync(db, userId, "USD", StarterFounderContribution, ct);

        // Record the two opening capital events for full auditability.
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BankAccountId = fundingAccount.Id,
            Category = LedgerCategory.FounderContribution,
            Description = city.CurrencyCode == "USD"
                ? $"Founder contribution: {StarterFounderContribution:N0} USD personal investment"
                : $"Founder contribution: {StarterFounderContribution:N0} USD → {founderContributionLocal:N0} {city.CurrencyCode} (FX {fxRate:F4})",
            Amount = founderContributionLocal,
            RecordedAtTick = currentTick,
            RecordedAtUtc = nowUtc,
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BankAccountId = fundingAccount.Id,
            Category = LedgerCategory.IpoRaise,
            Description = city.CurrencyCode == "USD"
                ? $"IPO public shareholder investment: {ipoSelection.RaiseTarget:N0} USD raised ({ipoSelection.FounderOwnershipRatio:P0} founder equity)"
                : $"IPO public shareholder investment: {ipoSelection.RaiseTarget:N0} USD → {ipoRaiseLocal:N0} {city.CurrencyCode} ({ipoSelection.FounderOwnershipRatio:P0} founder equity, FX {fxRate:F4})",
            Amount = ipoRaiseLocal,
            RecordedAtTick = currentTick,
            RecordedAtUtc = nowUtc,
        });

        // Create founder shareholding.
        db.Shareholdings.Add(new Shareholding
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            OwnerPlayerId = userId,
            ShareCount = ipoSelection.FounderShareCount,
        });

        await db.SaveChangesAsync(ct);

        return company;
    }
}
