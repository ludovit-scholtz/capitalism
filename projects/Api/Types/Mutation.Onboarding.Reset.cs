using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    [Authorize]
    public async Task<bool> ResetOnboardingProgress(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FindAsync(userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        if (player.OnboardingCompletedAtUtc is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You have already completed onboarding.")
                    .SetCode("ONBOARDING_ALREADY_COMPLETED")
                    .Build());
        }

        var ownedCompanies = await db.Companies
            .Where(company => company.PlayerId == userId)
            .ToListAsync();

        Company? onboardingCompany = null;
        if (player.OnboardingCompanyId.HasValue)
        {
            onboardingCompany = ownedCompanies.FirstOrDefault(company => company.Id == player.OnboardingCompanyId.Value);
        }

        if (onboardingCompany is null && ownedCompanies.Count == 1)
        {
            onboardingCompany = ownedCompanies[0];
        }

        if (onboardingCompany is null && ownedCompanies.Count > 1)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Multiple companies exist for this player. Reset onboarding is unavailable.")
                    .SetCode("ONBOARDING_RESET_NOT_AVAILABLE")
                    .Build());
        }

        if (onboardingCompany is not null)
        {
            var companyBankAccounts = await db.BankAccounts
                .Where(account => account.CompanyId == onboardingCompany.Id)
                .ToListAsync();
            if (companyBankAccounts.Count > 0)
            {
                db.BankAccounts.RemoveRange(companyBankAccounts);
            }

            var companyLots = await db.BuildingLots
                .Where(lot => lot.OwnerCompanyId == onboardingCompany.Id)
                .ToListAsync();
            foreach (var companyLot in companyLots)
            {
                companyLot.OwnerCompanyId = null;
                companyLot.BuildingId = null;
            }

            var affectedPlayers = await db.Players
                .Where(candidate => candidate.ActiveCompanyId == onboardingCompany.Id)
                .ToListAsync();
            foreach (var affectedPlayer in affectedPlayers)
            {
                affectedPlayer.ActiveCompanyId = null;
                affectedPlayer.ActiveAccountType = AccountContextType.Person;
            }

            db.Companies.Remove(onboardingCompany);
        }

        ClearIncompleteOnboardingProgress(player);
        player.ActiveCompanyId = null;
        player.ActiveAccountType = AccountContextType.Person;

        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Starts the lot-based onboarding journey by creating the first company and purchasing its factory lot.
    /// The player can later resume at the sales-shop step using the stored onboarding progress metadata.
    /// </summary>
    [Authorize]
    public async Task<OnboardingStartResult> StartOnboardingCompany(
        StartOnboardingCompanyInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var nowUtc = DateTime.UtcNow;
        var player = await db.Players.FindAsync(userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        if (player.OnboardingCompletedAtUtc is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You have already completed onboarding.")
                    .SetCode("ONBOARDING_ALREADY_COMPLETED")
                    .Build());
        }

        if (player.OnboardingCurrentStep is not null || player.OnboardingCompanyId is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Onboarding is already in progress.")
                    .SetCode("ONBOARDING_ALREADY_IN_PROGRESS")
                    .Build());
        }

        if (await db.Companies.AnyAsync(company => company.PlayerId == userId))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You already have a company. Resume or finish onboarding instead.")
                    .SetCode("ONBOARDING_ALREADY_IN_PROGRESS")
                    .Build());
        }

        var trimmedCompanyName = input.CompanyName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedCompanyName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company name cannot be empty.")
                    .SetCode("INVALID_COMPANY_NAME")
                    .Build());
        }

        if (!Industry.StarterIndustries.Contains(input.Industry))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Invalid starter industry: {input.Industry}")
                    .SetCode("INVALID_INDUSTRY")
                    .Build());
        }

        // Electronics and Construction are Pro-only starter industries.
        var hasProForStart = ProductAccessService.HasActiveProSubscription(player, nowUtc);
        if (Industry.ProOnlyStarterIndustries.Contains(input.Industry) && !hasProForStart)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This industry starter path requires a Pro subscription.")
                    .SetCode("PRO_SUBSCRIPTION_REQUIRED")
                    .Build());
        }

        var city = await db.Cities.FindAsync(input.CityId);
        if (city is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("City not found.")
                    .SetCode("CITY_NOT_FOUND")
                    .Build());
        }

        var personalUsdBalance = await EnsureStarterFounderFundingAsync(db, player, httpContextAccessor.HttpContext!.RequestAborted);
        var referralDiscountRate = await EnsureReferralRegistrationAndGetDiscountRateAsync(db, player, httpContextAccessor.HttpContext!.RequestAborted);

        if (personalUsdBalance < StarterFounderContribution)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You do not have enough personal cash to fund the founder contribution.")
                    .SetCode("INSUFFICIENT_PERSONAL_FUNDS")
                    .Build());
        }

        var ipoSelection = ResolveStarterIpoSelection(input.IpoRaiseTarget);

        // Scale founder contribution and IPO raise from USD to local city currency.
        var fxRate = await Query.ComputeForexRateAsync(db, "USD", city.CurrencyCode);
        var founderContributionLocal = Math.Round(StarterFounderContribution * fxRate, 2);
        var ipoRaiseLocal = Math.Round(ipoSelection.RaiseTarget * fxRate, 2);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = userId,
            Name = trimmedCompanyName,
            TotalSharesIssued = DefaultCompanyShareCount,
            DividendPayoutRatio = DefaultDividendPayoutRatio,
            FoundedAtUtc = nowUtc,
            FoundedAtTick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultDeterministicAsync()
        };
        db.Companies.Add(company);
        var fundingAccount = await CompanyBankingService.EnsurePreferredAccountAsync(
            db,
            company.Id,
            city.CurrencyCode,
            httpContextAccessor.HttpContext!.RequestAborted);
        fundingAccount.Balance += founderContributionLocal + ipoRaiseLocal;
        await PersonalBankAccountService.DebitTrackedBalanceAsync(db, player.Id, "USD", StarterFounderContribution, httpContextAccessor.HttpContext!.RequestAborted);

        // Record the two opening capital events in the company ledger so the bank statement
        // shows a clear audit trail: government starter deposit and IPO public investment.
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BankAccountId = fundingAccount.Id,
            Category = LedgerCategory.FounderContribution,
            Description = city.CurrencyCode == "USD"
                ? $"Founder contribution: {StarterFounderContribution:N0} USD government starter deposit"
                : $"Founder contribution: {StarterFounderContribution:N0} USD → {founderContributionLocal:N0} {city.CurrencyCode} (FX {fxRate:F4})",
            Amount = founderContributionLocal,
            RecordedAtTick = company.FoundedAtTick,
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
            RecordedAtTick = company.FoundedAtTick,
            RecordedAtUtc = nowUtc,
        });

        player.ActiveAccountType = AccountContextType.Company;
        player.ActiveCompanyId = company.Id;
        db.Shareholdings.Add(new Shareholding
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            OwnerPlayerId = userId,
            ShareCount = ipoSelection.FounderShareCount,
        });

        var (factoryLot, factory) = await PrepareLotPurchaseAsync(
            db,
            company,
            input.FactoryLotId,
            BuildingType.Factory,
            $"{trimmedCompanyName} Factory",
            Engine.GameConstants.PowerDemandMw(BuildingType.Factory, 1),
            nowUtc,
            input.CityId,
            purchaseDiscountRate: referralDiscountRate,
            skipCityUnlockValidation: true);

        AddStarterFactoryShell(db, factory.Id);

        player.OnboardingCurrentStep = OnboardingProgressStep.ShopSelection;
        player.OnboardingIndustry = input.Industry;
        player.OnboardingCityId = input.CityId;
        player.OnboardingCompanyId = company.Id;
        player.OnboardingFactoryLotId = factoryLot.Id;
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        try
        {
            await LandService.EnsureMinimumAvailableLotsAsync(db, currentTick, [input.CityId]);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This lot has already been purchased.")
                    .SetCode("LOT_ALREADY_OWNED")
                    .Build());
        }

        return new OnboardingStartResult
        {
            Company = company,
            Factory = factory,
            FactoryLot = factoryLot,
            NextStep = OnboardingProgressStep.ShopSelection
        };
    }

}
