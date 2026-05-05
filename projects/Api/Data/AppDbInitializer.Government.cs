using Api.Data.Entities;
using Api.Types;
using Api.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    /// <summary>
    /// Seeds one government-owned media house of each type (NEWSPAPER, RADIO, TV) in every city.
    /// Idempotent: ensures the government actor exists, then inserts only missing outlets.
    /// Government outlets provide a baseline media market from day one so players always have
    /// something to route their marketing budgets through.
    /// </summary>
    private async Task SeedGovernmentMediaHousesAsync()
    {
        var (_, govCompany) = await EnsureGovernmentActorAsync();
        var govCompanyId = govCompany.Id;

        var cities = await dbContext.Cities.ToListAsync();

        // Baseline initial content for government outlets.
        // Higher than 0 so they display at a non-zero ranking until players invest more.
        const decimal InitialContentValue = 1_000m;

        foreach (var city in cities)
        {
            // NEWSPAPER
            var newspaperId = CreateDeterministicGuid($"gov-media:{city.Id}:newspaper");
            if (!await dbContext.Buildings.AnyAsync(b => b.Id == newspaperId))
            {
                dbContext.Buildings.Add(new Building
                {
                    Id = newspaperId,
                    CompanyId = govCompanyId,
                    CityId = city.Id,
                    Type = BuildingType.MediaHouse,
                    Name = $"{city.Name} Gazette",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    MediaType = Entities.MediaType.Newspaper,
                    ContentValue = InitialContentValue,
                    IsGovernmentOwned = true,
                    PowerStatus = Entities.PowerStatus.Powered,
                    BuiltAtUtc = DateTime.UtcNow
                });
            }

            // RADIO
            var radioId = CreateDeterministicGuid($"gov-media:{city.Id}:radio");
            if (!await dbContext.Buildings.AnyAsync(b => b.Id == radioId))
            {
                dbContext.Buildings.Add(new Building
                {
                    Id = radioId,
                    CompanyId = govCompanyId,
                    CityId = city.Id,
                    Type = BuildingType.MediaHouse,
                    Name = $"{city.Name} Radio",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    MediaType = Entities.MediaType.Radio,
                    ContentValue = InitialContentValue,
                    IsGovernmentOwned = true,
                    PowerStatus = Entities.PowerStatus.Powered,
                    BuiltAtUtc = DateTime.UtcNow
                });
            }

            // TV
            var tvId = CreateDeterministicGuid($"gov-media:{city.Id}:tv");
            if (!await dbContext.Buildings.AnyAsync(b => b.Id == tvId))
            {
                dbContext.Buildings.Add(new Building
                {
                    Id = tvId,
                    CompanyId = govCompanyId,
                    CityId = city.Id,
                    Type = BuildingType.MediaHouse,
                    Name = $"{city.Name} TV",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    MediaType = Entities.MediaType.Tv,
                    ContentValue = InitialContentValue,
                    IsGovernmentOwned = true,
                    PowerStatus = Entities.PowerStatus.Powered,
                    BuiltAtUtc = DateTime.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds one government-owned bank building in every city with baseline public rates.
    /// These banks are immediately visible on the public banking page after restart.
    /// </summary>
    private async Task EnsureGovernmentBankBuildingsAsync()
    {
        var (_, govCompany) = await EnsureGovernmentActorAsync();
        var currentTick = await dbContext.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();
        var cities = await dbContext.Cities.AsNoTracking().ToListAsync();

        foreach (var city in cities)
        {
            var bankId = CreateDeterministicGuid($"gov-bank-building:{city.Id}");
            var baseCapitalRequirement = Mutation.GetBaseCapitalRequirement(city.CurrencyCode ?? "EUR");

            if (!await dbContext.Buildings.AnyAsync(b => b.Id == bankId))
            {
                dbContext.Buildings.Add(new Building
                {
                    Id = bankId,
                    CompanyId = govCompany.Id,
                    CityId = city.Id,
                    Type = BuildingType.Bank,
                    Name = $"{city.Name} Government Bank",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    DepositInterestRatePercent = 0m,
                    LendingInterestRatePercent = 20m,
                    TotalDeposits = baseCapitalRequirement,
                    BaseCapitalDeposited = true,
                    IsGovernmentOwned = true,
                    PowerStatus = Entities.PowerStatus.Powered,
                    BuiltAtUtc = DateTime.UtcNow,
                });
            }

            var baseDepositId = CreateDeterministicGuid($"gov-bank-base-deposit:{city.Id}");
            if (!await dbContext.BankAccounts.AnyAsync(a => a.Id == baseDepositId))
            {
                dbContext.BankAccounts.Add(new BankAccount
                {
                    Id = baseDepositId,
                    AccountNumber = GenerateDeterministicAccountNumber($"gov-bank-base-deposit:{city.Id}"),
                    CurrencyCode = city.CurrencyCode ?? "EUR",
                    CompanyId = govCompany.Id,
                    BankBuildingId = bankId,
                    Balance = baseCapitalRequirement,
                    DepositInterestRatePercent = 0m,
                    IsBaseCapitalDeposit = true,
                    DepositedAtTick = currentTick,
                    CreatedAtUtc = DateTime.UtcNow,
                    TotalInterestPaid = 0m,
                    IsGovernmentAccount = false,
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<(Player Player, Company Company)> EnsureGovernmentActorAsync()
    {
        const string GovEmail = "government@capitalism.game";
        const string GovDisplayName = "Government";

        var govPlayer = await dbContext.Players
            .FirstOrDefaultAsync(player => player.Email == GovEmail);

        if (govPlayer is null)
        {
            var hasher = new PasswordHasher<Player>();
            govPlayer = new Player
            {
                Id = CreateDeterministicGuid("player:government"),
                Email = GovEmail,
                DisplayName = GovDisplayName,
                Role = PlayerRole.Player,
                ActiveAccountType = AccountContextType.Person,
                CreatedAtUtc = DateTime.UtcNow,
            };
            govPlayer.PasswordHash = hasher.HashPassword(govPlayer, Guid.NewGuid().ToString());
            dbContext.Players.Add(govPlayer);
            await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(dbContext, govPlayer, 0m);
        }

        Company? govCompany;
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var companiesByName = await dbContext.Companies
                .Where(company => company.Name == GovDisplayName)
                .ToListAsync();
            govCompany = companiesByName.FirstOrDefault(company => company.PlayerId == govPlayer.Id);
        }
        else
        {
            govCompany = await dbContext.Companies
                .FirstOrDefaultAsync(company => company.PlayerId == govPlayer.Id && company.Name == GovDisplayName);
        }

        if (govCompany is null)
        {
            govCompany = new Company
            {
                Id = CreateDeterministicGuid("company:government"),
                PlayerId = govPlayer.Id,
                Name = GovDisplayName,
                FoundedAtUtc = DateTime.UtcNow,
            };
            dbContext.Companies.Add(govCompany);
        }

        return (govPlayer, govCompany);
    }

    /// <summary>
    /// Ensures exactly one government-owned bank account exists for each unique city currency.
    /// Called at startup to guarantee every city has a default bank for auto-assigning buildings.
    /// Idempotent: creates only accounts that do not yet exist.
    /// </summary>
    private async Task EnsureGovernmentBankAccountsAsync()
    {
        var currencies = await dbContext.Cities
            .Select(c => c.CurrencyCode)
            .Distinct()
            .ToListAsync();

        foreach (var currencyCode in currencies)
        {
            var exists = await dbContext.BankAccounts
                .AnyAsync(a => a.CurrencyCode == currencyCode && a.IsGovernmentAccount);

            if (!exists)
            {
                var govAccountId = CreateDeterministicGuid($"gov-bank:{currencyCode}");
                dbContext.BankAccounts.Add(new BankAccount
                {
                    Id = govAccountId,
                    AccountNumber = GenerateDeterministicAccountNumber($"gov-bank:{currencyCode}"),
                    CurrencyCode = currencyCode,
                    Balance = 0m,
                    CompanyId = null,
                    IsGovernmentAccount = true,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task EnsurePlayerSettlementAccountsAsync()
    {
        var players = await dbContext.Players.ToListAsync();

        foreach (var player in players)
        {
            await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(dbContext, player);
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task EnsureBuildingBankAccountsAsync()
    {
        var buildingsMissingAccounts = await dbContext.Buildings
            .Include(building => building.City)
            .Where(building => building.BankAccountId == null)
            .ToListAsync();

        if (buildingsMissingAccounts.Count == 0)
        {
            return;
        }

        foreach (var building in buildingsMissingAccounts)
        {
            await BuildingBankAccountProvisioning.EnsureBuildingAssignedAccountAsync(
                dbContext,
                building,
                building.City?.CurrencyCode);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Generates a deterministic 16-digit account number from a seed string.
    /// The result is always exactly 16 decimal digits, unique per seed within a server.
    /// </summary>
    private static string GenerateDeterministicAccountNumber(string seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var value = BitConverter.ToUInt64(bytes, 0);
        return (value % 10_000_000_000_000_000UL).ToString("D16");
    }
}
