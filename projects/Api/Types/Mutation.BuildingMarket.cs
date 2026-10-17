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
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .ThenInclude(c => c.Player)
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || !building.IsForSale || !building.AskingPrice.HasValue)
        {
            throw objectAuthorization.Deny(
                userId,
                "building",
                input.BuildingId,
                building is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotAvailableReason);
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
            .FirstOrDefaultAsync(c => c.Id == input.BuyerCompanyId);

        if (buyerCompany is null || buyerCompany.PlayerId != userId)
        {
            throw objectAuthorization.Deny(
                userId,
                "company",
                input.BuyerCompanyId,
                buyerCompany is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotOwnedReason);
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
}
