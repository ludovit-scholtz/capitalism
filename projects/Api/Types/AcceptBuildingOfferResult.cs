using Api.Data.Entities;

namespace Api.Types;

/// <summary>Result of accepting a building sale offer.</summary>
public sealed class AcceptBuildingOfferResult
{
    /// <summary>The building with updated ownership.</summary>
    public Building Building { get; set; } = null!;

    /// <summary>The accepted offer.</summary>
    public BuildingSaleOffer Offer { get; set; } = null!;
}
