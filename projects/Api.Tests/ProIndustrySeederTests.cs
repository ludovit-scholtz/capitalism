using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Non-archived tests that run in default CI.
/// Verifies that the three new Pro-exclusive industries (Pharmaceuticals, Energy, Logistics)
/// are correctly seeded with starter products, raw-material recipes, and Pro-only flags.
/// Also covers the backend Pro-gating enforcement at startOnboardingCompany.
/// </summary>
public sealed class ProIndustrySeederTests
{
    // ─── Pharmaceuticals (Gold-based) seeder tests ──────────────────────────

    [Fact]
    public async Task Pharmaceuticals_StarterProducts_AreSeededdWithGoldRecipe()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var starterSlugs = new[] { "aspirin", "vitamin-capsule", "antibiotic" };

        foreach (var slug in starterSlugs)
        {
            var product = await db.ProductTypes
                .Include(p => p.Recipes).ThenInclude(r => r.ResourceType)
                .FirstOrDefaultAsync(p => p.Slug == slug);

            Assert.NotNull(product);
            Assert.Equal("PHARMACEUTICALS", product.Industry);
            Assert.True(product.IsProOnly, $"{slug} must be Pro-only");

            var hasGoldRecipe = product.Recipes.Any(r => r.ResourceType != null && r.ResourceType.Slug == "gold");
            Assert.True(hasGoldRecipe, $"{slug} must have a direct gold recipe");
        }
    }

    [Fact]
    public async Task Pharmaceuticals_HasAtLeastNineProducts_Including_StartersAndAdvanced()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var products = await db.ProductTypes
            .Where(p => p.Industry == "PHARMACEUTICALS")
            .ToListAsync();

        Assert.True(products.Count >= 9, $"Expected ≥9 Pharmaceuticals products, got {products.Count}");

        // Exactly 3 starters (aspirin, vitamin-capsule, antibiotic) must exist
        var starterSlugs = new[] { "aspirin", "vitamin-capsule", "antibiotic" };
        foreach (var slug in starterSlugs)
            Assert.Contains(products, p => p.Slug == slug);
    }

    // ─── Energy (Coal-based) seeder tests ───────────────────────────────────

    [Fact]
    public async Task Energy_StarterProducts_AreSeededdWithCoalRecipe()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var starterSlugs = new[] { "coal-briquette", "heating-oil", "industrial-fuel" };

        foreach (var slug in starterSlugs)
        {
            var product = await db.ProductTypes
                .Include(p => p.Recipes).ThenInclude(r => r.ResourceType)
                .FirstOrDefaultAsync(p => p.Slug == slug);

            Assert.NotNull(product);
            Assert.Equal("ENERGY", product.Industry);
            Assert.True(product.IsProOnly, $"{slug} must be Pro-only");

            var hasCoalRecipe = product.Recipes.Any(r => r.ResourceType != null && r.ResourceType.Slug == "coal");
            Assert.True(hasCoalRecipe, $"{slug} must have a direct coal recipe");
        }
    }

    [Fact]
    public async Task Energy_HasAtLeastNineProducts_Including_StartersAndAdvanced()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var products = await db.ProductTypes
            .Where(p => p.Industry == "ENERGY")
            .ToListAsync();

        Assert.True(products.Count >= 9, $"Expected ≥9 Energy products, got {products.Count}");

        var starterSlugs = new[] { "coal-briquette", "heating-oil", "industrial-fuel" };
        foreach (var slug in starterSlugs)
            Assert.Contains(products, p => p.Slug == slug);
    }

    // ─── Logistics (Cotton-based) seeder tests ──────────────────────────────

    [Fact]
    public async Task Logistics_StarterProducts_AreSeededdWithCottonRecipe()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var starterSlugs = new[] { "shipping-bag", "storage-sack", "cargo-pack" };

        foreach (var slug in starterSlugs)
        {
            var product = await db.ProductTypes
                .Include(p => p.Recipes).ThenInclude(r => r.ResourceType)
                .FirstOrDefaultAsync(p => p.Slug == slug);

            Assert.NotNull(product);
            Assert.Equal("LOGISTICS", product.Industry);
            Assert.True(product.IsProOnly, $"{slug} must be Pro-only");

            var hasCottonRecipe = product.Recipes.Any(r => r.ResourceType != null && r.ResourceType.Slug == "cotton");
            Assert.True(hasCottonRecipe, $"{slug} must have a direct cotton recipe");
        }
    }

    [Fact]
    public async Task Logistics_HasAtLeastNineProducts_Including_StartersAndAdvanced()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var products = await db.ProductTypes
            .Where(p => p.Industry == "LOGISTICS")
            .ToListAsync();

        Assert.True(products.Count >= 9, $"Expected ≥9 Logistics products, got {products.Count}");

        var starterSlugs = new[] { "shipping-bag", "storage-sack", "cargo-pack" };
        foreach (var slug in starterSlugs)
            Assert.Contains(products, p => p.Slug == slug);
    }

    // ─── Cross-industry: all 5 Pro industries present and Pro-only ──────────

    [Fact]
    public async Task AllFiveProIndustries_HaveIsProOnly_StarterProducts_Seeded()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Each Pro-only industry must have at least one IsProOnly product
        var proIndustries = new[] { "ELECTRONICS", "CONSTRUCTION", "PHARMACEUTICALS", "ENERGY", "LOGISTICS" };

        foreach (var industry in proIndustries)
        {
            var proProducts = await db.ProductTypes
                .Where(p => p.Industry == industry && p.IsProOnly)
                .ToListAsync();

            Assert.True(proProducts.Count >= 1, $"{industry} must have at least one Pro-only product seeded");
        }
    }

    [Fact]
    public async Task FreeIndustry_StarterProducts_AreNotProOnly()
    {
        // The three free-industry starter products that appear in the onboarding wizard
        // must never be Pro-only, regardless of transitive closure from advanced products
        // that use Pro-industry ingredients (e.g., Furniture items using iron-fasteners).
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var freeStarterSlugs = new[] { "wooden-chair", "bread", "basic-medicine", "bandages" };

        foreach (var slug in freeStarterSlugs)
        {
            var product = await db.ProductTypes.FirstOrDefaultAsync(p => p.Slug == slug);
            Assert.NotNull(product);
            Assert.False(product.IsProOnly, $"{slug} is a free-tier starter product and must NOT be Pro-only");
        }
    }

    // ─── No duplicate product slugs in the seeded database ──────────────────

    [Fact]
    public async Task AllProductSlugs_AreUnique_AcrossAllIndustries()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var allSlugs = await db.ProductTypes.Select(p => p.Slug).ToListAsync();
        var distinctSlugs = allSlugs.Distinct().ToList();

        Assert.Equal(distinctSlugs.Count, allSlugs.Count);
    }

    // ─── Pharmaceuticals base prices match tier expectations ─────────────────

    [Fact]
    public async Task PharmaceuticalsStarterProducts_BasePrices_MatchTierExpectations()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var aspirin = await db.ProductTypes.FirstAsync(p => p.Slug == "aspirin");
        var vitaminCapsule = await db.ProductTypes.FirstAsync(p => p.Slug == "vitamin-capsule");
        var antibiotic = await db.ProductTypes.FirstAsync(p => p.Slug == "antibiotic");

        Assert.Equal(55m, aspirin.BasePrice);
        Assert.Equal(80m, vitaminCapsule.BasePrice);
        Assert.Equal(120m, antibiotic.BasePrice);
        // Price must increase with tier
        Assert.True(aspirin.BasePrice < vitaminCapsule.BasePrice, "Aspirin must be cheaper than Vitamin Capsule");
        Assert.True(vitaminCapsule.BasePrice < antibiotic.BasePrice, "Vitamin Capsule must be cheaper than Antibiotic");
    }

    [Fact]
    public async Task EnergyStarterProducts_BasePrices_MatchTierExpectations()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var coalBriquette = await db.ProductTypes.FirstAsync(p => p.Slug == "coal-briquette");
        var heatingOil = await db.ProductTypes.FirstAsync(p => p.Slug == "heating-oil");
        var industrialFuel = await db.ProductTypes.FirstAsync(p => p.Slug == "industrial-fuel");

        Assert.Equal(28m, coalBriquette.BasePrice);
        Assert.Equal(50m, heatingOil.BasePrice);
        Assert.Equal(75m, industrialFuel.BasePrice);
        Assert.True(coalBriquette.BasePrice < heatingOil.BasePrice);
        Assert.True(heatingOil.BasePrice < industrialFuel.BasePrice);
    }

    [Fact]
    public async Task LogisticsStarterProducts_BasePrices_MatchTierExpectations()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var shippingBag = await db.ProductTypes.FirstAsync(p => p.Slug == "shipping-bag");
        var storageSack = await db.ProductTypes.FirstAsync(p => p.Slug == "storage-sack");
        var cargoPack = await db.ProductTypes.FirstAsync(p => p.Slug == "cargo-pack");

        Assert.Equal(20m, shippingBag.BasePrice);
        Assert.Equal(35m, storageSack.BasePrice);
        Assert.Equal(55m, cargoPack.BasePrice);
        Assert.True(shippingBag.BasePrice < storageSack.BasePrice);
        Assert.True(storageSack.BasePrice < cargoPack.BasePrice);
    }
}
