using Api.Configuration;
using Api.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Data;

/// <summary>
/// Seeds the database with initial game data: admin player, cities, resources, products, and recipes.
/// Called at application startup.
/// </summary>
public sealed class AppDbInitializer(
    AppDbContext dbContext,
    IOptions<SeedDataOptions> seedOptions)
{
    /// <summary>
    /// Ensures the schema exists and seeds initial data if missing.
    /// </summary>
    public async Task InitializeAsync()
    {
        await dbContext.Database.EnsureCreatedAsync();

        // Seed admin player
        if (!await dbContext.Players.AnyAsync(p => p.Email == seedOptions.Value.AdminEmail))
        {
            var hasher = new PasswordHasher<Player>();
            var admin = new Player
            {
                Id = Guid.NewGuid(),
                Email = seedOptions.Value.AdminEmail,
                DisplayName = seedOptions.Value.AdminDisplayName,
                Role = PlayerRole.Admin,
                CreatedAtUtc = DateTime.UtcNow
            };
            admin.PasswordHash = hasher.HashPassword(admin, seedOptions.Value.AdminPassword);
            dbContext.Players.Add(admin);
        }

        // Seed game state
        if (!await dbContext.GameStates.AnyAsync())
        {
            dbContext.GameStates.Add(new GameState { Id = 1, CurrentTick = 0 });
        }

        // Seed resource types
        if (!await dbContext.ResourceTypes.AnyAsync())
        {
            SeedResources();
        }

        // Seed cities
        if (!await dbContext.Cities.AnyAsync())
        {
            SeedCities();
        }

        // Seed product types and recipes
        if (!await dbContext.ProductTypes.AnyAsync())
        {
            SeedProducts();
        }

        await dbContext.SaveChangesAsync();

        // Seed city resources (needs resource type IDs)
        if (!await dbContext.CityResources.AnyAsync())
        {
            await SeedCityResourcesAsync();
            await dbContext.SaveChangesAsync();
        }

        // Seed product recipes (needs resource and product IDs)
        if (!await dbContext.ProductRecipes.AnyAsync())
        {
            await SeedRecipesAsync();
            await dbContext.SaveChangesAsync();
        }
    }

    private void SeedResources()
    {
        var resources = new[]
        {
            new ResourceType { Id = Guid.NewGuid(), Name = "Wood", Slug = "wood", Category = "ORGANIC", BasePrice = 10m, WeightPerUnit = 5m, Description = "Harvested timber used in furniture and construction." },
            new ResourceType { Id = Guid.NewGuid(), Name = "Iron Ore", Slug = "iron-ore", Category = "MINERAL", BasePrice = 25m, WeightPerUnit = 10m, Description = "Raw iron extracted from mines, smelted into steel." },
            new ResourceType { Id = Guid.NewGuid(), Name = "Coal", Slug = "coal", Category = "MINERAL", BasePrice = 8m, WeightPerUnit = 8m, Description = "Fossil fuel for power generation and steel production." },
            new ResourceType { Id = Guid.NewGuid(), Name = "Gold", Slug = "gold", Category = "MINERAL", BasePrice = 500m, WeightPerUnit = 0.1m, Description = "Precious metal used in electronics and jewellery." },
            new ResourceType { Id = Guid.NewGuid(), Name = "Chemical Minerals", Slug = "chemical-minerals", Category = "MINERAL", BasePrice = 30m, WeightPerUnit = 3m, Description = "Raw minerals used in pharmaceutical and healthcare products." },
            new ResourceType { Id = Guid.NewGuid(), Name = "Cotton", Slug = "cotton", Category = "ORGANIC", BasePrice = 15m, WeightPerUnit = 1m, Description = "Natural fibre for textile manufacturing." },
            new ResourceType { Id = Guid.NewGuid(), Name = "Grain", Slug = "grain", Category = "ORGANIC", BasePrice = 5m, WeightPerUnit = 2m, Description = "Cereal crops used in food processing." },
            new ResourceType { Id = Guid.NewGuid(), Name = "Silicon", Slug = "silicon", Category = "MINERAL", BasePrice = 40m, WeightPerUnit = 2m, Description = "Semiconductor material for electronics." },
        };

        dbContext.ResourceTypes.AddRange(resources);
    }

    private void SeedCities()
    {
        var cities = new[]
        {
            new City { Id = Guid.NewGuid(), Name = "Bratislava", CountryCode = "SK", Latitude = 48.1486, Longitude = 17.1077, Population = 475_000, AverageRentPerSqm = 14m },
            new City { Id = Guid.NewGuid(), Name = "Prague", CountryCode = "CZ", Latitude = 50.0755, Longitude = 14.4378, Population = 1_350_000, AverageRentPerSqm = 18m },
            new City { Id = Guid.NewGuid(), Name = "Vienna", CountryCode = "AT", Latitude = 48.2082, Longitude = 16.3738, Population = 1_900_000, AverageRentPerSqm = 22m },
        };

        dbContext.Cities.AddRange(cities);
    }

    private void SeedProducts()
    {
        var products = new[]
        {
            // Furniture industry
            new ProductType { Id = Guid.NewGuid(), Name = "Wooden Chair", Slug = "wooden-chair", Industry = Industry.Furniture, BasePrice = 45m, BaseCraftTicks = 2, Description = "A basic wooden chair." },
            new ProductType { Id = Guid.NewGuid(), Name = "Wooden Table", Slug = "wooden-table", Industry = Industry.Furniture, BasePrice = 120m, BaseCraftTicks = 4, Description = "A sturdy wooden dining table." },
            new ProductType { Id = Guid.NewGuid(), Name = "Wooden Bed", Slug = "wooden-bed", Industry = Industry.Furniture, BasePrice = 200m, BaseCraftTicks = 6, Description = "A comfortable wooden bed frame." },

            // Food processing industry
            new ProductType { Id = Guid.NewGuid(), Name = "Bread", Slug = "bread", Industry = Industry.FoodProcessing, BasePrice = 3m, BaseCraftTicks = 1, Description = "Basic wheat bread loaf." },
            new ProductType { Id = Guid.NewGuid(), Name = "Flour", Slug = "flour", Industry = Industry.FoodProcessing, BasePrice = 8m, BaseCraftTicks = 1, Description = "Milled grain flour." },

            // Healthcare industry
            new ProductType { Id = Guid.NewGuid(), Name = "Basic Medicine", Slug = "basic-medicine", Industry = Industry.Healthcare, BasePrice = 50m, BaseCraftTicks = 3, Description = "Essential pharmaceutical product." },
            new ProductType { Id = Guid.NewGuid(), Name = "Bandages", Slug = "bandages", Industry = Industry.Healthcare, BasePrice = 15m, BaseCraftTicks = 1, Description = "Medical bandages from cotton." },
        };

        dbContext.ProductTypes.AddRange(products);
    }

    private async Task SeedCityResourcesAsync()
    {
        var cities = await dbContext.Cities.ToListAsync();
        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(r => r.Slug);

        foreach (var city in cities)
        {
            // Every city gets wood and grain
            dbContext.CityResources.Add(new CityResource { Id = Guid.NewGuid(), CityId = city.Id, ResourceTypeId = resources["wood"].Id, Abundance = 0.7m });
            dbContext.CityResources.Add(new CityResource { Id = Guid.NewGuid(), CityId = city.Id, ResourceTypeId = resources["grain"].Id, Abundance = 0.6m });
        }

        // City-specific resources
        var bratislava = cities.First(c => c.Name == "Bratislava");
        dbContext.CityResources.Add(new CityResource { Id = Guid.NewGuid(), CityId = bratislava.Id, ResourceTypeId = resources["iron-ore"].Id, Abundance = 0.4m });
        dbContext.CityResources.Add(new CityResource { Id = Guid.NewGuid(), CityId = bratislava.Id, ResourceTypeId = resources["chemical-minerals"].Id, Abundance = 0.3m });

        var prague = cities.First(c => c.Name == "Prague");
        dbContext.CityResources.Add(new CityResource { Id = Guid.NewGuid(), CityId = prague.Id, ResourceTypeId = resources["coal"].Id, Abundance = 0.6m });
        dbContext.CityResources.Add(new CityResource { Id = Guid.NewGuid(), CityId = prague.Id, ResourceTypeId = resources["silicon"].Id, Abundance = 0.3m });

        var vienna = cities.First(c => c.Name == "Vienna");
        dbContext.CityResources.Add(new CityResource { Id = Guid.NewGuid(), CityId = vienna.Id, ResourceTypeId = resources["cotton"].Id, Abundance = 0.5m });
        dbContext.CityResources.Add(new CityResource { Id = Guid.NewGuid(), CityId = vienna.Id, ResourceTypeId = resources["gold"].Id, Abundance = 0.1m });
    }

    private async Task SeedRecipesAsync()
    {
        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(r => r.Slug);
        var products = await dbContext.ProductTypes.ToDictionaryAsync(p => p.Slug);

        // Wooden Chair: 3 Wood
        dbContext.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductTypeId = products["wooden-chair"].Id, ResourceTypeId = resources["wood"].Id, Quantity = 3m });

        // Wooden Table: 6 Wood
        dbContext.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductTypeId = products["wooden-table"].Id, ResourceTypeId = resources["wood"].Id, Quantity = 6m });

        // Wooden Bed: 8 Wood, 1 Cotton
        dbContext.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductTypeId = products["wooden-bed"].Id, ResourceTypeId = resources["wood"].Id, Quantity = 8m });
        dbContext.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductTypeId = products["wooden-bed"].Id, ResourceTypeId = resources["cotton"].Id, Quantity = 1m });

        // Flour: 2 Grain
        dbContext.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductTypeId = products["flour"].Id, ResourceTypeId = resources["grain"].Id, Quantity = 2m });

        // Bread: 1 Grain (simplified - would normally need flour)
        dbContext.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductTypeId = products["bread"].Id, ResourceTypeId = resources["grain"].Id, Quantity = 1m });

        // Basic Medicine: 2 Chemical Minerals
        dbContext.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductTypeId = products["basic-medicine"].Id, ResourceTypeId = resources["chemical-minerals"].Id, Quantity = 2m });

        // Bandages: 2 Cotton
        dbContext.ProductRecipes.Add(new ProductRecipe { Id = Guid.NewGuid(), ProductTypeId = products["bandages"].Id, ResourceTypeId = resources["cotton"].Id, Quantity = 2m });
    }
}
