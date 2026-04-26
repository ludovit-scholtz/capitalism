using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Queries for building bank account management.
/// </summary>
public sealed partial class Query
{
    /// <summary>
    /// Returns the bank account assigned to the given building, including balance and
    /// whether the building is currently suspended for insufficient funds.
    /// Requires the caller to own the building's company.
    /// </summary>
    [Authorize]
    public async Task<BuildingBankAccountInfo?> BuildingBankAccount(
        Guid buildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.BankAccount)
            .Include(b => b.City)
            .Include(b => b.Company)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == buildingId && b.Company.PlayerId == userId);

        if (building is null)
            return null;

        var cityName = building.City?.Name ?? string.Empty;
        var currencyCode = building.City?.CurrencyCode ?? "EUR";

        if (building.BankAccount is null)
        {
            // No account assigned yet – return advisory info only.
            return new BuildingBankAccountInfo
            {
                BuildingId = buildingId,
                BuildingName = building.Name,
                CityName = cityName,
                CurrencyCode = currencyCode,
                AccountNumber = null,
                Balance = null,
                IsSuspendedForFunds = building.IsSuspendedForFunds,
                SuspendedReason = building.SuspendedReason,
                HasBankAccount = false,
            };
        }

        return new BuildingBankAccountInfo
        {
            BuildingId = buildingId,
            BuildingName = building.Name,
            CityName = cityName,
            CurrencyCode = currencyCode,
            BankAccountId = building.BankAccount.Id,
            AccountNumber = building.BankAccount.AccountNumber,
            Balance = building.BankAccount.Balance,
            IsSuspendedForFunds = building.IsSuspendedForFunds,
            SuspendedReason = building.SuspendedReason,
            HasBankAccount = true,
        };
    }

    /// <summary>
    /// Lists all bank accounts owned by the specified company.
    /// Used to present account selection when assigning an account to a building.
    /// </summary>
    [Authorize]
    public async Task<List<CompanyBankAccountSummary>> CompanyBankAccounts(
        Guid companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        // Verify the caller owns this company.
        var company = await db.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId && c.PlayerId == userId);

        if (company is null)
            return [];

        var accounts = await db.BankAccounts
            .Where(a => a.CompanyId == companyId && a.ClosedAtUtc == null)
            .AsNoTracking()
            .ToListAsync();

        return accounts.Select(a => new CompanyBankAccountSummary
        {
            Id = a.Id,
            AccountNumber = a.AccountNumber,
            CurrencyCode = a.CurrencyCode,
            Balance = a.Balance,
        }).ToList();
    }

    /// <summary>
    /// Returns all bank accounts across the authenticated player's personal and company contexts.
    /// Used to populate source/destination account selectors in the Forex Exchange swap form.
    /// </summary>
    [Authorize]
    public async Task<List<PlayerBankAccountSummary>> MyBankAccounts(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var accounts = await db.BankAccounts
            .Include(a => a.Company)
            .Include(a => a.Player)
            .Where(a => a.ClosedAtUtc == null
                && ((a.Company != null && a.Company.PlayerId == userId) || a.PlayerId == userId))
            .AsNoTracking()
            .OrderByDescending(a => a.Balance)
            .ThenBy(a => a.Company != null ? a.Company.Name : a.Player!.DisplayName)
            .ThenBy(a => a.CurrencyCode)
            .ToListAsync();

        var companyIds = accounts
            .Where(a => a.CompanyId.HasValue)
            .Select(a => a.CompanyId!.Value)
            .Distinct()
            .ToList();

        var companyPrimaryCityByCurrency = new Dictionary<(Guid CompanyId, string CurrencyCode), Guid>();

        if (companyIds.Count > 0)
        {
            var companyBuildingCities = await db.Buildings
                .AsNoTracking()
                .Where(building => companyIds.Contains(building.CompanyId))
                .Join(
                    db.Cities.AsNoTracking(),
                    building => building.CityId,
                    city => city.Id,
                    (building, city) => new
                    {
                        building.CompanyId,
                        building.CityId,
                        CurrencyCode = city.CurrencyCode,
                        building.BuiltAtUtc,
                        building.Id,
                    })
                .Where(x => x.CurrencyCode != null)
                .OrderBy(x => x.BuiltAtUtc)
                .ThenBy(x => x.Id)
                .ToListAsync();

            foreach (var item in companyBuildingCities)
            {
                var key = (item.CompanyId, item.CurrencyCode!.ToUpperInvariant());
                if (!companyPrimaryCityByCurrency.ContainsKey(key))
                {
                    companyPrimaryCityByCurrency[key] = item.CityId;
                }
            }
        }

        var governmentBanks = await db.Buildings
            .AsNoTracking()
            .Where(building => building.IsGovernmentOwned && building.Type == BuildingType.Bank)
            .Join(
                db.Cities.AsNoTracking(),
                building => building.CityId,
                city => city.Id,
                (building, city) => new
                {
                    building.Id,
                    building.CityId,
                    CurrencyCode = city.CurrencyCode,
                    city.Name,
                })
            .Where(x => x.CurrencyCode != null)
            .OrderBy(x => x.Name)
            .ToListAsync();

        var governmentBankByCity = governmentBanks
            .GroupBy(x => x.CityId)
            .ToDictionary(group => group.Key, group => group.First().Id);

        var governmentBankByCurrency = governmentBanks
            .GroupBy(x => x.CurrencyCode!.ToUpperInvariant())
            .ToDictionary(group => group.Key, group => group.First().Id);

        return accounts.Select(a => new PlayerBankAccountSummary
        {
            Id = a.Id,
            AccountNumber = a.AccountNumber,
            CurrencyCode = a.CurrencyCode,
            Balance = a.Balance,
            CompanyId = a.CompanyId,
            CompanyName = a.Company?.Name,
            OwnerType = a.CompanyId.HasValue ? "COMPANY" : "PERSON",
            OwnerDisplayName = a.Company?.Name ?? a.Player?.DisplayName ?? string.Empty,
            BankBuildingId = ResolveBankBuildingId(a),
        }).ToList();

        Guid? ResolveBankBuildingId(BankAccount account)
        {
            if (account.BankBuildingId.HasValue)
            {
                return account.BankBuildingId.Value;
            }

            if (account.CompanyId.HasValue)
            {
                var currencyCode = (account.CurrencyCode ?? "EUR").ToUpperInvariant();
                if (companyPrimaryCityByCurrency.TryGetValue((account.CompanyId.Value, currencyCode), out var cityId)
                    && governmentBankByCity.TryGetValue(cityId, out var bankIdByCity))
                {
                    return bankIdByCity;
                }

                if (governmentBankByCurrency.TryGetValue(currencyCode, out var bankIdByCurrency))
                {
                    return bankIdByCurrency;
                }
            }

            return null;
        }
    }
}
