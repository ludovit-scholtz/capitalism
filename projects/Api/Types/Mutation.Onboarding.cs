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
    /// <summary>
    /// Completes the onboarding process: creates a company, a factory, and a sales shop
    /// with default unit configurations for the chosen industry.
    /// </summary>
    [Authorize]
    public async Task<OnboardingResult> CompleteOnboarding(
        OnboardingInput input,
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
        var hasActiveProSubscription = ProductAccessService.HasActiveProSubscription(player, nowUtc);

        // Validate company name
        var trimmedCompanyName = input.CompanyName?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedCompanyName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company name cannot be empty.")
                    .SetCode("INVALID_COMPANY_NAME")
                    .Build());
        }

        // Validate industry
        if (!Industry.StarterIndustries.Contains(input.Industry))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Invalid starter industry: {input.Industry}")
                    .SetCode("INVALID_INDUSTRY")
                    .Build());
        }

        // Electronics and Construction are Pro-only starter industries.
        if (Industry.ProOnlyStarterIndustries.Contains(input.Industry) && !hasActiveProSubscription)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This industry starter path requires a Pro subscription.")
                    .SetCode("PRO_SUBSCRIPTION_REQUIRED")
                    .Build());
        }

        // Validate city
        var city = await db.Cities.FindAsync(input.CityId);
        if (city is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("City not found.")
                    .SetCode("CITY_NOT_FOUND")
                    .Build());
        }

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();
        await LandService.EnsureMinimumAvailableLotsAsync(db, currentTick, [city.Id]);

        // Validate product
        var product = await db.ProductTypes
            .Include(candidate => candidate.Recipes)
            .ThenInclude(recipe => recipe.ResourceType)
            .Include(candidate => candidate.Recipes)
            .ThenInclude(recipe => recipe.InputProductType)
            .FirstOrDefaultAsync(candidate => candidate.Id == input.ProductTypeId);
        if (product is null || product.Industry != input.Industry || !IsStarterOnboardingProduct(product))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Product not found, doesn't belong to selected industry, or is not available for onboarding.")
                    .SetCode("INVALID_PRODUCT")
                    .Build());
        }

        if (!ProductAccessService.IsUnlockedForPlayer(product, hasActiveProSubscription))
        {
            throw ProductAccessService.CreateProAccessException(product.Name);
        }

        var starterResourceId = product.Recipes
            .Where(recipe => recipe.InputProductTypeId is null && recipe.ResourceTypeId is not null)
            .Select(recipe => recipe.ResourceTypeId)
            .FirstOrDefault();
        if (starterResourceId is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The selected onboarding product is missing a starter raw-material recipe.")
                    .SetCode("INVALID_PRODUCT")
                    .Build());
        }

        var personalUsdBalance = await EnsureStarterFounderFundingAsync(db, player, httpContextAccessor.HttpContext!.RequestAborted);
        var referralDiscountRate = await EnsureReferralRegistrationAndGetDiscountRateAsync(db, player, httpContextAccessor.HttpContext!.RequestAborted);

        // Create company
        if (personalUsdBalance < StarterFounderContribution)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You do not have enough personal cash to fund the founder contribution.")
                    .SetCode("INSUFFICIENT_PERSONAL_FUNDS")
                    .Build());
        }

        var ipoSelection = ResolveStarterIpoSelection(null);

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
            FoundedAtTick = currentTick
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

        var factoryLotId = await FindCompatibleAvailableLotIdAsync(db, city.Id, BuildingType.Factory);
        var (_, factory) = await PrepareLotPurchaseAsync(
            db,
            company,
            factoryLotId,
            BuildingType.Factory,
            $"{trimmedCompanyName} Factory",
            Engine.GameConstants.PowerDemandMw(BuildingType.Factory, 1),
            nowUtc,
            city.Id,
            purchaseDiscountRate: referralDiscountRate,
            skipCityUnlockValidation: true);
        ConfigureStarterFactory(db, factory, product, starterResourceId.Value, fxRate);

        var shopLotId = await FindCompatibleAvailableLotIdAsync(db, city.Id, BuildingType.SalesShop);
        var (_, shop) = await PrepareLotPurchaseAsync(
            db,
            company,
            shopLotId,
            BuildingType.SalesShop,
            $"{trimmedCompanyName} Shop",
            Engine.GameConstants.PowerDemandMw(BuildingType.SalesShop, 1),
            nowUtc,
            city.Id,
            purchaseDiscountRate: referralDiscountRate,
            skipCityUnlockValidation: true);
        AddStarterShop(db, company.Id, shop.Id, product, fxRate);

        // Mark onboarding as completed for this player
        player.OnboardingCompletedAtUtc = nowUtc;
        player.OnboardingShopBuildingId = shop.Id;
        ClearOnboardingProgress(player);

        await LandService.EnsureMinimumAvailableLotsAsync(db, currentTick, [city.Id]);
        await db.SaveChangesAsync();

        return new OnboardingResult
        {
            Company = company,
            Factory = factory,
            SalesShop = shop,
            SelectedProduct = product,
            CityCurrencyCode = city.CurrencyCode
        };
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
