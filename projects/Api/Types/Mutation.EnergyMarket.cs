using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Lists surplus power plant capacity on the intra-city energy spot market.
    ///
    /// Only power plant building owners may create listings.
    /// Active listings are auto-matched each tick by <c>EnergySpotMarketPhase</c>.
    /// A building can have at most one active listing at a time; submitting a new one
    /// replaces (cancels) the previous active listing.
    /// </summary>
    [Authorize]
    public async Task<EnergyListing> ListEnergyForSale(
        ListEnergyForSaleInput input,
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
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        if (building.Type != BuildingType.PowerPlant)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only power plant buildings can list energy for sale.")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        if (input.PricePerKwhLocal <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Price per kWh must be greater than zero.")
                    .SetCode("INVALID_PRICE")
                    .Build());
        }

        const decimal MaxListingPricePerKwh = 10_000m;
        if (input.PricePerKwhLocal > MaxListingPricePerKwh)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Price per kWh cannot exceed {MaxListingPricePerKwh:N0}.")
                    .SetCode("PRICE_TOO_HIGH")
                    .Build());
        }

        if (input.CapacityKw <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Capacity must be greater than zero.")
                    .SetCode("INVALID_CAPACITY")
                    .Build());
        }

        // Cancel any pre-existing active listing for this building.
        var existing = await db.EnergyListings
            .Where(l => l.BuildingId == input.BuildingId && l.IsActive)
            .FirstOrDefaultAsync();

        if (existing is not null)
        {
            existing.IsActive = false;
            existing.CancelledAtUtc = DateTime.UtcNow;
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;

        var listing = new EnergyListing
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            CompanyId = building.CompanyId,
            CityId = building.CityId,
            PricePerKwhLocal = decimal.Round(input.PricePerKwhLocal, 4, MidpointRounding.AwayFromZero),
            CapacityKw = decimal.Round(input.CapacityKw, 2, MidpointRounding.AwayFromZero),
            AvailableKw = decimal.Round(input.CapacityKw, 2, MidpointRounding.AwayFromZero),
            IsActive = true,
            CreatedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.EnergyListings.Add(listing);
        await db.SaveChangesAsync();
        return listing;
    }

    /// <summary>
    /// Removes an active energy spot-market listing.
    /// The listing will no longer be matched against deficit buildings from the next tick onward.
    /// </summary>
    [Authorize]
    public async Task<EnergyListing> CancelEnergyListing(
        CancelEnergyListingInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var listing = await db.EnergyListings
            .Include(l => l.Building).ThenInclude(b => b.Company)
            .FirstOrDefaultAsync(l => l.Id == input.ListingId);

        if (listing is null || listing.Building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        if (!listing.IsActive)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Listing is already cancelled.")
                    .SetCode("LISTING_ALREADY_CANCELLED")
                    .Build());
        }

        listing.IsActive = false;
        listing.CancelledAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return listing;
    }

    /// <summary>
    /// Sets the maximum price per kWh (in city local currency) that a building owner
    /// is willing to pay on the energy spot market when the building goes OFFLINE.
    ///
    /// Set to zero or null to opt out of spot-market auto-purchasing entirely.
    /// </summary>
    [Authorize]
    public async Task<Building> SetMaxEnergyBidPrice(
        SetMaxEnergyBidPriceInput input,
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
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        if (building.Type == BuildingType.PowerPlant)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Power plants produce energy; they cannot have a max bid price.")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        if (input.MaxBidPricePerKwh < 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Max bid price cannot be negative.")
                    .SetCode("INVALID_BID_PRICE")
                    .Build());
        }

        const decimal MaxBidPriceCeiling = 10_000m;
        if (input.MaxBidPricePerKwh > MaxBidPriceCeiling)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Max bid price cannot exceed {MaxBidPriceCeiling:N0}.")
                    .SetCode("BID_PRICE_TOO_HIGH")
                    .Build());
        }

        building.MaxEnergyBidPrice = input.MaxBidPricePerKwh > 0m
            ? decimal.Round(input.MaxBidPricePerKwh, 4, MidpointRounding.AwayFromZero)
            : null;

        await db.SaveChangesAsync();
        return building;
    }
}

/// <summary>Input for <see cref="Mutation.ListEnergyForSale"/>.</summary>
public sealed class ListEnergyForSaleInput
{
    /// <summary>ID of the power-plant building listing surplus capacity.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Asking price per kWh in the city's local currency. Must be &gt; 0.</summary>
    public decimal PricePerKwhLocal { get; set; }

    /// <summary>Capacity offered for sale in kW. Must be &gt; 0.</summary>
    public decimal CapacityKw { get; set; }
}

/// <summary>Input for <see cref="Mutation.CancelEnergyListing"/>.</summary>
public sealed class CancelEnergyListingInput
{
    /// <summary>ID of the <see cref="EnergyListing"/> to cancel.</summary>
    public Guid ListingId { get; set; }
}

/// <summary>Input for <see cref="Mutation.SetMaxEnergyBidPrice"/>.</summary>
public sealed class SetMaxEnergyBidPriceInput
{
    /// <summary>ID of the non-power-plant building to configure.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>
    /// Maximum price per kWh (local currency) to bid on the spot market.
    /// Pass 0 to disable spot-market auto-purchasing for this building.
    /// </summary>
    public decimal MaxBidPricePerKwh { get; set; }
}
