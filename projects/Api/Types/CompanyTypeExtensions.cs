using Api.Data;
using Api.Data.Entities;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

[ExtendObjectType<Company>]
public sealed class CompanyTypeExtensions
{
    public async Task<string> GetCurrencyCode(
        [Parent] Company company,
        [Service] AppDbContext db)
    {
        return await db.Buildings
            .AsNoTracking()
            .Where(building => building.CompanyId == company.Id)
            .Select(building => building.City != null ? building.City.CurrencyCode : "EUR")
            .FirstOrDefaultAsync() ?? "EUR";
    }
}