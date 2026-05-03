using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    /// <summary>
    /// Idempotent: seeds <see cref="DemandSeasonality"/> rows for all existing product types.
    /// If a product already has a row it is left unchanged.
    /// Industry-based seasonal patterns are applied:
    ///   FURNITURE    – spring/summer moving season peak (Q2 1.5× Q3 1.3×)
    ///   FOOD         – holiday/winter comfort peak (Q4 1.2× Q1 1.1×)
    ///   HEALTHCARE   – winter flu season peak (Q1/Q4 1.1×), summer trough (Q3 0.9×)
    ///   ELECTRONICS  – holiday gift peak (Q4 1.5×), post-holiday trough (Q1 0.9×)
    ///   CONSTRUCTION – summer construction peak (Q3 1.4×), winter trough (Q1 0.7×)
    /// </summary>
    public async Task EnsureDemandSeasonalitySeedAsync()
    {
        var products = await dbContext.ProductTypes.ToListAsync();

        // Read existing rows so we don't duplicate.
        var existingIds = await dbContext.DemandSeasonalities
            .Select(d => d.ProductTypeId)
            .ToHashSetAsync();

        var toAdd = new List<DemandSeasonality>();

        foreach (var product in products)
        {
            if (existingIds.Contains(product.Id))
                continue;

            var (q1, q2, q3, q4) = GetMultipliersForIndustry(product.Industry);

            toAdd.Add(new DemandSeasonality
            {
                Id = CreateDeterministicGuid($"seasonality:{product.Id}"),
                ProductTypeId = product.Id,
                Q1Multiplier = q1,
                Q2Multiplier = q2,
                Q3Multiplier = q3,
                Q4Multiplier = q4,
            });
        }

        if (toAdd.Count > 0)
        {
            dbContext.DemandSeasonalities.AddRange(toAdd);
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Returns seasonal multipliers (Q1, Q2, Q3, Q4) for the given industry.
    /// All values are in the range [0.5, 2.0] with 1.0 = neutral demand.
    /// </summary>
    private static (decimal Q1, decimal Q2, decimal Q3, decimal Q4) GetMultipliersForIndustry(string industry) =>
        industry switch
        {
            // Furniture peaks in spring/summer (moving season) and dips in Q1 (post-holiday).
            Industry.Furniture =>     (0.8m, 1.5m, 1.3m, 1.0m),
            // Food processing sees a holiday/winter boost and stable baseline otherwise.
            Industry.FoodProcessing => (1.1m, 1.0m, 1.0m, 1.2m),
            // Healthcare peaks in flu seasons (Q1, Q4) and dips slightly in summer (Q3).
            Industry.Healthcare =>    (1.1m, 1.0m, 0.9m, 1.1m),
            // Electronics surges in Q4 holiday gift season; quiet in Q1 post-holiday.
            Industry.Electronics =>   (0.9m, 1.0m, 1.0m, 1.5m),
            // Construction peaks in summer (Q3), completely slows in winter (Q1).
            Industry.Construction =>  (0.7m, 1.3m, 1.4m, 0.8m),
            // Default: flat seasonality for any unlisted industry.
            _ =>                      (1.0m, 1.0m, 1.0m, 1.0m),
        };
}
