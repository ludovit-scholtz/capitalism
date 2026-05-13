using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    private void SeedProducts()
    {
        var seeds = GetProductSeeds();
        var proOnlySlugs = DetermineInitialProOnlyProductSlugs(seeds);

        dbContext.ProductTypes.AddRange(seeds.Select(seed => new ProductType
        {
            Id = CreateDeterministicGuid($"product:{seed.Slug}"),
            Name = seed.Name,
            Slug = seed.Slug,
            Industry = seed.Industry,
            BasePrice = seed.BasePrice,
            PriceElasticity = seed.PriceElasticity,
            BaseCraftTicks = seed.BaseCraftTicks,
            OutputQuantity = seed.OutputQuantity,
            EnergyConsumptionMwh = seed.EnergyConsumptionMwh,
            BasicLaborHours = seed.BasicLaborHours,
            IsProOnly = proOnlySlugs.Contains(seed.Slug),
            UnitName = seed.UnitName,
            UnitSymbol = seed.UnitSymbol,
            Description = seed.Description,
            IsPerishable = seed.IsPerishable
        }));
    }

    /// <summary>
    /// Idempotent upgrade: ensures the three Electronics starter products with direct Silicon
    /// recipes are present.  Databases seeded before the Electronics Pro-starter increment
    /// will not have <c>basic-electronics</c> or <c>led-screen</c>, and will have
    /// <c>circuit-board</c> using old product-ingredient recipes.
    /// </summary>
    private async Task EnsureElectronicsStarterProductsAsync()
    {
        var silicon = await dbContext.ResourceTypes.FirstOrDefaultAsync(r => r.Slug == "silicon");
        if (silicon == null) return;

        // Products to ensure exist with a direct Silicon resource recipe.
        var starterSeeds = new[]
        {
            (Slug: "basic-electronics",  Name: "Basic Electronics",  BasePrice: 45m,  CraftTicks: 3, Output: 12m, Energy: 1.0m, Description: "A starter pack of electronic components assembled from raw silicon. The entry point for any electronics manufacturer.", UnitName: "Pack",    UnitSymbol: "packs",   SiliconQty: 1m),
            (Slug: "led-screen",         Name: "LED Screen",         BasePrice: 85m,  CraftTicks: 4, Output: 6m,  Energy: 1.3m, Description: "A flat-panel LED display made from silicon. High-margin starter product for premium retail channels.",              UnitName: "Display",  UnitSymbol: "displays", SiliconQty: 1m),
            (Slug: "circuit-board",      Name: "Circuit Board",      BasePrice: 55m,  CraftTicks: 3, Output: 10m, Energy: 1.1m, Description: "A populated circuit board assembled from silicon. Core platform for advanced electronics assemblies.",               UnitName: "Board",    UnitSymbol: "boards",   SiliconQty: 2m),
        };

        foreach (var seed in starterSeeds)
        {
            var productId = CreateDeterministicGuid($"product:{seed.Slug}");
            var existing = await dbContext.ProductTypes
                .Include(p => p.Recipes)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (existing == null)
            {
                // Product doesn't exist yet — add it.
                var labor = ComputeBasicLaborHours(seed.CraftTicks, seed.Energy, 1);
                var elasticity = DeterminePriceElasticity(Industry.Electronics);
                var product = new ProductType
                {
                    Id = productId,
                    Name = seed.Name,
                    Slug = seed.Slug,
                    Industry = Industry.Electronics,
                    BasePrice = seed.BasePrice,
                    PriceElasticity = elasticity,
                    BaseCraftTicks = seed.CraftTicks,
                    OutputQuantity = seed.Output,
                    EnergyConsumptionMwh = seed.Energy,
                    BasicLaborHours = labor,
                    IsProOnly = true,
                    UnitName = seed.UnitName,
                    UnitSymbol = seed.UnitSymbol,
                    Description = seed.Description
                };
                dbContext.ProductTypes.Add(product);

                dbContext.ProductRecipes.Add(new ProductRecipe
                {
                    Id = CreateDeterministicGuid($"recipe:{seed.Slug}:silicon"),
                    ProductTypeId = productId,
                    ResourceTypeId = silicon.Id,
                    Quantity = seed.SiliconQty
                });
            }
            else
            {
                // Product exists — ensure it has at least one direct Silicon resource recipe
                // (circuit-board was originally seeded with product-ingredient recipes only).
                var hasSiliconRecipe = existing.Recipes.Any(r => r.ResourceTypeId == silicon.Id);
                if (!hasSiliconRecipe)
                {
                    // Remove old product-ingredient recipes and replace with silicon.
                    dbContext.ProductRecipes.RemoveRange(existing.Recipes);
                    dbContext.ProductRecipes.Add(new ProductRecipe
                    {
                        Id = CreateDeterministicGuid($"recipe:{seed.Slug}:silicon"),
                        ProductTypeId = productId,
                        ResourceTypeId = silicon.Id,
                        Quantity = seed.SiliconQty
                    });
                }

                // Ensure the product is correctly marked as Pro-only.
                if (!existing.IsProOnly)
                {
                    existing.IsProOnly = true;
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent upgrade: ensures the three Construction starter products with direct Iron Ore
    /// recipes are present. Databases seeded before the Construction Pro-starter increment
    /// will not have <c>residential-block</c>, <c>commercial-block</c>, or <c>industrial-block</c>.
    /// </summary>
    private async Task EnsureConstructionStarterProductsAsync()
    {
        var ironOre = await dbContext.ResourceTypes.FirstOrDefaultAsync(r => r.Slug == "iron-ore");
        if (ironOre == null) return;

        // Products to ensure exist with a direct Iron Ore resource recipe.
        var starterSeeds = new[]
        {
            (Slug: "residential-block",  Name: "Residential Block",  BasePrice: 80m,  CraftTicks: 3, Output: 8m,  Energy: 1.2m, Description: "A prefabricated residential building block made from processed iron. The entry point for any construction company entering the housing market.", UnitName: "Block",  UnitSymbol: "blocks", IronOreQty: 2m),
            (Slug: "commercial-block",   Name: "Commercial Block",   BasePrice: 120m, CraftTicks: 4, Output: 5m,  Energy: 1.5m, Description: "A structural block for commercial buildings. Higher iron content means premium durability for shops, offices, and service buildings.", UnitName: "Block",  UnitSymbol: "blocks", IronOreQty: 3m),
            (Slug: "industrial-block",   Name: "Industrial Block",   BasePrice: 180m, CraftTicks: 5, Output: 3m,  Energy: 1.8m, Description: "A heavy-duty industrial building block engineered for factories and warehouses. Maximum iron content for maximum load-bearing capacity.", UnitName: "Block",  UnitSymbol: "blocks", IronOreQty: 4m),
        };

        foreach (var seed in starterSeeds)
        {
            var productId = CreateDeterministicGuid($"product:{seed.Slug}");
            var existing = await dbContext.ProductTypes
                .Include(p => p.Recipes)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (existing == null)
            {
                // Product doesn't exist yet — add it.
                var labor = ComputeBasicLaborHours(seed.CraftTicks, seed.Energy, 1);
                var elasticity = DeterminePriceElasticity(Industry.Construction);
                var product = new ProductType
                {
                    Id = productId,
                    Name = seed.Name,
                    Slug = seed.Slug,
                    Industry = Industry.Construction,
                    BasePrice = seed.BasePrice,
                    PriceElasticity = elasticity,
                    BaseCraftTicks = seed.CraftTicks,
                    OutputQuantity = seed.Output,
                    EnergyConsumptionMwh = seed.Energy,
                    BasicLaborHours = labor,
                    IsProOnly = true,
                    UnitName = seed.UnitName,
                    UnitSymbol = seed.UnitSymbol,
                    Description = seed.Description
                };
                dbContext.ProductTypes.Add(product);

                dbContext.ProductRecipes.Add(new ProductRecipe
                {
                    Id = CreateDeterministicGuid($"recipe:{seed.Slug}:iron-ore"),
                    ProductTypeId = productId,
                    ResourceTypeId = ironOre.Id,
                    Quantity = seed.IronOreQty
                });
            }
            else
            {
                // Ensure the product is correctly marked as Pro-only and has an Iron Ore recipe.
                var hasIronOreRecipe = existing.Recipes.Any(r => r.ResourceTypeId == ironOre.Id);
                if (!hasIronOreRecipe)
                {
                    dbContext.ProductRecipes.Add(new ProductRecipe
                    {
                        Id = CreateDeterministicGuid($"recipe:{seed.Slug}:iron-ore"),
                        ProductTypeId = productId,
                        ResourceTypeId = ironOre.Id,
                        Quantity = seed.IronOreQty
                    });
                }

                if (!existing.IsProOnly)
                {
                    existing.IsProOnly = true;
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent upgrade: ensures the three Pharmaceuticals starter products with direct Gold
    /// recipes are present.  Databases seeded before the Pharmaceuticals Pro-starter increment
    /// will not have <c>aspirin</c>, <c>vitamin-capsule</c>, or <c>antibiotic</c>.
    /// </summary>
    private async Task EnsurePharmaceuticalsStarterProductsAsync()
    {
        var gold = await dbContext.ResourceTypes.FirstOrDefaultAsync(r => r.Slug == "gold");
        if (gold == null) return;

        var starterSeeds = new[]
        {
            (Slug: "aspirin",         Name: "Aspirin",         BasePrice: 55m,  CraftTicks: 3, Output: 10m, Energy: 1.0m, Description: "A starter pharmaceutical tablet synthesised from refined gold compounds. The entry point for any pharmaceutical manufacturer.", UnitName: "Bottle", UnitSymbol: "bottles", GoldQty: 1m),
            (Slug: "vitamin-capsule", Name: "Vitamin Capsule", BasePrice: 80m,  CraftTicks: 4, Output: 6m,  Energy: 1.2m, Description: "Premium vitamin supplement produced from pure gold compounds. High-margin product for health-conscious markets.",               UnitName: "Pack",   UnitSymbol: "packs",   GoldQty: 1m),
            (Slug: "antibiotic",      Name: "Antibiotic",      BasePrice: 120m, CraftTicks: 5, Output: 4m,  Energy: 1.5m, Description: "A broad-spectrum antibiotic formulated from concentrated gold catalyst compounds. Maximum margin in any pharmacy product line.",  UnitName: "Box",    UnitSymbol: "boxes",   GoldQty: 2m),
        };

        foreach (var seed in starterSeeds)
        {
            var productId = CreateDeterministicGuid($"product:{seed.Slug}");
            var existing = await dbContext.ProductTypes
                .Include(p => p.Recipes)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (existing == null)
            {
                var labor = ComputeBasicLaborHours(seed.CraftTicks, seed.Energy, 1);
                var elasticity = DeterminePriceElasticity(Industry.Pharmaceuticals);
                var product = new ProductType
                {
                    Id = productId,
                    Name = seed.Name,
                    Slug = seed.Slug,
                    Industry = Industry.Pharmaceuticals,
                    BasePrice = seed.BasePrice,
                    PriceElasticity = elasticity,
                    BaseCraftTicks = seed.CraftTicks,
                    OutputQuantity = seed.Output,
                    EnergyConsumptionMwh = seed.Energy,
                    BasicLaborHours = labor,
                    IsProOnly = true,
                    UnitName = seed.UnitName,
                    UnitSymbol = seed.UnitSymbol,
                    Description = seed.Description
                };
                dbContext.ProductTypes.Add(product);

                dbContext.ProductRecipes.Add(new ProductRecipe
                {
                    Id = CreateDeterministicGuid($"recipe:{seed.Slug}:gold"),
                    ProductTypeId = productId,
                    ResourceTypeId = gold.Id,
                    Quantity = seed.GoldQty
                });
            }
            else
            {
                var hasGoldRecipe = existing.Recipes.Any(r => r.ResourceTypeId == gold.Id);
                if (!hasGoldRecipe)
                {
                    dbContext.ProductRecipes.Add(new ProductRecipe
                    {
                        Id = CreateDeterministicGuid($"recipe:{seed.Slug}:gold"),
                        ProductTypeId = productId,
                        ResourceTypeId = gold.Id,
                        Quantity = seed.GoldQty
                    });
                }

                if (!existing.IsProOnly)
                {
                    existing.IsProOnly = true;
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent upgrade: ensures the three Energy starter products with direct Coal
    /// recipes are present.  Databases seeded before the Energy Pro-starter increment
    /// will not have <c>coal-briquette</c>, <c>heating-oil</c>, or <c>industrial-fuel</c>.
    /// </summary>
    private async Task EnsureEnergyStarterProductsAsync()
    {
        var coal = await dbContext.ResourceTypes.FirstOrDefaultAsync(r => r.Slug == "coal");
        if (coal == null) return;

        var starterSeeds = new[]
        {
            (Slug: "coal-briquette",  Name: "Coal Briquette",  BasePrice: 28m, CraftTicks: 2, Output: 15m, Energy: 0.8m, Description: "A compressed coal briquette providing consistent heat output for domestic and industrial furnaces. The entry point for any energy producer.", UnitName: "Bag",    UnitSymbol: "bags",    CoalQty: 2m),
            (Slug: "heating-oil",     Name: "Heating Oil",     BasePrice: 50m, CraftTicks: 3, Output: 8m,  Energy: 1.1m, Description: "Refined heating oil distilled from coal for residential and commercial heating systems. Steady demand across all seasons.",               UnitName: "Barrel", UnitSymbol: "barrels", CoalQty: 3m),
            (Slug: "industrial-fuel", Name: "Industrial Fuel", BasePrice: 75m, CraftTicks: 4, Output: 5m,  Energy: 1.4m, Description: "High-density industrial fuel refined from premium coal stocks. Powers factories, generators, and heavy machinery.",                       UnitName: "Drum",   UnitSymbol: "drums",   CoalQty: 4m),
        };

        foreach (var seed in starterSeeds)
        {
            var productId = CreateDeterministicGuid($"product:{seed.Slug}");
            var existing = await dbContext.ProductTypes
                .Include(p => p.Recipes)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (existing == null)
            {
                var labor = ComputeBasicLaborHours(seed.CraftTicks, seed.Energy, 1);
                var elasticity = DeterminePriceElasticity(Industry.Energy);
                var product = new ProductType
                {
                    Id = productId,
                    Name = seed.Name,
                    Slug = seed.Slug,
                    Industry = Industry.Energy,
                    BasePrice = seed.BasePrice,
                    PriceElasticity = elasticity,
                    BaseCraftTicks = seed.CraftTicks,
                    OutputQuantity = seed.Output,
                    EnergyConsumptionMwh = seed.Energy,
                    BasicLaborHours = labor,
                    IsProOnly = true,
                    UnitName = seed.UnitName,
                    UnitSymbol = seed.UnitSymbol,
                    Description = seed.Description
                };
                dbContext.ProductTypes.Add(product);

                dbContext.ProductRecipes.Add(new ProductRecipe
                {
                    Id = CreateDeterministicGuid($"recipe:{seed.Slug}:coal"),
                    ProductTypeId = productId,
                    ResourceTypeId = coal.Id,
                    Quantity = seed.CoalQty
                });
            }
            else
            {
                var hasCoalRecipe = existing.Recipes.Any(r => r.ResourceTypeId == coal.Id);
                if (!hasCoalRecipe)
                {
                    dbContext.ProductRecipes.Add(new ProductRecipe
                    {
                        Id = CreateDeterministicGuid($"recipe:{seed.Slug}:coal"),
                        ProductTypeId = productId,
                        ResourceTypeId = coal.Id,
                        Quantity = seed.CoalQty
                    });
                }

                if (!existing.IsProOnly)
                {
                    existing.IsProOnly = true;
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent upgrade: ensures the three Logistics starter products with direct Cotton
    /// recipes are present.  Databases seeded before the Logistics Pro-starter increment
    /// will not have <c>shipping-bag</c>, <c>storage-sack</c>, or <c>cargo-pack</c>.
    /// </summary>
    private async Task EnsureLogisticsStarterProductsAsync()
    {
        var cotton = await dbContext.ResourceTypes.FirstOrDefaultAsync(r => r.Slug == "cotton");
        if (cotton == null) return;

        var starterSeeds = new[]
        {
            (Slug: "shipping-bag", Name: "Shipping Bag", BasePrice: 20m, CraftTicks: 2, Output: 18m, Energy: 0.6m, Description: "A durable cotton shipping bag for consumer goods distribution. The entry point for any logistics manufacturer.", UnitName: "Bag",  UnitSymbol: "bags",  CottonQty: 1m),
            (Slug: "storage-sack", Name: "Storage Sack", BasePrice: 35m, CraftTicks: 3, Output: 10m, Energy: 0.9m, Description: "Reinforced cotton storage sack for bulk commodity warehousing. High-volume demand from agricultural and industrial buyers.", UnitName: "Sack", UnitSymbol: "sacks", CottonQty: 2m),
            (Slug: "cargo-pack",   Name: "Cargo Pack",   BasePrice: 55m, CraftTicks: 4, Output: 6m,  Energy: 1.2m, Description: "Heavy-duty cotton cargo pack built for international shipping and warehouse handling. Premium packaging for high-value goods.",  UnitName: "Pack", UnitSymbol: "packs", CottonQty: 3m),
        };

        foreach (var seed in starterSeeds)
        {
            var productId = CreateDeterministicGuid($"product:{seed.Slug}");
            var existing = await dbContext.ProductTypes
                .Include(p => p.Recipes)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (existing == null)
            {
                var labor = ComputeBasicLaborHours(seed.CraftTicks, seed.Energy, 1);
                var elasticity = DeterminePriceElasticity(Industry.Logistics);
                var product = new ProductType
                {
                    Id = productId,
                    Name = seed.Name,
                    Slug = seed.Slug,
                    Industry = Industry.Logistics,
                    BasePrice = seed.BasePrice,
                    PriceElasticity = elasticity,
                    BaseCraftTicks = seed.CraftTicks,
                    OutputQuantity = seed.Output,
                    EnergyConsumptionMwh = seed.Energy,
                    BasicLaborHours = labor,
                    IsProOnly = true,
                    UnitName = seed.UnitName,
                    UnitSymbol = seed.UnitSymbol,
                    Description = seed.Description
                };
                dbContext.ProductTypes.Add(product);

                dbContext.ProductRecipes.Add(new ProductRecipe
                {
                    Id = CreateDeterministicGuid($"recipe:{seed.Slug}:cotton"),
                    ProductTypeId = productId,
                    ResourceTypeId = cotton.Id,
                    Quantity = seed.CottonQty
                });
            }
            else
            {
                var hasCottonRecipe = existing.Recipes.Any(r => r.ResourceTypeId == cotton.Id);
                if (!hasCottonRecipe)
                {
                    dbContext.ProductRecipes.Add(new ProductRecipe
                    {
                        Id = CreateDeterministicGuid($"recipe:{seed.Slug}:cotton"),
                        ProductTypeId = productId,
                        ResourceTypeId = cotton.Id,
                        Quantity = seed.CottonQty
                    });
                }

                if (!existing.IsProOnly)
                {
                    existing.IsProOnly = true;
                }
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private static IReadOnlyList<ProductSeed> GetProductSeeds() =>
    [
        .. GetFurnitureProducts(),
        .. GetFoodProducts(),
        .. GetHealthcareProducts(),
        .. GetElectronicsProducts(),
        .. GetConstructionProducts(),
        .. GetPharmaceuticalsProducts(),
        .. GetEnergyProducts(),
        .. GetLogisticsProducts()
    ];

    private static HashSet<string> DetermineInitialProOnlyProductSlugs(IReadOnlyList<ProductSeed> seeds)
    {
        var proOnlySlugs = seeds
            .Where(seed => seed.Industry is Industry.Electronics or Industry.Construction
                                         or Industry.Pharmaceuticals or Industry.Energy or Industry.Logistics)
            .Select(seed => seed.Slug)
            .ToHashSet(StringComparer.Ordinal);

        var changed = true;
        while (changed)
        {
            changed = false;

            foreach (var seed in seeds)
            {
                if (proOnlySlugs.Contains(seed.Slug))
                {
                    continue;
                }

                if (seed.Ingredients.Any(ingredient => ingredient.ProductSlug is not null && proOnlySlugs.Contains(ingredient.ProductSlug)))
                {
                    changed |= proOnlySlugs.Add(seed.Slug);
                }
            }
        }

        return proOnlySlugs;
    }

}
