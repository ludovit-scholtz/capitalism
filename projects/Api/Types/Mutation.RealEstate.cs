using System.Globalization;
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
    /// <summary>Sets or clears the for-sale status and asking price of a building.</summary>
    [Authorize]
    public async Task<Building> SetBuildingForSale(
        SetBuildingForSaleInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.Units)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        if (input.IsForSale)
        {
            // Validate asking price is positive
            if (!input.AskingPrice.HasValue || input.AskingPrice.Value <= 0m)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Asking price must be a positive value.")
                        .SetCode("INVALID_ASKING_PRICE")
                        .Build());
            }

            // Prevent listing a building that is pledged as collateral on an active or overdue loan.
            // Defaulted loans are excluded: a building with a defaulted loan must be sold to repay the debt.
            var isCollateral = await db.Loans.AnyAsync(l =>
                l.CollateralBuildingId == input.BuildingId &&
                (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue));

            if (isCollateral)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("This building is pledged as collateral for an active loan and cannot be listed for sale.")
                        .SetCode("BUILDING_IS_COLLATERAL")
                        .Build());
            }
        }
        else
        {
            // During a missed-payment foreclosure flow, the collateral listing cannot be cancelled.
            var isForeclosureLocked = await db.Loans.AnyAsync(l =>
                l.CollateralBuildingId == input.BuildingId
                && l.RemainingPrincipal > 0m
                && (l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Defaulted)
                && l.MissedPayments > 0);

            if (isForeclosureLocked)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Sale cannot be cancelled because this building is collateral for an unpaid loan.")
                        .SetCode("BUILDING_SALE_LOCKED_BY_UNPAID_COLLATERAL")
                        .Build());
            }
        }

        building.IsForSale = input.IsForSale;
        building.AskingPrice = input.IsForSale ? input.AskingPrice : null;
        building.ListedAtUtc = input.IsForSale ? DateTime.UtcNow : null;

        await db.SaveChangesAsync();
        return building;
    }

    /// <summary>
    /// Permanently destroys a building, releases its lot, and pays an 80% refund to the owner.
    /// Destruction is blocked while the building is collateral for an unpaid loan.
    /// </summary>
    [Authorize]
    public async Task<DestroyBuildingResult> DestroyBuilding(
        DestroyBuildingInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .Include(b => b.Units)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        if (building.DestroyedAtUtc is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building is already destroyed.")
                    .SetCode("BUILDING_ALREADY_DESTROYED")
                    .Build());
        }

        var hasUnpaidCollateralLoan = await db.Loans.AnyAsync(loan =>
            loan.CollateralBuildingId == building.Id
            && loan.RemainingPrincipal > 0m
            && (loan.Status == LoanStatus.Active || loan.Status == LoanStatus.Overdue || loan.Status == LoanStatus.Defaulted));

        if (hasUnpaidCollateralLoan)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building cannot be destroyed while it is collateral for an unpaid loan.")
                    .SetCode("BUILDING_HAS_UNPAID_COLLATERAL_LOAN")
                    .Build());
        }

        var currencyCode = building.City?.CurrencyCode ?? "EUR";
        var fxRates = await FxRateHelper.BuildEurRatesLookupAsync(db, [currencyCode]);
        var cityFxRate = FxRateHelper.GetEurRate(fxRates, currencyCode);

        var propertyValueLocal = WealthCalculator.GetBuildingValue(building) * cityFxRate;
        var unitValueLocal = building.Units.Count * 20_000m * cityFxRate;
        var estimatedMarketValue = decimal.Round(propertyValueLocal + unitValueLocal, 2, MidpointRounding.AwayFromZero);
        var refundAmount = decimal.Round(
            estimatedMarketValue * GameConstants.ForeclosureRefundFraction,
            2,
            MidpointRounding.AwayFromZero);

        var ownerAccount = await CompanyBankingService.EnsurePreferredAccountAsync(db, building.CompanyId, currencyCode);
        ownerAccount.Balance += refundAmount;
        ownerAccount.ConcurrencyToken = Guid.NewGuid();

        var lot = await db.BuildingLots.FirstOrDefaultAsync(l => l.BuildingId == building.Id);
        if (lot is not null)
        {
            lot.OwnerCompanyId = null;
            lot.BuildingId = null;
            lot.ConcurrencyToken = Guid.NewGuid();
        }

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gs => gs.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        building.IsForSale = false;
        building.AskingPrice = null;
        building.ListedAtUtc = null;
        building.DestroyedAtUtc = DateTime.UtcNow;

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = building.CompanyId,
            BankAccountId = ownerAccount.Id,
            BuildingId = building.Id,
            Category = LedgerCategory.BuildingSale,
            Description = $"Building destroyed refund for '{building.Name}'",
            Amount = refundAmount,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        db.BuildingDestructionRecords.Add(new BuildingDestructionRecord
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            BuildingName = building.Name,
            LoanId = null,
            CityId = building.CityId,
            OwnerCompanyId = building.CompanyId,
            OriginalPropertyValue = estimatedMarketValue,
            CompensationPaid = refundAmount,
            DestructionTickCount = currentTick,
            DestructionReason = BuildingDestructionReason.Other,
            CreatedAtUtc = DateTime.UtcNow,
        });

        PlayerNotificationService.Add(
            db,
            userId,
            PlayerNotificationType.Generic,
            "Building destroyed",
            $"'{building.Name}' was destroyed. {refundAmount.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode} was credited to your account.",
            currentTick,
            building.CompanyId,
            building.Id,
            bankAccountId: ownerAccount.Id);

        await db.SaveChangesAsync();

        return new DestroyBuildingResult
        {
            BuildingId = building.Id,
            BuildingName = building.Name,
            RefundAmount = refundAmount,
            CurrencyCode = currencyCode,
        };
    }

    /// <summary>
    /// Schedules a new rent per m² for an apartment or commercial building.
    /// The change is stored as pending and activates after one in-game day (24 ticks).
    /// </summary>
    [Authorize]
    public async Task<Building> SetRentPerSqm(
        SetRentPerSqmInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or you don't own it.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        if (building.Type != BuildingType.Apartment && building.Type != BuildingType.Commercial)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only apartment and commercial buildings support rent pricing.")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        if (input.RentPerSqm < 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Rent per m² must be a non-negative value.")
                    .SetCode("INVALID_RENT")
                    .Build());
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync()
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Game state not found.")
                    .SetCode("GAME_STATE_NOT_FOUND")
                    .Build());

        // Schedule the rent change – takes effect after one in-game day (24 ticks).
        building.PendingPricePerSqm = input.RentPerSqm;
        building.PendingPriceActivationTick = gameState.CurrentTick + Engine.GameConstants.TicksPerDay;

        await db.SaveChangesAsync();
        return building;
    }

    /// <summary>Purchases a building lot and places a building on it.</summary>
    [Authorize]
    public async Task<PurchaseLotResult> PurchaseLot(
        PurchaseLotInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IServiceScopeFactory scopeFactory)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var company = await db.Companies.FirstOrDefaultAsync(
            c => c.Id == input.CompanyId && c.PlayerId == userId);
        var referralDiscountRate = await GetReferralDiscountRateIfRegisteredAsync(db, userId, httpContextAccessor.HttpContext!.RequestAborted);

        if (company is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found or you don't own it.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());
        }

        var lotPricingPreview = await db.BuildingLots
            .AsNoTracking()
            .Include(candidate => candidate.City)
            .FirstOrDefaultAsync(candidate => candidate.Id == input.LotId);
        var cityCurrencyCode = lotPricingPreview?.City?.CurrencyCode ?? "EUR";
        var fundingAccountPreview = await CompanyBankingService.EnsurePreferredAccountAsync(db, company.Id, cityCurrencyCode);
        var balanceBeforePurchaseAttempt = fundingAccountPreview.Balance;

        var (lot, building) = await PrepareLotPurchaseAsync(
            db,
            company,
            input.LotId,
            input.BuildingType,
            input.BuildingName,
            Engine.GameConstants.PowerDemandMw(input.BuildingType, 1),
            DateTime.UtcNow,
            powerPlantType: input.PowerPlantType,
            applyConstructionDelay: true,
            validateDestinationCurrency: true,
            purchaseDiscountRate: referralDiscountRate);

        // Validate and apply media house channel type.
        if (input.BuildingType == BuildingType.MediaHouse)
        {
            if (string.IsNullOrEmpty(input.MediaType) || !Data.Entities.MediaType.All.Contains(input.MediaType))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"A valid mediaType (NEWSPAPER, RADIO, TV) is required for media house buildings. Received: '{input.MediaType}'.")
                        .SetCode("INVALID_MEDIA_TYPE")
                        .Build());
            }
            building.MediaType = input.MediaType;
        }

        // Pre-initialize default interest rates for bank buildings so the owner
        // can see and edit them from the bank management page immediately.
        if (input.BuildingType == BuildingType.Bank)
        {
            building.DepositInterestRatePercent ??= 3m;
            building.LendingInterestRatePercent ??= 8m;
        }

        try
        {
            var currentTick = await db.GameStates
                .AsNoTracking()
                .Select(state => state.CurrentTick)
                .FirstOrDefaultDeterministicAsync();
            await LandService.EnsureMinimumAvailableLotsAsync(db, currentTick, [lot.CityId]);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // In high-contention races, the losing request can hit concurrency after balance mutation.
            // Restore the pre-attempt balance so only the winning company is charged.
            await using var refundScope = scopeFactory.CreateAsyncScope();
            var refundDb = refundScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var fundingAccount = await CompanyBankingService.EnsurePreferredAccountAsync(refundDb, company.Id, cityCurrencyCode);
            if (fundingAccount.Balance < balanceBeforePurchaseAttempt)
            {
                fundingAccount.Balance = balanceBeforePurchaseAttempt;
                await refundDb.SaveChangesAsync();
            }

            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This lot has already been purchased.")
                    .SetCode("LOT_ALREADY_OWNED")
                    .Build());
        }

        return new PurchaseLotResult
        {
            Lot = lot,
            Building = building,
            Company = company
        };
    }

    /// <summary>
    /// Marks the first-sale onboarding milestone as completed for the current player.
    /// Validates backend-authoritative conditions: the player must have a sales shop
    /// created during onboarding with at least one configured PUBLIC_SALES unit.
    /// Idempotent once the milestone has been granted.
    /// </summary>
    [Authorize]
    public async Task<Player> CompleteFirstSaleMilestone(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players
            .Include(p => p.Companies)
            .FirstOrDefaultAsync(p => p.Id == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        // Idempotent: already completed
        if (player.OnboardingFirstSaleCompletedAtUtc is not null)
        {
            return player;
        }

        if (player.OnboardingShopBuildingId is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("No sales shop was found for this onboarding milestone. Please complete the onboarding setup first.")
                    .SetCode("SHOP_NOT_FOUND")
                    .Build());
        }

        // Verify the shop belongs to this player and has a configured public-sales unit
        var shopBuilding = await db.Buildings
            .Include(b => b.Units)
            .FirstOrDefaultAsync(b => b.Id == player.OnboardingShopBuildingId);

        if (shopBuilding is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Sales shop building not found.")
                    .SetCode("SHOP_NOT_FOUND")
                    .Build());
        }

        // Verify ownership via the company chain
        var ownsShop = await db.Companies
            .AnyAsync(c => c.Id == shopBuilding.CompanyId && c.PlayerId == userId);

        if (!ownsShop)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You do not own this sales shop.")
                    .SetCode("SHOP_NOT_FOUND")
                    .Build());
        }

        // Check backend-authoritative condition: shop must have a PUBLIC_SALES unit with a price set
        var hasSalesUnit = shopBuilding.Units.Any(u =>
            string.Equals(u.UnitType, UnitType.PublicSales, StringComparison.Ordinal)
            && u.MinPrice > 0);

        if (!hasSalesUnit)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Your sales shop is not yet configured. Please set up a public sales unit with a selling price and return here to complete the milestone.")
                    .SetCode("SHOP_NOT_CONFIGURED")
                    .Build());
        }

        // Check backend-authoritative condition: a real public sale must have occurred in the simulation
        var hasRealSale = await db.PublicSalesRecords
            .AnyAsync(r => r.BuildingId == shopBuilding.Id && r.QuantitySold > 0m);

        if (!hasRealSale)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Your shop has not made its first real sale yet. Wait for the simulation to process the next tick and try again after your shop has sold at least one item.")
                    .SetCode("FIRST_SALE_NOT_RECORDED")
                    .Build());
        }

        player.OnboardingFirstSaleCompletedAtUtc = DateTime.UtcNow;
        player.OnboardingShopBuildingId = null;
        await db.SaveChangesAsync();

        return player;
    }
}
