using System.Security.Claims;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    private const int EncyclopediaPageSize = 24;

    public async Task<EncyclopediaResourcePage> GetEncyclopediaResources(
        string? search,
        int? page,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var hasActivePro = await GetEncyclopediaHasActiveProSubscriptionAsync(db, httpContextAccessor);

        var resources = await db.ResourceTypes
            .AsNoTracking()
            .OrderBy(resource => resource.Name)
            .ToListAsync();

        var products = await db.ProductTypes
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .ToListAsync();
        ProductAccessService.ApplyAccessMetadata(products, hasActivePro);

        var entries = resources
            .Select(MapEncyclopediaResourceEntry)
            .Concat(products.Select(MapEncyclopediaProductEntry))
            .OrderBy(entry => entry.Name)
            .ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            entries = entries
                .Where(entry =>
                    entry.Name.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || entry.Slug.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || (entry.Description?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false)
                    || entry.Category.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || (entry.Industry?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
        }

        var safePage = Math.Max(1, page ?? 1);
        var totalCount = entries.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)EncyclopediaPageSize));
        if (safePage > totalPages)
        {
            safePage = totalPages;
        }

        return new EncyclopediaResourcePage
        {
            Page = safePage,
            TotalPages = totalPages,
            TotalCount = totalCount,
            Items = entries
                .Skip((safePage - 1) * EncyclopediaPageSize)
                .Take(EncyclopediaPageSize)
                .ToList(),
        };
    }

    public async Task<EncyclopediaEntryDetail?> GetEncyclopediaResourceDetail(
        string slug,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var hasActivePro = await GetEncyclopediaHasActiveProSubscriptionAsync(db, httpContextAccessor);

        var resource = await db.ResourceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug);

        if (resource is not null)
        {
            var downstreamProducts = await db.ProductTypes
                .AsNoTracking()
                .Include(product => product.Recipes)
                .ThenInclude(recipe => recipe.ResourceType)
                .Include(product => product.Recipes)
                .ThenInclude(recipe => recipe.InputProductType)
                .Where(product => product.Recipes.Any(recipe => recipe.ResourceTypeId == resource.Id))
                .OrderBy(product => product.Name)
                .ToListAsync();

            ProductAccessService.ApplyAccessMetadata(downstreamProducts, hasActivePro);

            return new EncyclopediaEntryDetail
            {
                Entry = MapEncyclopediaResourceEntry(resource),
                ProducedByRecipes = [CreateMiningRecipe(resource)],
                UsedInRecipes = downstreamProducts.Select(CreateProductRecipeCard).ToList(),
            };
        }

        var productType = await db.ProductTypes
            .AsNoTracking()
            .Include(product => product.Recipes)
            .ThenInclude(recipe => recipe.ResourceType)
            .Include(product => product.Recipes)
            .ThenInclude(recipe => recipe.InputProductType)
            .FirstOrDefaultAsync(candidate => candidate.Slug == slug);

        if (productType is null)
        {
            return null;
        }

        var downstreamProductsUsingProduct = await db.ProductTypes
            .AsNoTracking()
            .Include(product => product.Recipes)
            .ThenInclude(recipe => recipe.ResourceType)
            .Include(product => product.Recipes)
            .ThenInclude(recipe => recipe.InputProductType)
            .Where(product => product.Recipes.Any(recipe => recipe.InputProductTypeId == productType.Id))
            .OrderBy(product => product.Name)
            .ToListAsync();

        var productsToApplyAccess = downstreamProductsUsingProduct
            .Append(productType)
            .ToList();
        ProductAccessService.ApplyAccessMetadata(productsToApplyAccess, hasActivePro);

        return new EncyclopediaEntryDetail
        {
            Entry = MapEncyclopediaProductEntry(productType),
            ProducedByRecipes = [CreateProductRecipeCard(productType)],
            UsedInRecipes = downstreamProductsUsingProduct.Select(CreateProductRecipeCard).ToList(),
        };
    }

    private static EncyclopediaCatalogEntry MapEncyclopediaResourceEntry(ResourceType resource)
        => new()
        {
            Id = resource.Id,
            Kind = "RESOURCE",
            Name = resource.Name,
            Slug = resource.Slug,
            Category = resource.Category,
            Description = resource.Description,
            ImageUrl = resource.ImageUrl,
            IsPerishable = false,
            IsProOnly = false,
            IsUnlockedForCurrentPlayer = true,
            BasePrice = resource.BasePrice,
            WeightPerUnit = resource.WeightPerUnit,
            UnitName = resource.UnitName,
            UnitSymbol = resource.UnitSymbol,
        };

    private static EncyclopediaCatalogEntry MapEncyclopediaProductEntry(ProductType product)
        => new()
        {
            Id = product.Id,
            Kind = "PRODUCT",
            Name = product.Name,
            Slug = product.Slug,
            Category = product.Industry,
            Industry = product.Industry,
            Description = product.Description,
            IsPerishable = product.IsPerishable,
            IsProOnly = product.IsProOnly,
            IsUnlockedForCurrentPlayer = product.IsUnlockedForCurrentPlayer,
            BasePrice = product.BasePrice,
            BaseCraftTicks = product.BaseCraftTicks,
            OutputQuantity = product.OutputQuantity,
            EnergyConsumptionMwh = product.EnergyConsumptionMwh,
            BasicLaborHours = product.BasicLaborHours,
            UnitName = product.UnitName,
            UnitSymbol = product.UnitSymbol,
        };

    private static EncyclopediaRecipeCard CreateMiningRecipe(ResourceType resource)
        => new()
        {
            Id = $"mine:{resource.Slug}",
            RecipeName = $"{resource.Name} Extraction",
            BuildingType = BuildingType.Mine,
            OutputQuantity = 1m,
            Output = MapEncyclopediaResourceEntry(resource),
        };

    private static EncyclopediaRecipeCard CreateProductRecipeCard(ProductType product)
        => new()
        {
            Id = $"product:{product.Slug}",
            RecipeName = $"{product.Name} Recipe",
            BuildingType = BuildingType.Factory,
            OutputQuantity = product.OutputQuantity,
            Output = MapEncyclopediaProductEntry(product),
            Inputs = product.Recipes
                .OrderBy(recipe => recipe.ResourceType?.Name ?? recipe.InputProductType!.Name)
                .Select(MapEncyclopediaRecipeIngredient)
                .ToList(),
        };

    private static EncyclopediaRecipeIngredient MapEncyclopediaRecipeIngredient(ProductRecipe recipe)
    {
        if (recipe.ResourceType is not null)
        {
            return new EncyclopediaRecipeIngredient
            {
                Kind = "RESOURCE",
                Name = recipe.ResourceType.Name,
                Slug = recipe.ResourceType.Slug,
                Category = recipe.ResourceType.Category,
                ImageUrl = recipe.ResourceType.ImageUrl,
                Quantity = recipe.Quantity,
                UnitName = recipe.ResourceType.UnitName,
                UnitSymbol = recipe.ResourceType.UnitSymbol,
                IsUnlockedForCurrentPlayer = true,
            };
        }

        var inputProduct = recipe.InputProductType!;
        return new EncyclopediaRecipeIngredient
        {
            Kind = "PRODUCT",
            Name = inputProduct.Name,
            Slug = inputProduct.Slug,
            Category = inputProduct.Industry,
            Industry = inputProduct.Industry,
            Quantity = recipe.Quantity,
            UnitName = inputProduct.UnitName,
            UnitSymbol = inputProduct.UnitSymbol,
            IsPerishable = inputProduct.IsPerishable,
            IsProOnly = inputProduct.IsProOnly,
            IsUnlockedForCurrentPlayer = inputProduct.IsUnlockedForCurrentPlayer,
        };
    }

    private static async Task<bool> GetEncyclopediaHasActiveProSubscriptionAsync(
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor)
    {
        if (httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        var userId = httpContextAccessor.HttpContext.User.GetRequiredUserId();
        var subscriptionEndsAtUtc = await db.Players
            .Where(player => player.Id == userId)
            .Select(player => player.ProSubscriptionEndsAtUtc)
            .FirstOrDefaultDeterministicAsync();

        return ProductAccessService.HasActiveProSubscription(subscriptionEndsAtUtc, DateTime.UtcNow);
    }
}
