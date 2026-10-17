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
    /// Seller cancels a pending offer.
    /// API-key callers can cancel only offers for buildings owned by the authenticated principal.
    /// </summary>
    [Authorize]
    public async Task<BuildingSaleOffer> CancelBuildingOffer(
        CancelBuildingOfferInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
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
            throw objectAuthorization.Deny(
                userId,
                "building_sale_offer",
                input.OfferId,
                ObjectAuthorizationService.NotFoundReason);
        }

        if (offer.Building.Company.PlayerId != userId)
        {
            throw objectAuthorization.Deny(
                userId,
                "building_sale_offer",
                input.OfferId,
                ObjectAuthorizationService.NotOwnedReason);
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
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization) =>
        CancelBuildingOffer(
            new CancelBuildingOfferInput
            {
                OfferId = input.OfferId,
                OfferVersion = input.OfferVersion,
            },
            db,
            httpContextAccessor,
            objectAuthorization);

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
