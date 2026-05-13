using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

[ExtendObjectType<Company>]
public sealed class CompanyTypeExtensions
{
    public async Task<decimal> GetCash(
        [Parent] Company company,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        return await CompanyBankingService.GetTotalBalanceAsync(db, company.Id, cancellationToken);
    }

    public async Task<string> GetCurrencyCode(
        [Parent] Company company,
        [Service] AppDbContext db)
    {
        return await db.Buildings
            .AsNoTracking()
            .Where(building => building.CompanyId == company.Id)
            .Select(building => building.City != null ? building.City.CurrencyCode : "EUR")
            .FirstOrDefaultDeterministicAsync() ?? "EUR";
    }

    /// <summary>
    /// Returns the buildings owned by this company, excluding ones the owner has
    /// dismissed from their list (<see cref="Building.IsDismissedByOwner"/> = true).
    /// Dismissed buildings are destroyed and the player has chosen to hide them.
    /// </summary>
    public async Task<List<Building>> GetBuildings(
        [Parent] Company company,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        return await db.Buildings
            .AsNoTracking()
            .Where(b => b.CompanyId == company.Id && !b.IsDismissedByOwner)
            .Include(b => b.Units)
            .ToListAsync(cancellationToken);
    }
}