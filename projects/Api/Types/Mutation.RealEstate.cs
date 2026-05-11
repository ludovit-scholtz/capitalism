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
    /// <summary>
    /// Sets or clears the for-sale status and asking price of a building.
    /// API-key callers can mutate only buildings owned by the authenticated principal.
    /// </summary>
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

            var marketValuation = await BuildingMarketValuationCalculator.CalculateAsync(
                db,
                building,
                httpContextAccessor.HttpContext!.RequestAborted);
            if (input.AskingPrice.Value < marketValuation.MinimumSalePrice)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage(
                            $"Asking price must be at least {marketValuation.MinimumSalePrice:F2} {marketValuation.CurrencyCode} (70% of market value {marketValuation.TotalValue:F2} {marketValuation.CurrencyCode}).")
                        .SetCode("ASKING_PRICE_BELOW_MINIMUM")
                        .Build());
            }

            var isCollateralLocked = await IsCollateralLockedAtCommitAsync(db, input.BuildingId);
            if (isCollateralLocked)
            {
                LoanCollateralSecurityAuditLogger.Add(
                    db,
                    userId,
                    action: "BUILDING_SALE",
                    reason: "BUILDING_LOCKED_AS_COLLATERAL",
                    buildingId: input.BuildingId);
                await db.SaveChangesAsync();
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Building is locked as loan collateral.")
                        .SetCode("BUILDING_LOCKED_AS_COLLATERAL")
                        .Build());
            }
        }
        else
        {
            var isCollateralLocked = await IsCollateralLockedAtCommitAsync(db, input.BuildingId);
            if (isCollateralLocked)
            {
                LoanCollateralSecurityAuditLogger.Add(
                    db,
                    userId,
                    action: "BUILDING_SALE",
                    reason: "BUILDING_LOCKED_AS_COLLATERAL",
                    buildingId: input.BuildingId);
                await db.SaveChangesAsync();
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Building is locked as loan collateral.")
                        .SetCode("BUILDING_LOCKED_AS_COLLATERAL")
                        .Build());
            }
        }

        building.IsForSale = input.IsForSale;
        building.AskingPrice = input.IsForSale ? input.AskingPrice : null;
        building.ListedAtUtc = input.IsForSale ? DateTime.UtcNow : null;
        building.ConcurrencyToken = Guid.NewGuid();

        await db.SaveChangesAsync();
        return building;
    }

    /// <summary>
    /// Permanently destroys a building, releases its lot, and pays an 80% refund to the owner.
    /// Destruction is blocked while the building is collateral for an unpaid loan.
    /// API-key callers can destroy only buildings owned by the authenticated principal.
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

        var hasCollateralLock = await IsCollateralLockedAtCommitAsync(db, building.Id);
        if (hasCollateralLock)
        {
            LoanCollateralSecurityAuditLogger.Add(
                db,
                userId,
                action: "BUILDING_DESTROY",
                reason: "BUILDING_LOCKED_AS_COLLATERAL",
                buildingId: building.Id);
            await db.SaveChangesAsync();
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building is locked as loan collateral.")
                    .SetCode("BUILDING_LOCKED_AS_COLLATERAL")
                    .Build());
        }

        var marketValuation = await BuildingMarketValuationCalculator.CalculateAsync(
            db,
            building,
            httpContextAccessor.HttpContext!.RequestAborted);
        var estimatedMarketValue = marketValuation.TotalValue;
        var refundAmount = decimal.Round(
            estimatedMarketValue * GameConstants.ForeclosureRefundFraction,
            2,
            MidpointRounding.AwayFromZero);

        var ownerAccount = await CompanyBankingService.EnsurePreferredAccountAsync(db, building.CompanyId, marketValuation.CurrencyCode);
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
        building.ConcurrencyToken = Guid.NewGuid();

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
            $"'{building.Name}' was destroyed. {refundAmount.ToString("N2", CultureInfo.InvariantCulture)} {marketValuation.CurrencyCode} was credited to your account.",
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
            CurrencyCode = marketValuation.CurrencyCode,
        };
    }

    private static async Task<bool> IsCollateralLockedAtCommitAsync(AppDbContext db, Guid buildingId)
    {
        return await db.Loans.AnyAsync(loan =>
            loan.CollateralBuildingId == buildingId
            && loan.RemainingPrincipal > 0m
            && (loan.Status == LoanStatus.Active || loan.Status == LoanStatus.Overdue));
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
