using System.Globalization;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Allows a buyer to submit a purchase offer on a building listed for sale.
    /// The buyer must own the target company and the company must have sufficient funds.
    /// The buyer cannot make an offer on their own building.
    /// </summary>
    [Authorize]
    public async Task<BuildingSaleOffer> MakeOfferOnBuilding(
        MakeOfferOnBuildingInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .ThenInclude(c => c.Player)
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || !building.IsForSale || !building.AskingPrice.HasValue)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building not found or not listed for sale.")
                    .SetCode("BUILDING_NOT_FOR_SALE")
                    .Build());
        }

        if (building.Company.PlayerId == userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You cannot make an offer on your own building.")
                    .SetCode("CANNOT_BUY_OWN_BUILDING")
                    .Build());
        }

        if (input.OfferedPrice <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Offered price must be positive.")
                    .SetCode("INVALID_PRICE")
                    .Build());
        }

        var buyerCompany = await db.Companies
            .Include(c => c.BankAccounts)
            .FirstOrDefaultAsync(c => c.Id == input.BuyerCompanyId && c.PlayerId == userId);

        if (buyerCompany is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found or you don't own it.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());
        }

        var currencyCode = building.City.CurrencyCode;
        var availableBalance = CompanyBankingService.GetCurrencyBalance(buyerCompany.BankAccounts, currencyCode);
        if (availableBalance < input.OfferedPrice)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(
                        $"Insufficient funds. Available: {availableBalance.ToString("F2", CultureInfo.InvariantCulture)} {currencyCode}.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        // Prevent duplicate pending offers from the same buyer on the same building
        var existingPendingOffer = await db.BuildingSaleOffers
            .AnyAsync(o => o.BuildingId == building.Id
                && o.BuyerCompanyId == buyerCompany.Id
                && o.Status == BuildingSaleOfferStatus.Pending);

        if (existingPendingOffer)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You already have a pending offer on this building. Withdraw or wait for resolution before submitting a new offer.")
                    .SetCode("DUPLICATE_OFFER")
                    .Build());
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();

        var offer = new BuildingSaleOffer
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            BuyerPlayerId = userId,
            BuyerCompanyId = buyerCompany.Id,
            OfferedPrice = input.OfferedPrice,
            NegotiationNote = input.NegotiationNote?.Trim(),
            Status = BuildingSaleOfferStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BuildingSaleOffers.Add(offer);

        // Notify the seller
        var sellerPlayer = building.Company.Player;
        db.PlayerNotifications.Add(new PlayerNotification
        {
            Id = Guid.NewGuid(),
            PlayerId = sellerPlayer.Id,
            Type = PlayerNotificationType.BuildingOfferReceived,
            Title = "New offer on your building",
            Message = $"{buyerCompany.Name} offered {input.OfferedPrice.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode} for {building.Name}.",
            BuildingId = building.Id,
            CreatedAtTick = gameState?.CurrentTick ?? 0,
        });

        await db.SaveChangesAsync();

        // Reload with navigation for response
        offer.Building = building;
        offer.BuyerPlayer = await db.Players.FindAsync(userId) ?? new Player { DisplayName = "Unknown" };
        offer.BuyerCompany = buyerCompany;

        return offer;
    }

    /// <summary>
    /// Seller accepts an offer: atomically debits the buyer's account,
    /// credits the seller's account, transfers building ownership, and writes ledger entries.
    /// All pending offers on the building are rejected after the accepted transfer.
    /// </summary>
    [Authorize]
    public async Task<AcceptBuildingOfferResult> AcceptBuildingOffer(
        AcceptBuildingOfferInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var offer = await db.BuildingSaleOffers
            .Include(o => o.Building)
            .ThenInclude(b => b.Company)
            .ThenInclude(c => c.BankAccounts)
            .Include(o => o.Building)
            .ThenInclude(b => b.City)
            .Include(o => o.BuyerPlayer)
            .Include(o => o.BuyerCompany)
            .ThenInclude(c => c.BankAccounts)
            .FirstOrDefaultAsync(o => o.Id == input.OfferId);

        if (offer is null || offer.Status != BuildingSaleOfferStatus.Pending)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Offer not found or no longer pending.")
                    .SetCode("OFFER_NOT_FOUND")
                    .Build());
        }

        var building = offer.Building;
        var sellerCompany = building.Company;

        if (sellerCompany.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You don't own this building.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        if (!building.IsForSale)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This building is no longer listed for sale.")
                    .SetCode("BUILDING_NOT_FOR_SALE")
                    .Build());
        }

        var city = building.City;
        var currencyCode = city.CurrencyCode;
        var salePrice = offer.OfferedPrice;

        // Validate buyer funds
        var buyerBalance = CompanyBankingService.GetCurrencyBalance(
            offer.BuyerCompany.BankAccounts, currencyCode);
        if (buyerBalance < salePrice)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(
                        $"Buyer has insufficient funds. Available: {buyerBalance.ToString("F2", CultureInfo.InvariantCulture)} {currencyCode}.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0;
        var nowUtc = DateTime.UtcNow;
        var sellerNetProceeds = salePrice;
        BankAccount? lenderDebtCreditedAccount = null;
        decimal debtPaidInLoanCurrency = 0m;
        decimal debtPaidFromSale = 0m;
        string? debtCurrencyCode = null;
        var usesForcedSaleFx = false;

        // If this building is collateral for an unpaid overdue/defaulted loan, settle that debt from sale proceeds first.
        var collateralLoan = await db.Loans
            .Include(l => l.BankBuilding)
            .ThenInclude(b => b.City)
            .Include(l => l.LoanOffer)
            .FirstOrDefaultAsync(l =>
                l.CollateralBuildingId == building.Id
                && l.RemainingPrincipal > 0m
                && (l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Defaulted));

        if (collateralLoan is not null)
        {
            debtCurrencyCode = collateralLoan.BankBuilding.City?.CurrencyCode ?? currencyCode;
            var fxRates = await FxRateHelper.BuildEurRatesLookupAsync(db, [currencyCode, debtCurrencyCode]);
            var remainingDebtInSaleCurrency = decimal.Round(
                FxRateHelper.ConvertAmount(
                    collateralLoan.RemainingPrincipal,
                    debtCurrencyCode,
                    currencyCode,
                    fxRates),
                2,
                MidpointRounding.AwayFromZero);

            debtPaidFromSale = Math.Min(salePrice, remainingDebtInSaleCurrency);
            usesForcedSaleFx = !string.Equals(debtCurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase);
            debtPaidInLoanCurrency = debtPaidFromSale >= remainingDebtInSaleCurrency
                ? collateralLoan.RemainingPrincipal
                : decimal.Round(
                    FxRateHelper.ConvertAmount(
                        debtPaidFromSale,
                        currencyCode,
                        debtCurrencyCode,
                        fxRates),
                    2,
                    MidpointRounding.AwayFromZero);
            debtPaidInLoanCurrency = Math.Min(collateralLoan.RemainingPrincipal, debtPaidInLoanCurrency);

            if (debtPaidInLoanCurrency > 0m)
            {
                lenderDebtCreditedAccount = await CompanyBankingService.EnsurePreferredAccountAsync(
                    db,
                    collateralLoan.LenderCompanyId,
                    debtCurrencyCode);
                lenderDebtCreditedAccount.Balance += debtPaidInLoanCurrency;
                lenderDebtCreditedAccount.ConcurrencyToken = Guid.NewGuid();
            }

            collateralLoan.RemainingPrincipal = Math.Max(0m, collateralLoan.RemainingPrincipal - debtPaidInLoanCurrency);
            collateralLoan.CollateralBuildingId = null;
            if (collateralLoan.RemainingPrincipal <= 0m)
            {
                collateralLoan.RemainingPrincipal = 0m;
                collateralLoan.Status = LoanStatus.Repaid;
                collateralLoan.DefaultedAtTick = null;
                collateralLoan.ClosedAtUtc = nowUtc;
                collateralLoan.MissedPayments = 0;
                collateralLoan.AccumulatedPenalty = 0m;
                collateralLoan.LoanOffer.UsedCapacity = Math.Max(0m, collateralLoan.LoanOffer.UsedCapacity - collateralLoan.OriginalPrincipal);
            }
            else
            {
                collateralLoan.Status = LoanStatus.Defaulted;
                collateralLoan.ClosedAtUtc = nowUtc;
            }

            sellerNetProceeds = Math.Max(0m, salePrice - debtPaidFromSale);
        }

        // Debit buyer
        if (!CompanyBankingService.TryDebit(offer.BuyerCompany.BankAccounts, salePrice, currencyCode))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Failed to debit buyer account.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        // Credit seller – find or create preferred account in the building currency
        var sellerAccount = await CompanyBankingService.EnsurePreferredAccountAsync(db, sellerCompany.Id, currencyCode);
        sellerAccount.Balance += sellerNetProceeds;
        sellerAccount.ConcurrencyToken = Guid.NewGuid();
        var debtSettlementDescription = debtPaidInLoanCurrency > 0m
            ? usesForcedSaleFx
                ? $"Forced-sale FX swap: {debtPaidFromSale.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode} → {debtPaidInLoanCurrency.ToString("N2", CultureInfo.InvariantCulture)} {debtCurrencyCode} for loan settlement of {building.Name}"
                : $"Collateral debt settled from sale proceeds for {building.Name}"
            : null;

        // Transfer building ownership
        building.CompanyId = offer.BuyerCompanyId;
        building.IsForSale = false;
        building.AskingPrice = null;

        // Mark this offer as accepted
        offer.Status = BuildingSaleOfferStatus.Accepted;
        offer.ResolvedAtUtc = nowUtc;

        // Reject all other pending offers for this building
        var otherOffers = await db.BuildingSaleOffers
            .Include(o => o.BuyerPlayer)
            .Include(o => o.BuyerCompany)
            .Where(o => o.BuildingId == building.Id
                && o.Id != offer.Id
                && o.Status == BuildingSaleOfferStatus.Pending)
            .ToListAsync();

        foreach (var rejected in otherOffers)
        {
            rejected.Status = BuildingSaleOfferStatus.Rejected;
            rejected.ResolvedAtUtc = nowUtc;

            // Notify other buyers of rejection
            db.PlayerNotifications.Add(new PlayerNotification
            {
                Id = Guid.NewGuid(),
                PlayerId = rejected.BuyerPlayerId,
                Type = PlayerNotificationType.BuildingOfferRejected,
                Title = "Your offer was not accepted",
                Message = $"Your offer of {rejected.OfferedPrice.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode} for {building.Name} was not accepted (building sold to another buyer).",
                BuildingId = building.Id,
                CreatedAtTick = currentTick,
            });
        }

        // Ledger: buyer (expense)
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = offer.BuyerCompanyId,
            BuildingId = building.Id,
            Category = LedgerCategory.BuildingAcquisition,
            Description = $"Building acquisition: {building.Name} ({building.Type}) in {city.Name} from {sellerCompany.Name}",
            Amount = -salePrice,
            RecordedAtTick = currentTick,
            RecordedAtUtc = nowUtc,
        });

        if (debtPaidInLoanCurrency > 0m)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = sellerCompany.Id,
                BankAccountId = sellerAccount.Id,
                BuildingId = building.Id,
                Category = LedgerCategory.LoanRepaymentPrincipal,
                Description = debtSettlementDescription!,
                Amount = -(usesForcedSaleFx ? debtPaidFromSale : debtPaidInLoanCurrency),
                RecordedAtTick = currentTick,
                RecordedAtUtc = nowUtc,
            });

            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = collateralLoan!.LenderCompanyId,
                BankAccountId = lenderDebtCreditedAccount?.Id,
                BuildingId = building.Id,
                Category = LedgerCategory.LoanRepaymentPrincipal,
                Description = debtSettlementDescription!,
                Amount = debtPaidInLoanCurrency,
                RecordedAtTick = currentTick,
                RecordedAtUtc = nowUtc,
            });
        }

        // Ledger: seller (income)
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            BankAccountId = sellerAccount.Id,
            BuildingId = building.Id,
            Category = LedgerCategory.BuildingSale,
            Description = $"Building sale: {building.Name} ({building.Type}) in {city.Name} to {offer.BuyerCompany.Name}",
            Amount = salePrice,
            RecordedAtTick = currentTick,
            RecordedAtUtc = nowUtc,
        });

        // Notify seller
        db.PlayerNotifications.Add(new PlayerNotification
        {
            Id = Guid.NewGuid(),
            PlayerId = userId,
            Type = PlayerNotificationType.BuildingSoldSuccessfully,
            Title = "Building sold",
            Message = debtPaidInLoanCurrency > 0m
                ? $"Your building {building.Name} was sold to {offer.BuyerCompany.Name} for {salePrice.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode}. Debt settlement: {debtPaidInLoanCurrency.ToString("N2", CultureInfo.InvariantCulture)} {debtCurrencyCode}. Net proceeds: {sellerNetProceeds.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode}."
                : $"Your building {building.Name} was sold to {offer.BuyerCompany.Name} for {salePrice.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode}.",
            BuildingId = building.Id,
            CreatedAtTick = currentTick,
        });

        // Notify buyer
        db.PlayerNotifications.Add(new PlayerNotification
        {
            Id = Guid.NewGuid(),
            PlayerId = offer.BuyerPlayerId,
            Type = PlayerNotificationType.BuildingOfferAccepted,
            Title = "Offer accepted!",
            Message = $"Your offer of {salePrice.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode} for {building.Name} was accepted. Building transferred to {offer.BuyerCompany.Name}.",
            BuildingId = building.Id,
            CreatedAtTick = currentTick,
        });

        await db.SaveChangesAsync();

        return new AcceptBuildingOfferResult
        {
            Building = building,
            Offer = offer,
        };
    }

    /// <summary>Seller rejects a pending offer.</summary>
    [Authorize]
    public async Task<BuildingSaleOffer> RejectBuildingOffer(
        RejectBuildingOfferInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var offer = await db.BuildingSaleOffers
            .Include(o => o.Building)
            .ThenInclude(b => b.Company)
            .Include(o => o.Building)
            .ThenInclude(b => b.City)
            .Include(o => o.BuyerPlayer)
            .Include(o => o.BuyerCompany)
            .FirstOrDefaultAsync(o => o.Id == input.OfferId);

        if (offer is null || offer.Status != BuildingSaleOfferStatus.Pending)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Offer not found or no longer pending.")
                    .SetCode("OFFER_NOT_FOUND")
                    .Build());
        }

        if (offer.Building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You don't own this building.")
                    .SetCode("BUILDING_NOT_FOUND")
                    .Build());
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var city = offer.Building.City;
        var currencyCode = city.CurrencyCode;

        offer.Status = BuildingSaleOfferStatus.Rejected;
        offer.ResolvedAtUtc = DateTime.UtcNow;

        db.PlayerNotifications.Add(new PlayerNotification
        {
            Id = Guid.NewGuid(),
            PlayerId = offer.BuyerPlayerId,
            Type = PlayerNotificationType.BuildingOfferRejected,
            Title = "Offer rejected",
            Message = $"Your offer of {offer.OfferedPrice.ToString("N2", CultureInfo.InvariantCulture)} {currencyCode} for {offer.Building.Name} was rejected by the seller.",
            BuildingId = offer.BuildingId,
            CreatedAtTick = gameState?.CurrentTick ?? 0,
        });

        await db.SaveChangesAsync();
        return offer;
    }
}

/// <summary>Result of accepting a building sale offer.</summary>
public sealed class AcceptBuildingOfferResult
{
    /// <summary>The building with updated ownership.</summary>
    public Building Building { get; set; } = null!;

    /// <summary>The accepted offer.</summary>
    public BuildingSaleOffer Offer { get; set; } = null!;
}
