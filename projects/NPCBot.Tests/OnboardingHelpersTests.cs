using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="OnboardingHelpers"/> — pure lot and product selection
/// logic extracted from <see cref="OnboardingService"/> for testability.
/// </summary>
public sealed class OnboardingHelpersTests
{
    // ── ContainsSuitableType ──────────────────────────────────────────────────

    [Fact]
    public void ContainsSuitableType_ExactMatch_ReturnsTrue()
    {
        Assert.True(OnboardingHelpers.ContainsSuitableType("FACTORY", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_CsvContainsType_ReturnsTrue()
    {
        Assert.True(OnboardingHelpers.ContainsSuitableType("FACTORY,MINE", "FACTORY"));
        Assert.True(OnboardingHelpers.ContainsSuitableType("FACTORY,MINE", "MINE"));
    }

    [Fact]
    public void ContainsSuitableType_CsvDoesNotContainType_ReturnsFalse()
    {
        Assert.False(OnboardingHelpers.ContainsSuitableType("FACTORY,MINE", "SALES_SHOP"));
    }

    [Fact]
    public void ContainsSuitableType_CaseInsensitive_ReturnsTrue()
    {
        Assert.True(OnboardingHelpers.ContainsSuitableType("factory,mine", "FACTORY"));
        Assert.True(OnboardingHelpers.ContainsSuitableType("FACTORY,MINE", "factory"));
    }

    [Fact]
    public void ContainsSuitableType_SubstringDoesNotMatch_ReturnsFalse()
    {
        // "FACT" should NOT match "FACTORY" — whole-segment matching required.
        Assert.False(OnboardingHelpers.ContainsSuitableType("FACTORY", "FACT"));
    }

    [Fact]
    public void ContainsSuitableType_EmptyField_ReturnsFalse()
    {
        Assert.False(OnboardingHelpers.ContainsSuitableType(string.Empty, "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_NullField_ReturnsFalse()
    {
        Assert.False(OnboardingHelpers.ContainsSuitableType(null!, "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_EmptySuitableType_ReturnsFalse()
    {
        Assert.False(OnboardingHelpers.ContainsSuitableType("FACTORY", string.Empty));
    }

    [Fact]
    public void ContainsSuitableType_TrimsWhitespaceAroundSegments()
    {
        Assert.True(OnboardingHelpers.ContainsSuitableType(" FACTORY , MINE ", "FACTORY"));
    }

    // ── PickCheapestAvailableLot ──────────────────────────────────────────────

    [Fact]
    public void PickCheapestAvailableLot_EmptyList_ReturnsNull()
    {
        var result = OnboardingHelpers.PickCheapestAvailableLot([], "FACTORY");
        Assert.Null(result);
    }

    [Fact]
    public void PickCheapestAvailableLot_NoMatchingType_ReturnsNull()
    {
        var lots = new[]
        {
            new BuildingLotSummary { Id = "1", SuitableTypes = "MINE", Price = 100m },
        };
        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");
        Assert.Null(result);
    }

    [Fact]
    public void PickCheapestAvailableLot_AllLotsOccupied_ReturnsNull()
    {
        var lots = new[]
        {
            new BuildingLotSummary { Id = "1", SuitableTypes = "FACTORY", BuildingId = "bldg-1", Price = 100m },
            new BuildingLotSummary { Id = "2", SuitableTypes = "FACTORY", BuildingId = "bldg-2", Price = 50m },
        };
        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");
        Assert.Null(result);
    }

    [Fact]
    public void PickCheapestAvailableLot_SingleMatchingLot_ReturnsThatLot()
    {
        var lots = new[]
        {
            new BuildingLotSummary { Id = "1", SuitableTypes = "FACTORY", Price = 75_000m },
        };
        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");
        Assert.NotNull(result);
        Assert.Equal("1", result.Id);
    }

    [Fact]
    public void PickCheapestAvailableLot_MultipleAvailable_ReturnsLowestPrice()
    {
        var lots = new[]
        {
            new BuildingLotSummary { Id = "expensive", SuitableTypes = "FACTORY", Price = 200_000m },
            new BuildingLotSummary { Id = "cheapest",  SuitableTypes = "FACTORY", Price = 75_000m },
            new BuildingLotSummary { Id = "middle",    SuitableTypes = "FACTORY", Price = 150_000m },
        };
        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");
        Assert.Equal("cheapest", result!.Id);
    }

    [Fact]
    public void PickCheapestAvailableLot_SkipsOccupiedAndPicksCheapestFree()
    {
        var lots = new[]
        {
            new BuildingLotSummary { Id = "occupied-cheap", SuitableTypes = "FACTORY", BuildingId = "b1", Price = 50_000m },
            new BuildingLotSummary { Id = "free-expensive", SuitableTypes = "FACTORY", Price = 120_000m },
            new BuildingLotSummary { Id = "free-cheap",     SuitableTypes = "FACTORY", Price = 80_000m },
        };
        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");
        Assert.Equal("free-cheap", result!.Id);
    }

    [Fact]
    public void PickCheapestAvailableLot_LotWithMultipleTypes_MatchesCorrectly()
    {
        var lots = new[]
        {
            new BuildingLotSummary { Id = "combo",    SuitableTypes = "FACTORY,MINE", Price = 90_000m },
            new BuildingLotSummary { Id = "mine-only",SuitableTypes = "MINE",          Price = 60_000m },
        };

        // Asking for FACTORY should only match the combo lot.
        var factory = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");
        Assert.Equal("combo", factory!.Id);

        // Asking for MINE returns mine-only (cheapest).
        var mine = OnboardingHelpers.PickCheapestAvailableLot(lots, "MINE");
        Assert.Equal("mine-only", mine!.Id);
    }

    [Fact]
    public void PickCheapestAvailableLot_ShopSelection_PicksCorrectType()
    {
        var lots = new[]
        {
            new BuildingLotSummary { Id = "factory-lot", SuitableTypes = "FACTORY",    Price = 50_000m },
            new BuildingLotSummary { Id = "shop-lot",    SuitableTypes = "SALES_SHOP", Price = 120_000m },
        };
        var shopLot = OnboardingHelpers.PickCheapestAvailableLot(lots, "SALES_SHOP");
        Assert.Equal("shop-lot", shopLot!.Id);
    }

    // ── PickCheapestFreeProduct ───────────────────────────────────────────────

    [Fact]
    public void PickCheapestFreeProduct_EmptyList_ReturnsNull()
    {
        var result = OnboardingHelpers.PickCheapestFreeProduct([]);
        Assert.Null(result);
    }

    [Fact]
    public void PickCheapestFreeProduct_AllProOnly_ReturnsNull()
    {
        var products = new[]
        {
            new ProductTypeSummary { Id = "p1", IsProOnly = true, BasePrice = 10m },
            new ProductTypeSummary { Id = "p2", IsProOnly = true, BasePrice = 5m },
        };
        var result = OnboardingHelpers.PickCheapestFreeProduct(products);
        Assert.Null(result);
    }

    [Fact]
    public void PickCheapestFreeProduct_SingleFreeProduct_ReturnsThatProduct()
    {
        var products = new[]
        {
            new ProductTypeSummary { Id = "wooden-chair", IsProOnly = false, BasePrice = 45m },
        };
        var result = OnboardingHelpers.PickCheapestFreeProduct(products);
        Assert.Equal("wooden-chair", result!.Id);
    }

    [Fact]
    public void PickCheapestFreeProduct_MixedProAndFree_SkipsProAndPicksCheapestFree()
    {
        var products = new[]
        {
            new ProductTypeSummary { Id = "pro-expensive", IsProOnly = true,  BasePrice = 5m   },
            new ProductTypeSummary { Id = "free-cheap",    IsProOnly = false, BasePrice = 3m   },
            new ProductTypeSummary { Id = "free-expensive",IsProOnly = false, BasePrice = 50m  },
        };
        var result = OnboardingHelpers.PickCheapestFreeProduct(products);
        Assert.Equal("free-cheap", result!.Id);
    }

    [Fact]
    public void PickCheapestFreeProduct_MultipleFreePicker_SelectsCheapest()
    {
        var products = new[]
        {
            new ProductTypeSummary { Id = "bread",       IsProOnly = false, BasePrice = 3m   },
            new ProductTypeSummary { Id = "flour",       IsProOnly = false, BasePrice = 2m   },
            new ProductTypeSummary { Id = "basic-med",   IsProOnly = false, BasePrice = 50m  },
            new ProductTypeSummary { Id = "wooden-chair",IsProOnly = false, BasePrice = 45m  },
        };
        var result = OnboardingHelpers.PickCheapestFreeProduct(products);
        Assert.Equal("flour", result!.Id);
    }

    [Fact]
    public void PickCheapestFreeProduct_ProOnlyProductWithLowerPrice_NotSelected()
    {
        // Even if the Pro product has the lowest price, it must be skipped.
        var products = new[]
        {
            new ProductTypeSummary { Id = "pro-cheap",  IsProOnly = true,  BasePrice = 1m  },
            new ProductTypeSummary { Id = "free-pricey",IsProOnly = false, BasePrice = 50m },
        };
        var result = OnboardingHelpers.PickCheapestFreeProduct(products);
        Assert.Equal("free-pricey", result!.Id);
    }

    // ── Additional ContainsSuitableType edge cases ────────────────────────────

    [Fact]
    public void ContainsSuitableType_SingleItem_MatchesExactly()
    {
        // A single-item field with no commas must still match.
        Assert.True(OnboardingHelpers.ContainsSuitableType("SALES_SHOP", "SALES_SHOP"));
    }

    [Fact]
    public void ContainsSuitableType_PrefixSubstring_DoesNotMatch()
    {
        // "MINE" should NOT match "MINER" — whole-segment requirement.
        Assert.False(OnboardingHelpers.ContainsSuitableType("MINER,STORAGE", "MINE"));
    }

    [Fact]
    public void ContainsSuitableType_SuffixSubstring_DoesNotMatch()
    {
        // "SHOP" should NOT match "SALES_SHOP" — it is a suffix, not the whole segment.
        Assert.False(OnboardingHelpers.ContainsSuitableType("SALES_SHOP", "SHOP"));
    }

    // ── Additional PickCheapestAvailableLot edge cases ────────────────────────

    [Fact]
    public void PickCheapestAvailableLot_NullBuildingId_TreatedAsAvailable()
    {
        // A lot with BuildingId = null should be selected as available.
        var lots = new[]
        {
            new BuildingLotSummary { Id = "free-null", SuitableTypes = "FACTORY", BuildingId = null, Price = 80_000m },
        };
        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");
        Assert.NotNull(result);
        Assert.Equal("free-null", result.Id);
    }

    [Fact]
    public void PickCheapestAvailableLot_EmptyBuildingId_TreatedAsOccupied()
    {
        // PickCheapestAvailableLot checks `BuildingId is null` — an empty string is
        // NOT null, so a lot with BuildingId="" is treated as occupied and skipped.
        var lots = new[]
        {
            new BuildingLotSummary { Id = "empty-id", SuitableTypes = "FACTORY", BuildingId = string.Empty, Price = 50_000m },
        };
        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");
        // Empty string is non-null → treated as occupied (lot is filtered out).
        Assert.Null(result);
    }
}
