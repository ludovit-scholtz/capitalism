using Api.Data.Entities;

namespace Api.Engine.Phases;

public sealed partial class PublicSalesPhase
{
    private static decimal ComputeQualityDemandFactor(decimal quality) =>
        Math.Clamp(0.2m + Math.Min(1m, quality) * 0.8m, 0.2m, 1m);

    /// <summary>
    /// Computes the combined brand quality from R&amp;D quality and marketing prestige.
    /// Both components contribute independently using an additive blending formula:
    /// <c>combined = 1 - (1 - Quality) × (1 - MarketingQuality)</c>
    /// so each source provides diminishing returns when the other is already high.
    /// Range: [0, 1].
    /// </summary>
    private static decimal ComputeCombinedBrandQuality(Brand? brand)
    {
        if (brand is null) return 0m;
        var rdQuality = Math.Clamp(brand.Quality, 0m, 1m);
        var mktQuality = Math.Clamp(brand.MarketingQuality, 0m, 1m);
        return Math.Clamp(1m - (1m - rdQuality) * (1m - mktQuality), 0m, 1m);
    }

    /// <summary>
    /// Computes the brand demand factor from awareness and combined brand quality.
    /// Awareness drives the base factor [0.2, 1.0]. Combined brand quality then amplifies
    /// this by up to <see cref="GameConstants.BrandQualityBoostFactor"/> (50%) at full quality,
    /// rewarding companies that have built both reach (awareness) and reputation (quality).
    /// The boost is multiplicative so zero quality preserves the old awareness-only behaviour.
    /// </summary>
    private static decimal ComputeBrandFactor(decimal awareness, decimal brandQuality) =>
        Math.Clamp(0.2m + awareness * 0.8m, 0.2m, 1m)
        * (1m + brandQuality * GameConstants.BrandQualityBoostFactor);

    private static decimal ComputeSaturationFactor(decimal cityBaseDemand, decimal totalCurrentStock)
    {
        if (cityBaseDemand <= 0m)
            return 0m;

        if (totalCurrentStock <= 0m)
            return 1m;

        return Math.Clamp(cityBaseDemand / totalCurrentStock, 0.05m, 1m);
    }

    private static decimal ComputeLocationFactor(decimal populationIndex, decimal maxPopulationIndex)
    {
        if (maxPopulationIndex <= 0m)
            return 1m;

        return Math.Clamp(populationIndex / maxPopulationIndex, 0.25m, 1m);
    }

    private static decimal ComputeDemandAttractiveness(SalesOffer offer, decimal maxPopulationIndex)
    {
        var locationFactor = ComputeLocationFactor(offer.PopulationIndex, maxPopulationIndex);
        return Math.Clamp(
            offer.PriceIndex * 0.45m
            + offer.QualityDemandFactor * 0.25m
            + offer.BrandFactor * 0.20m
            + locationFactor * 0.10m,
            0m,
            1m);
    }

    private static decimal ComputeMarketDemandFactor(decimal saturationFactor, decimal weightedDemandAttractiveness)
    {
        var marketAbsorptionFactor = 0.25m + (0.75m * saturationFactor);
        return Math.Clamp(marketAbsorptionFactor * Math.Max(0m, weightedDemandAttractiveness), 0m, 1m);
    }

    private static decimal ComputeCompetitionFactor(decimal marketShare)
    {
        if (marketShare <= 0m)
            return 0.05m;

        return Math.Clamp((decimal)Math.Sqrt((double)marketShare), 0.05m, 1m);
    }

    private static decimal ComputePublicSellIndex(
        decimal saturationFactor,
        decimal qualityDemandFactor,
        decimal brandFactor,
        decimal locationFactor,
        decimal competitionFactor)
    {
        return Math.Clamp(
            saturationFactor * 0.45m
            + qualityDemandFactor * 0.15m
            + brandFactor * 0.10m
            + locationFactor * 0.10m
            + competitionFactor * 0.20m,
            0m,
            1m);
    }

    /// <summary>
    /// Intermediate structure holding a single seller's offer for one product in one city.
    /// </summary>
    private sealed class SalesOffer
    {
        public Guid CityId { get; init; }
        public Guid ItemId { get; init; }
        public Building Building { get; init; } = null!;
        public BuildingUnit Unit { get; init; } = null!;
        public City City { get; init; } = null!;
        public Company Company { get; init; } = null!;
        public BuildingLot? Lot { get; init; }
        public Inventory Inventory { get; init; } = null!;
        public decimal BasePrice { get; init; }
        public decimal Price { get; init; }
        public string? Industry { get; init; }
        public string? ProductName { get; init; }
        public decimal PopulationIndex { get; init; }
        public decimal PriceElasticity { get; init; }
        public decimal PriceIndex { get; init; }
        public decimal QualityDemandFactor { get; init; }
        public decimal BrandAwareness { get; init; }
        public decimal BrandQuality { get; init; }
        public decimal BrandFactor { get; init; }
        public decimal Competitiveness { get; init; }
        public decimal CurrentStock { get; init; }
        public decimal MaxCanSell { get; init; }
    }
}
