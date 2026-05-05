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
    /// Finishes the lot-based onboarding journey by selecting the starter product, purchasing the first sales shop,
    /// configuring both buildings, and marking onboarding complete.
    /// </summary>
    [Authorize]
    public async Task<OnboardingResult> FinishOnboarding(
        FinishOnboardingInput input,
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

        if (!string.Equals(player.OnboardingCurrentStep, OnboardingProgressStep.ShopSelection, StringComparison.Ordinal)
            || player.OnboardingCompanyId is null
            || player.OnboardingIndustry is null
            || player.OnboardingCityId is null
            || player.OnboardingFactoryLotId is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("No onboarding progress was found to resume.")
                    .SetCode("ONBOARDING_NOT_IN_PROGRESS")
                    .Build());
        }

        var company = await db.Companies.FirstOrDefaultAsync(
            candidate => candidate.Id == player.OnboardingCompanyId && candidate.PlayerId == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found or you don't own it.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());

        var factoryLot = await db.BuildingLots.FirstOrDefaultAsync(lot => lot.Id == player.OnboardingFactoryLotId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Factory lot not found.")
                    .SetCode("LOT_NOT_FOUND")
                    .Build());

        var factory = await db.Buildings
            .Include(building => building.Units)
            .FirstOrDefaultAsync(building => building.Id == factoryLot.BuildingId && building.CompanyId == company.Id)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Factory not found.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());

        var hasActiveProSubscription = ProductAccessService.HasActiveProSubscription(player, nowUtc);
        var product = await db.ProductTypes
            .Include(candidate => candidate.Recipes)
            .ThenInclude(recipe => recipe.ResourceType)
            .Include(candidate => candidate.Recipes)
            .ThenInclude(recipe => recipe.InputProductType)
            .FirstOrDefaultAsync(candidate => candidate.Id == input.ProductTypeId);

        if (product is null || product.Industry != player.OnboardingIndustry || !IsStarterOnboardingProduct(product))
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

        var onboardingCityId = player.OnboardingCityId!.Value;

        // Look up the FX rate for the onboarding city's currency to normalize starter prices.
        var onboardingCityCurrencyCode = await db.Cities
            .AsNoTracking()
            .Where(city => city.Id == onboardingCityId)
            .Select(city => city.CurrencyCode)
            .FirstOrDefaultDeterministicAsync() ?? "EUR";
        var finishFxRate = await Query.ComputeForexRateAsync(db, "EUR", onboardingCityCurrencyCode);

        var (_, shop) = await PrepareLotPurchaseAsync(
            db,
            company,
            input.ShopLotId,
            BuildingType.SalesShop,
            $"{company.Name} Shop",
            Engine.GameConstants.PowerDemandMw(BuildingType.SalesShop, 1),
            nowUtc,
            onboardingCityId);

        ConfigureStarterFactory(db, factory, product, starterResourceId.Value, finishFxRate);
        AddStarterShop(db, company.Id, shop.Id, product, finishFxRate);

        player.OnboardingCompletedAtUtc = nowUtc;
        player.OnboardingShopBuildingId = shop.Id;
        player.ActiveAccountType = AccountContextType.Company;
        player.ActiveCompanyId = company.Id;
        ClearOnboardingProgress(player);
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        try
        {
            await LandService.EnsureMinimumAvailableLotsAsync(db, currentTick, [onboardingCityId]);
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

        return new OnboardingResult
        {
            Company = company,
            Factory = factory,
            SalesShop = shop,
            SelectedProduct = product,
            CityCurrencyCode = onboardingCityCurrencyCode
        };
    }

    private async Task<decimal> EnsureStarterFounderFundingAsync(
        AppDbContext db,
        Player player,
        CancellationToken cancellationToken)
    {
        if (player.OnboardingCompletedAtUtc is not null
            || !string.IsNullOrEmpty(player.OnboardingCurrentStep)
            || player.OnboardingCompanyId is not null)
        {
            return await PersonalBankAccountService.GetTrackedBalanceAsync(db, player.Id, "USD", cancellationToken);
        }

        // Starter funding is guaranteed only before the first company is created.
        if (await db.Companies.AsNoTracking().AnyAsync(company => company.PlayerId == player.Id, cancellationToken))
        {
            return await PersonalBankAccountService.GetTrackedBalanceAsync(db, player.Id, "USD", cancellationToken);
        }

        var settlementAccount = await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(db, player, cancellationToken);
        if (settlementAccount.Balance < StarterFounderContribution)
        {
            settlementAccount.Balance = StarterFounderContribution;
        }

        return settlementAccount.Balance;
    }
}
