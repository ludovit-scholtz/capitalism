using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="PriceAdjustmentHelper"/> — pure functions with no I/O.
/// </summary>
public sealed class PriceAdjustmentHelperTests
{
    // ── ComputeNewPrice ───────────────────────────────────────────────────────

    [Fact]
    public void ComputeNewPrice_IdentityFactor_ReturnsSamePrice()
    {
        // Factor 1.0 should produce no change.
        Assert.Equal(100.00m, PriceAdjustmentHelper.ComputeNewPrice(100.00m, 1.0m));
    }

    [Fact]
    public void ComputeNewPrice_MildReductionFactor_ReducesByFivePercent()
    {
        // 100 × 0.95 = 95.00
        Assert.Equal(95.00m, PriceAdjustmentHelper.ComputeNewPrice(100.00m, 0.95m));
    }

    [Fact]
    public void ComputeNewPrice_AggressiveReductionFactor_ReducesByFifteenPercent()
    {
        // 100 × 0.85 = 85.00
        Assert.Equal(85.00m, PriceAdjustmentHelper.ComputeNewPrice(100.00m, 0.85m));
    }

    [Fact]
    public void ComputeNewPrice_RoundsToTwoDecimalPlaces()
    {
        // 33.33 × 0.95 = 31.6635 → rounds to 31.66
        Assert.Equal(31.66m, PriceAdjustmentHelper.ComputeNewPrice(33.33m, 0.95m));
    }

    [Fact]
    public void ComputeNewPrice_VerySmallPrice_ClampsToMinimum()
    {
        // 0.005 × 0.85 = 0.00425 → clamped to 0.01
        Assert.Equal(0.01m, PriceAdjustmentHelper.ComputeNewPrice(0.005m, 0.85m));
    }

    [Fact]
    public void ComputeNewPrice_ZeroPrice_ReturnsMinimum()
    {
        // 0 × any factor = 0 → clamped to 0.01
        Assert.Equal(PriceAdjustmentHelper.MinimumAllowedPrice,
            PriceAdjustmentHelper.ComputeNewPrice(0m, 0.95m));
    }

    [Fact]
    public void ComputeNewPrice_LargePrice_FactorAppliedCorrectly()
    {
        // 50_000 × 0.85 = 42_500.00
        Assert.Equal(42_500.00m, PriceAdjustmentHelper.ComputeNewPrice(50_000m, 0.85m));
    }

    [Fact]
    public void ComputeNewPrice_NeverNegative()
    {
        // Negative factor (unusual, but must not produce negative price) — clamped to minimum.
        var result = PriceAdjustmentHelper.ComputeNewPrice(100m, -0.5m);
        Assert.Equal(PriceAdjustmentHelper.MinimumAllowedPrice, result);
    }

    // ── IsAdjustmentMeaningful ────────────────────────────────────────────────

    [Fact]
    public void IsAdjustmentMeaningful_LargeDifference_ReturnsTrue()
    {
        // 100 → 95 is a $5 difference — clearly meaningful
        Assert.True(PriceAdjustmentHelper.IsAdjustmentMeaningful(100m, 95m));
    }

    [Fact]
    public void IsAdjustmentMeaningful_ExactlyOneCentDifference_ReturnsTrue()
    {
        // Exactly 1 cent difference is the threshold boundary — must return true
        Assert.True(PriceAdjustmentHelper.IsAdjustmentMeaningful(100.00m, 99.99m));
    }

    [Fact]
    public void IsAdjustmentMeaningful_ZeroDifference_ReturnsFalse()
    {
        // No change at all — should not issue a no-op update
        Assert.False(PriceAdjustmentHelper.IsAdjustmentMeaningful(100m, 100m));
    }

    [Fact]
    public void IsAdjustmentMeaningful_SubCentDifference_ReturnsFalse()
    {
        // Less than 1 cent change — too small to bother updating
        Assert.False(PriceAdjustmentHelper.IsAdjustmentMeaningful(100.000m, 100.005m));
    }

    // ── SelectAdjustableUnits ─────────────────────────────────────────────────

    [Fact]
    public void SelectAdjustableUnits_EmptyCompanies_ReturnsEmpty()
    {
        var result = PriceAdjustmentHelper.SelectAdjustableUnits([]).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void SelectAdjustableUnits_NoPublicSalesUnits_ReturnsEmpty()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Acme",
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "Factory",
                        Units = [new() { Id = "u1", UnitType = "MANUFACTURING", MinPrice = null }],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void SelectAdjustableUnits_PublicSalesUnitWithNullPrice_Excluded()
    {
        // A PUBLIC_SALES unit that has never been configured has MinPrice = null — skip it.
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Acme",
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "Shop",
                        Units = [new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = null }],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void SelectAdjustableUnits_PublicSalesUnitWithZeroPrice_Excluded()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Acme",
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "Shop",
                        Units = [new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 0m }],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void SelectAdjustableUnits_PublicSalesUnitWithPrice_ReturnsIt()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Acme",
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "Wooden Chair Shop",
                        Units =
                        [
                            new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 50m },
                        ],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Single(result);
        Assert.Equal("u1", result[0].Unit.Id);
        Assert.Equal("Wooden Chair Shop", result[0].BuildingName);
    }

    [Fact]
    public void SelectAdjustableUnits_CaseInsensitiveUnitType_MatchesLowercase()
    {
        // Unit type might arrive from a server returning lowercase "public_sales"
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Shop Co",
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "My Shop",
                        Units = [new() { Id = "u1", UnitType = "public_sales", MinPrice = 30m }],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Single(result);
    }

    [Fact]
    public void SelectAdjustableUnits_MultipleCompaniesAndBuildings_ReturnsAllEligible()
    {
        // Two companies, three buildings — only PUBLIC_SALES units with a price are returned.
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Furniture Co",
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "Chair Shop",
                        Units =
                        [
                            new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 50m },
                            new() { Id = "u2", UnitType = "PURCHASE", MinPrice = null },
                        ],
                    },
                    new()
                    {
                        Id = "b2", Name = "Table Shop",
                        Units =
                        [
                            new() { Id = "u3", UnitType = "PUBLIC_SALES", MinPrice = 80m },
                        ],
                    },
                ],
            },
            new()
            {
                Id = "c2", Name = "Food Co",
                Buildings =
                [
                    new()
                    {
                        Id = "b3", Name = "Bread Factory",
                        Units =
                        [
                            new() { Id = "u4", UnitType = "MANUFACTURING", MinPrice = null },
                            new() { Id = "u5", UnitType = "PUBLIC_SALES", MinPrice = 5m },
                        ],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Equal(3, result.Count);
        Assert.Contains(result, r => r.Unit.Id == "u1");
        Assert.Contains(result, r => r.Unit.Id == "u3");
        Assert.Contains(result, r => r.Unit.Id == "u5");
    }

    [Fact]
    public void SelectAdjustableUnits_MixedUnitTypes_OnlyPublicSalesReturned()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Mixed Co",
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "Mixed Building",
                        Units =
                        [
                            new() { Id = "u1", UnitType = "PURCHASE", MinPrice = 10m },
                            new() { Id = "u2", UnitType = "MANUFACTURING", MinPrice = 5m },
                            new() { Id = "u3", UnitType = "STORAGE", MinPrice = 1m },
                            new() { Id = "u4", UnitType = "PUBLIC_SALES", MinPrice = 45m },
                            new() { Id = "u5", UnitType = "B2B_SALES", MinPrice = 40m },
                        ],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Single(result);
        Assert.Equal("u4", result[0].Unit.Id);
    }

    // ── BuildingName pairing ──────────────────────────────────────────────────

    [Fact]
    public void SelectAdjustableUnits_BuildingNamePairedCorrectly()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Co",
                Buildings =
                [
                    new()
                    {
                        Id = "b1", Name = "Downtown Shop",
                        Units = [new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 25m }],
                    },
                ],
            },
        };

        var (unit, buildingName) = PriceAdjustmentHelper.SelectAdjustableUnits(companies).Single();
        Assert.Equal("u1", unit.Id);
        Assert.Equal("Downtown Shop", buildingName);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void SelectAdjustableUnits_CompanyWithNoBuildings_ReturnsEmpty()
    {
        var companies = new List<CompanySummary>
        {
            new() { Id = "c1", Name = "Empty Co", Buildings = [] },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void SelectAdjustableUnits_BuildingWithNoUnits_ReturnsEmpty()
    {
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1", Name = "Co",
                Buildings =
                [
                    new() { Id = "b1", Name = "Empty Building", Units = [] },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Empty(result);
    }

    [Fact]
    public void IsAdjustmentMeaningful_BothPricesZero_ReturnsFalse()
    {
        // 0 → 0 produces |0 - 0| = 0 which is less than 0.01
        Assert.False(PriceAdjustmentHelper.IsAdjustmentMeaningful(0m, 0m));
    }

    [Fact]
    public void IsAdjustmentMeaningful_BothAtFloor_ReturnsFalse()
    {
        // MinimumAllowedPrice → MinimumAllowedPrice: no change
        Assert.False(PriceAdjustmentHelper.IsAdjustmentMeaningful(
            PriceAdjustmentHelper.MinimumAllowedPrice,
            PriceAdjustmentHelper.MinimumAllowedPrice));
    }

    [Fact]
    public void ComputeNewPrice_ExactMidpointRounding_RoundsAwayFromZero()
    {
        // 10.005 rounds to 10.01 (AwayFromZero midpoint rounding, not banker's rounding)
        Assert.Equal(10.01m, PriceAdjustmentHelper.ComputeNewPrice(10.005m, 1.0m));
    }
}
