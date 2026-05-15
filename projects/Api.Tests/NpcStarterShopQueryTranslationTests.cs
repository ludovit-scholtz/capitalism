using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public sealed class NpcStarterShopQueryTranslationTests
{
    [Fact]
    public void SalesShopLotQuery_TranslatesForNpgsql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=npc-query-translation;Username=test;Password=test")
            .Options;

        using var dbContext = new AppDbContext(options);
        var cityId = Guid.NewGuid();
        var salesShopType = BuildingType.SalesShop;

        var query = dbContext.BuildingLots
            .Where(candidate => candidate.CityId == cityId && candidate.OwnerCompanyId == null)
            .Where(candidate =>
                candidate.SuitableTypes == salesShopType
                || candidate.SuitableTypes.StartsWith($"{salesShopType},")
                || candidate.SuitableTypes.EndsWith($",{salesShopType}")
                || candidate.SuitableTypes.Contains($",{salesShopType},"))
            .OrderBy(candidate => candidate.Price);

        var sql = query.ToQueryString();

        Assert.Contains("\"SuitableTypes\"", sql, StringComparison.Ordinal);
        Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
    }
}