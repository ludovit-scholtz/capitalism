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
    /// API-key callers can submit offers only for buyer companies owned by the authenticated principal.
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
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
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
                    .SetMessage("Insufficient funds.")
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
        var escrowReserved = CompanyBankingService.TryDebit(
            buyerCompany.BankAccounts,
            input.OfferedPrice,
            currencyCode);
        if (!escrowReserved)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Insufficient funds.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        var offer = new BuildingSaleOffer
        {
            Id = Guid.NewGuid(),
            OfferVersion = Guid.NewGuid(),
            BuildingId = building.Id,
            BuyerPlayerId = userId,
            BuyerCompanyId = buyerCompany.Id,
            OfferedPrice = input.OfferedPrice,
            EscrowAmount = input.OfferedPrice,
            EscrowCurrencyCode = currencyCode,
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
            Severity = PlayerNotificationSeverity.Info,
            RelatedEntityType = "BUILDING",
            RelatedEntityId = building.Id,
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
    /// API-key callers can accept only offers for buildings owned by the authenticated principal.
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

        if (offer is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        if (offer.Status != BuildingSaleOfferStatus.Pending)
        {
            await LogOfferVersionConflictAsync(
                db,
                action: "ACCEPT",
                actorPlayerId: userId,
                offerId: input.OfferId,
                buyerPlayerId: offer.BuyerPlayerId,
                expectedVersion: input.OfferVersion,
                actualVersion: offer.OfferVersion);
            throw BuildOfferVersionConflictException();
        }

        if (offer.OfferVersion != input.OfferVersion)
        {
            await LogOfferVersionConflictAsync(
                db,
                action: "ACCEPT",
                actorPlayerId: userId,
                offerId: input.OfferId,
                buyerPlayerId: offer.BuyerPlayerId,
                expectedVersion: input.OfferVersion,
                actualVersion: offer.OfferVersion);
            throw BuildOfferVersionConflictException();
        }

        var building = offer.Building;
        var sellerCompany = building.Company;

        if (sellerCompany.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
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

        var hasCollateralLock = await db.Loans
            .AsNoTracking()
            .AnyAsync(loan =>
                loan.CollateralBuildingId == building.Id
                && loan.RemainingPrincipal > 0m
                && (loan.Status == LoanStatus.Active || loan.Status == LoanStatus.Overdue));
        if (hasCollateralLock)
        {
            LoanCollateralSecurityAuditLogger.Add(
                db,
                userId,
                action: "BUILDING_TRANSFER",
                reason: "BUILDING_LOCKED_AS_COLLATERAL",
                buildingId: building.Id);
            await db.SaveChangesAsync();
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building is locked as loan collateral.")
                    .SetCode("BUILDING_LOCKED_AS_COLLATERAL")
                    .Build());
        }

        var city = building.City;
        var currencyCode = city.CurrencyCode;
        var salePrice = offer.OfferedPrice;

        var marketValuation = await BuildingMarketValuationCalculator.CalculateAsync(
            db,
            building,
            httpContextAccessor.HttpContext!.RequestAborted);
        if (salePrice < marketValuation.MinimumSalePrice)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Offer is below the minimum sale floor ({marketValuation.MinimumSalePrice:F2} {marketValuation.CurrencyCode}).")
                    .SetCode("OFFER_BELOW_FLOOR")
                    .SetExtension("minimumSaleFloor", marketValuation.MinimumSalePrice)
                    .SetExtension("currencyCode", marketValuation.CurrencyCode)
                    .Build());
        }

        var escrowAvailable = string.Equals(offer.EscrowCurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase)
            && offer.EscrowAmount >= salePrice;
        if (!escrowAvailable)
        {
            var buyerBalance = CompanyBankingService.GetCurrencyBalance(
                offer.BuyerCompany.BankAccounts, currencyCode);
            if (buyerBalance < salePrice)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Buyer does not have sufficient funds to complete this purchase.")
                        .SetCode("INSUFFICIENT_FUNDS")
                        .Build());
            }
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0;
        var nowUtc = DateTime.UtcNow;
        var sellerNetProceeds = salePrice;
        BankAccount? lenderDebtCreditedAccount = null;
        decimal debtPaidInLoanCurrency = 0m;
        // These are populated inside the collateral-loan block and reused when we write the settlement ledger rows.
        decimal debtPaidFromSale = 0m;
        string? debtCurrencyCode = null;
        var usesForcedSaleFx = false;

        static string BuildDebtSettlementDescription(
            bool usesFx,
            decimal debtPaidFromSale,
            string saleCurrencyCode,
            decimal debtPaidInLoanCurrency,
            string? loanCurrencyCode,
            string buildingName) =>
            usesFx
                ? $"Forced-sale FX swap: {debtPaidFromSale.ToString("N2", CultureInfo.InvariantCulture)} {saleCurrencyCode} → {debtPaidInLoanCurrency.ToString("N2", CultureInfo.InvariantCulture)} {loanCurrencyCode} for loan settlement of {buildingName}"
                : $"Collateral debt settled from sale proceeds for {buildingName}";

        static decimal GetSellerDebtSettlementLedgerAmount(
            bool usesFx,
            decimal debtPaidFromSale,
            decimal debtPaidInLoanCurrency) => usesFx ? -debtPaidFromSale : -debtPaidInLoanCurrency;

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

            if (salePrice < remainingDebtInSaleCurrency)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage(
                            $"Sale proceeds are below the outstanding collateral lien. Required minimum is {remainingDebtInSaleCurrency:F2} {currencyCode}.")
                        .SetCode("COLLATERAL_LIEN_UNDERFUNDED")
                        .SetExtension("requiredLienSettlementAmount", remainingDebtInSaleCurrency)
                        .SetExtension("currencyCode", currencyCode)
                        .Build());
            }

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
            collateralLoan.ConcurrencyToken = Guid.NewGuid();
            collateralLoan.RemainingPrincipal = 0m;
            collateralLoan.Status = LoanStatus.Repaid;
            collateralLoan.DefaultedAtTick = null;
            collateralLoan.ClosedAtUtc = nowUtc;
            collateralLoan.MissedPayments = 0;
            collateralLoan.AccumulatedPenalty = 0m;
            collateralLoan.LoanOffer.UsedCapacity = Math.Max(0m, collateralLoan.LoanOffer.UsedCapacity - collateralLoan.OriginalPrincipal);

            sellerNetProceeds = Math.Max(0m, salePrice - debtPaidFromSale);
        }

        if (!escrowAvailable && !CompanyBankingService.TryDebit(offer.BuyerCompany.BankAccounts, salePrice, currencyCode))
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
            ? BuildDebtSettlementDescription(
                usesForcedSaleFx,
                debtPaidFromSale,
                currencyCode,
                debtPaidInLoanCurrency,
                debtCurrencyCode,
                building.Name)
            : null;

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;

        var acceptedOfferVersion = Guid.NewGuid();
        var acceptedRows = 0;
        if (db.Database.IsRelational())
        {
            acceptedRows = await db.BuildingSaleOffers
                .Where(o =>
                    o.Id == input.OfferId
                    && o.Status == BuildingSaleOfferStatus.Pending
                    && o.OfferVersion == input.OfferVersion)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.Status, BuildingSaleOfferStatus.Accepted)
                    .SetProperty(o => o.ResolvedAtUtc, nowUtc)
                    .SetProperty(o => o.OfferVersion, acceptedOfferVersion)
                    .SetProperty(o => o.EscrowAmount, 0m));
        }
        else
        {
            var matchingOffer = await db.BuildingSaleOffers
                .FirstOrDefaultAsync(o =>
                    o.Id == input.OfferId
                    && o.Status == BuildingSaleOfferStatus.Pending
                    && o.OfferVersion == input.OfferVersion);
            if (matchingOffer is not null)
            {
                matchingOffer.Status = BuildingSaleOfferStatus.Accepted;
                matchingOffer.ResolvedAtUtc = nowUtc;
                matchingOffer.OfferVersion = acceptedOfferVersion;
                matchingOffer.EscrowAmount = 0m;
                acceptedRows = 1;
            }
        }

        if (acceptedRows != 1)
        {
            var latest = await db.BuildingSaleOffers
                .AsNoTracking()
                .Where(o => o.Id == input.OfferId)
                .Select(o => new { o.BuyerPlayerId, o.OfferVersion })
                .FirstOrDefaultAsync();
            await LogOfferVersionConflictAsync(
                db,
                action: "ACCEPT",
                actorPlayerId: userId,
                offerId: input.OfferId,
                buyerPlayerId: latest?.BuyerPlayerId ?? offer.BuyerPlayerId,
                expectedVersion: input.OfferVersion,
                actualVersion: latest?.OfferVersion);
            if (tx is not null)
            {
                await tx.CommitAsync();
            }
            throw BuildOfferVersionConflictException();
        }

        // Transfer building ownership
        building.CompanyId = offer.BuyerCompanyId;
        building.IsForSale = false;
        building.AskingPrice = null;
        building.ConcurrencyToken = Guid.NewGuid();

        // Mark this offer as accepted

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
            await ReleaseOfferEscrowAsync(db, rejected.BuyerCompanyId, rejected.EscrowAmount, rejected.EscrowCurrencyCode);
            rejected.EscrowAmount = 0m;
            rejected.Status = BuildingSaleOfferStatus.Rejected;
            rejected.ResolvedAtUtc = nowUtc;
            rejected.OfferVersion = Guid.NewGuid();

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
                Amount = GetSellerDebtSettlementLedgerAmount(usesForcedSaleFx, debtPaidFromSale, debtPaidInLoanCurrency),
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
            // Record the gross sale so the bank statement can show the full sale proceeds, with debt settlement split into its own row.
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

        try
        {
            await db.SaveChangesAsync();
            if (tx is not null)
            {
                await tx.CommitAsync();
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            if (tx is not null)
            {
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            var latest = await db.BuildingSaleOffers
                .AsNoTracking()
                .Where(o => o.Id == input.OfferId)
                .Select(o => new { o.BuyerPlayerId, o.OfferVersion })
                .FirstOrDefaultAsync();
            await LogOfferVersionConflictAsync(
                db,
                action: "ACCEPT",
                actorPlayerId: userId,
                offerId: input.OfferId,
                buyerPlayerId: latest?.BuyerPlayerId ?? offer.BuyerPlayerId,
                expectedVersion: input.OfferVersion,
                actualVersion: latest?.OfferVersion);
            throw BuildOfferVersionConflictException();
        }
        var acceptedOffer = await db.BuildingSaleOffers
            .AsNoTracking()
            .FirstAsync(o => o.Id == offer.Id);

        return new AcceptBuildingOfferResult
        {
            Building = building,
            Offer = acceptedOffer,
        };
    }

    /// <summary>
    /// Seller cancels a pending offer.
    /// API-key callers can cancel only offers for buildings owned by the authenticated principal.
    /// </summary>
    [Authorize]
    public async Task<BuildingSaleOffer> CancelBuildingOffer(
        CancelBuildingOfferInput input,
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

        if (offer is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        if (offer.Status != BuildingSaleOfferStatus.Pending)
        {
            await LogOfferVersionConflictAsync(
                db,
                action: "CANCEL",
                actorPlayerId: userId,
                offerId: input.OfferId,
                buyerPlayerId: offer.BuyerPlayerId,
                expectedVersion: input.OfferVersion,
                actualVersion: offer.OfferVersion);
            throw BuildOfferVersionConflictException();
        }

        if (offer.OfferVersion != input.OfferVersion)
        {
            await LogOfferVersionConflictAsync(
                db,
                action: "CANCEL",
                actorPlayerId: userId,
                offerId: input.OfferId,
                buyerPlayerId: offer.BuyerPlayerId,
                expectedVersion: input.OfferVersion,
                actualVersion: offer.OfferVersion);
            throw BuildOfferVersionConflictException();
        }

        if (offer.Building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var city = offer.Building.City;
        var currencyCode = city.CurrencyCode;
        var nowUtc = DateTime.UtcNow;
        var escrowAmountToRelease = offer.EscrowAmount;
        var escrowCurrencyToRelease = offer.EscrowCurrencyCode;
        var escrowBuyerCompanyId = offer.BuyerCompanyId;

        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;

        var cancelledOfferVersion = Guid.NewGuid();
        var cancelledRows = 0;
        if (db.Database.IsRelational())
        {
            cancelledRows = await db.BuildingSaleOffers
                .Where(o =>
                    o.Id == input.OfferId
                    && o.Status == BuildingSaleOfferStatus.Pending
                    && o.OfferVersion == input.OfferVersion)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(o => o.Status, BuildingSaleOfferStatus.Rejected)
                    .SetProperty(o => o.ResolvedAtUtc, nowUtc)
                    .SetProperty(o => o.OfferVersion, cancelledOfferVersion)
                    .SetProperty(o => o.EscrowAmount, 0m));
        }
        else
        {
            var matchingOffer = await db.BuildingSaleOffers
                .FirstOrDefaultAsync(o =>
                    o.Id == input.OfferId
                    && o.Status == BuildingSaleOfferStatus.Pending
                    && o.OfferVersion == input.OfferVersion);
            if (matchingOffer is not null)
            {
                matchingOffer.Status = BuildingSaleOfferStatus.Rejected;
                matchingOffer.ResolvedAtUtc = nowUtc;
                matchingOffer.OfferVersion = cancelledOfferVersion;
                matchingOffer.EscrowAmount = 0m;
                cancelledRows = 1;
            }
        }

        if (cancelledRows != 1)
        {
            var latest = await db.BuildingSaleOffers
                .AsNoTracking()
                .Where(o => o.Id == input.OfferId)
                .Select(o => new { o.BuyerPlayerId, o.OfferVersion })
                .FirstOrDefaultAsync();
            await LogOfferVersionConflictAsync(
                db,
                action: "CANCEL",
                actorPlayerId: userId,
                offerId: input.OfferId,
                buyerPlayerId: latest?.BuyerPlayerId ?? offer.BuyerPlayerId,
                expectedVersion: input.OfferVersion,
                actualVersion: latest?.OfferVersion);
            if (tx is not null)
            {
                await tx.CommitAsync();
            }
            throw BuildOfferVersionConflictException();
        }
        await ReleaseOfferEscrowAsync(db, escrowBuyerCompanyId, escrowAmountToRelease, escrowCurrencyToRelease);

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

        try
        {
            await db.SaveChangesAsync();
            if (tx is not null)
            {
                await tx.CommitAsync();
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            if (tx is not null)
            {
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();
            var latest = await db.BuildingSaleOffers
                .AsNoTracking()
                .Where(o => o.Id == input.OfferId)
                .Select(o => new { o.BuyerPlayerId, o.OfferVersion })
                .FirstOrDefaultAsync();
            await LogOfferVersionConflictAsync(
                db,
                action: "CANCEL",
                actorPlayerId: userId,
                offerId: input.OfferId,
                buyerPlayerId: latest?.BuyerPlayerId ?? offer.BuyerPlayerId,
                expectedVersion: input.OfferVersion,
                actualVersion: latest?.OfferVersion);
            throw BuildOfferVersionConflictException();
        }
        return await db.BuildingSaleOffers
            .AsNoTracking()
            .FirstAsync(o => o.Id == input.OfferId);
    }

    /// <summary>Seller rejects a pending offer.</summary>
    [Authorize]
    public Task<BuildingSaleOffer> RejectBuildingOffer(
        RejectBuildingOfferInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor) =>
        CancelBuildingOffer(
            new CancelBuildingOfferInput
            {
                OfferId = input.OfferId,
                OfferVersion = input.OfferVersion,
            },
            db,
            httpContextAccessor);

    private static GraphQLException BuildOfferVersionConflictException() =>
        new(
            ErrorBuilder.New()
                .SetMessage("This building was just sold or the offer changed — please refresh the market listing.")
                .SetCode("OFFER_VERSION_CONFLICT")
                .Build());

    private static async Task LogOfferVersionConflictAsync(
        AppDbContext db,
        string action,
        Guid actorPlayerId,
        Guid offerId,
        Guid buyerPlayerId,
        Guid expectedVersion,
        Guid? actualVersion)
    {
        db.BuildingOfferSecurityAuditLogs.Add(new BuildingOfferSecurityAuditLog
        {
            Id = Guid.NewGuid(),
            OfferId = offerId,
            BuyerPlayerId = buyerPlayerId,
            ActorPlayerId = actorPlayerId,
            Action = action,
            ExpectedOfferVersion = expectedVersion,
            ActualOfferVersion = actualVersion,
            OccurredAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static async Task ReleaseOfferEscrowAsync(
        AppDbContext db,
        Guid buyerCompanyId,
        decimal escrowAmount,
        string? escrowCurrencyCode)
    {
        if (escrowAmount <= 0m)
        {
            return;
        }

        var escrowCurrency = string.IsNullOrWhiteSpace(escrowCurrencyCode)
            ? "EUR"
            : escrowCurrencyCode;
        var buyerEscrowAccount = await CompanyBankingService.EnsurePreferredAccountAsync(
            db,
            buyerCompanyId,
            escrowCurrency);
        buyerEscrowAccount.Balance += escrowAmount;
        buyerEscrowAccount.ConcurrencyToken = Guid.NewGuid();
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
