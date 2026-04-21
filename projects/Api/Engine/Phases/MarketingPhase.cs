using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Processes MARKETING units inside SALES_SHOP buildings.
/// Deducts the configured budget from the owning company's cash,
/// routes the spend as income to the media-house owner (if a media house is selected),
/// applies channel effectiveness (TV > Radio > Newspaper) and R&amp;D efficiency multipliers,
/// and increases brand awareness for products sold in linked PUBLIC_SALES units.
/// </summary>
public sealed class MarketingPhase : ITickPhase
{
    public string Name => "Marketing";
    public int Order => 700;

    public Task ProcessAsync(TickContext context)
    {
        if (!context.BuildingsByType.TryGetValue(BuildingType.SalesShop, out var shops))
            return Task.CompletedTask;

        foreach (var building in shops)
        {
            if (!context.UnitsByBuilding.TryGetValue(building.Id, out var units))
                continue;
            if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company))
                continue;

            foreach (var unit in units)
            {
                if (unit.UnitType != UnitType.Marketing) continue;
                ProcessMarketingUnit(context, building, unit, company, units);
            }
        }

        // Decay marketing quality for all brands every tick.
        // This runs even when no marketing units are active so that
        // prestige erodes gradually when investment stops.
        DecayMarketingQuality(context);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies a slow per-tick decay to all brands' MarketingQuality.
    /// This ensures brand prestige erodes gradually when marketing investment stops,
    /// making sustained marketing a continuous strategic commitment.
    /// </summary>
    private static void DecayMarketingQuality(TickContext context)
    {
        foreach (var brand in context.AllBrands)
        {
            if (brand.MarketingQuality <= 0m) continue;
            var decay = decimal.Round(
                brand.MarketingQuality * GameConstants.BrandMarketingQualityDecayRate,
                6, MidpointRounding.AwayFromZero);
            brand.MarketingQuality = Math.Max(0m, brand.MarketingQuality - decay);
        }
    }

    private static void ProcessMarketingUnit(
        TickContext context,
        Building building,
        BuildingUnit unit,
        Company company,
        List<BuildingUnit> allUnits)
    {
        if (unit.Budget is null || unit.Budget <= 0m) return;

        var budget = Math.Min(unit.Budget.Value, company.Cash);
        if (budget <= 0m) return;

        // Find product types in linked PUBLIC_SALES units to target.
        var linkedUnits = context.GetOutgoingLinkedUnits(unit);
        var productIds = linkedUnits
            .Where(u => u.UnitType == UnitType.PublicSales && u.ProductTypeId.HasValue)
            .Select(u => u.ProductTypeId!.Value)
            .Distinct()
            .ToList();

        // Fallback: if no direct links, use all PUBLIC_SALES units in same building.
        if (productIds.Count == 0)
        {
            productIds = allUnits
                .Where(u => u.UnitType == UnitType.PublicSales && u.ProductTypeId.HasValue)
                .Select(u => u.ProductTypeId!.Value)
                .Distinct()
                .ToList();
        }

        if (productIds.Count == 0) return;

        // Resolve the selected media house and compute channel effectiveness.
        Building? mediaHouse = null;
        var channelMultiplier = 1.0m;
        var channelDescription = "direct";

        if (unit.MediaHouseBuildingId.HasValue)
        {
            mediaHouse = context.BuildingsById.TryGetValue(unit.MediaHouseBuildingId.Value, out var mh)
                && mh.Type == BuildingType.MediaHouse
                && mh.CityId == building.CityId
                ? mh : null;

            if (mediaHouse is not null)
            {
                channelMultiplier = MediaType.EffectivenessMultiplier(mediaHouse.MediaType);
                channelDescription = mediaHouse.MediaType?.ToLowerInvariant() ?? "media";

                // Apply content ranking multiplier: top-ranked outlet = 1.5×, zero-ranked = 0.5×.
                var contentRankingFraction = ComputeContentRankingFraction(mediaHouse, context);
                channelMultiplier *= GameConstants.ContentRankingBaseMultiplier
                    + contentRankingFraction * GameConstants.ContentRankingMarketingBoostRange;
            }
        }

        company.Cash -= budget;

        context.Db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = building.Id,
            BuildingUnitId = unit.Id,
            Category = LedgerCategory.Marketing,
            Description = $"Marketing spend via {channelDescription}",
            Amount = -budget,
            RecordedAtTick = context.CurrentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        // Route income to the media house owner, if applicable and if a different company.
        if (mediaHouse is not null
            && context.CompaniesById.TryGetValue(mediaHouse.CompanyId, out var mediaOwner)
            && mediaOwner.Id != company.Id)
        {
            mediaOwner.Cash += budget;
            context.Db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = mediaOwner.Id,
                BuildingId = mediaHouse.Id,
                Category = LedgerCategory.MediaHouseIncome,
                Description = $"Advertising revenue ({channelDescription})",
                Amount = budget,
                RecordedAtTick = context.CurrentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }

        var budgetPerProduct = budget / productIds.Count;

        foreach (var productId in productIds)
        {
            var productName = context.ProductTypesById.TryGetValue(productId, out var pt) ? pt.Name : "Product";
            var brand = context.GetOrCreateBrand(building.CompanyId, productId, $"{company.Cash:F0} – {productName}");

            // Apply marketing efficiency multiplier from BRAND_QUALITY R&D.
            // This is the causal chain: R&D → higher efficiency → marketing budget produces more awareness.
            var efficiencyBrand = context.FindBrand(building.CompanyId, productId, pt?.Industry);
            var efficiencyMultiplier = efficiencyBrand?.MarketingEfficiencyMultiplier ?? 1m;

            // Combined: channel reach × R&D efficiency.
            brand.Awareness = Math.Min(1m, brand.Awareness + budgetPerProduct * GameConstants.BrandAwarenessPerBudget * channelMultiplier * efficiencyMultiplier);

            // Brand prestige (marketing quality): sustained marketing spend builds long-term brand reputation.
            // This grows much more slowly than awareness and accumulates as a durable competitive advantage.
            // Channel quality and R&D efficiency both amplify the quality gain rate.
            var qualityGain = decimal.Round(
                budgetPerProduct * GameConstants.BrandMarketingQualityPerBudget * channelMultiplier * efficiencyMultiplier,
                6, MidpointRounding.AwayFromZero);
            brand.MarketingQuality = Math.Min(1m, brand.MarketingQuality + qualityGain);
        }
    }

    /// <summary>
    /// Computes the content ranking fraction (0–1) for a media house relative to all
    /// outlets in the same city and media category.
    /// The top outlet returns 1.0; an outlet with zero content returns 0.0.
    /// </summary>
    private static decimal ComputeContentRankingFraction(Building mediaHouse, TickContext context)
    {
        var mediaType = mediaHouse.MediaType ?? string.Empty;
        decimal maxContent = 0m;

        if (!context.BuildingsByType.TryGetValue(BuildingType.MediaHouse, out var allMediaHouses))
            return 0m;

        foreach (var mh in allMediaHouses)
        {
            if (mh.CityId != mediaHouse.CityId) continue;
            if ((mh.MediaType ?? string.Empty) != mediaType) continue;
            if (mh.ContentValue > maxContent) maxContent = mh.ContentValue;
        }

        if (maxContent <= 0m) return 0m;
        return Math.Clamp(mediaHouse.ContentValue / maxContent, 0m, 1m);
    }
}