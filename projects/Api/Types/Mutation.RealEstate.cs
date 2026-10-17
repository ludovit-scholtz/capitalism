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
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.Units)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw objectAuthorization.Deny(
                userId,
                "building",
                input.BuildingId,
                building is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotOwnedReason);
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
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .Include(b => b.Units)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw objectAuthorization.Deny(
                userId,
                "building",
                input.BuildingId,
                building is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotOwnedReason);
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
        building.DestroyedReason = BuildingDestructionReason.PlayerDemolished;
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
            DestructionReason = BuildingDestructionReason.PlayerDemolished,
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

    private static async Task<bool> IsPledgedCollateralWithUnpaidLoanAsync(AppDbContext db, Guid buildingId)
    {
        return await db.Loans.AnyAsync(loan =>
            loan.CollateralBuildingId == buildingId
            && loan.RemainingPrincipal > 0m
            && (loan.Status == LoanStatus.Active || loan.Status == LoanStatus.Overdue || loan.Status == LoanStatus.Defaulted));
    }
}
