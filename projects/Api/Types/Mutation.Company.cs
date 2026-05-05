using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>Creates a new company for the authenticated player.</summary>
    [Authorize]
    public async Task<Company> CreateCompany(
        CreateCompanyInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = userId,
            Name = input.Name,
            TotalSharesIssued = DefaultCompanyShareCount,
            DividendPayoutRatio = DefaultDividendPayoutRatio,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = currentTick
        };

        db.Companies.Add(company);
        var fundingAccount = await CompanyBankingService.EnsurePreferredAccountAsync(
            db,
            company.Id,
            "EUR",
            httpContextAccessor.HttpContext!.RequestAborted);
        fundingAccount.Balance += 1_000_000m;
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BankAccountId = fundingAccount.Id,
            Category = LedgerCategory.FounderContribution,
            Description = "Initial founder company funding",
            Amount = 1_000_000m,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });
        db.Shareholdings.Add(new Shareholding
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            OwnerPlayerId = userId,
            ShareCount = company.TotalSharesIssued,
        });

        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Id == userId);
        if (player is not null)
        {
            player.ActiveAccountType = AccountContextType.Company;
            player.ActiveCompanyId = company.Id;
        }

        await db.SaveChangesAsync();

        return company;
    }

    /// <summary>Updates a company's display name and city salary settings.</summary>
    [Authorize]
    public async Task<Company> UpdateCompanySettings(
        UpdateCompanySettingsInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var company = await db.Companies
            .Include(candidate => candidate.CitySalarySettings)
            .FirstOrDefaultAsync(candidate => candidate.Id == input.CompanyId && candidate.PlayerId == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found or you don't own it.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());

        var trimmedName = input.Name.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company name cannot be empty.")
                    .SetCode("INVALID_COMPANY_NAME")
                    .Build());
        }

        var validCityIds = await db.Cities
            .Select(city => city.Id)
            .ToListAsync();
        var validCityIdSet = validCityIds.ToHashSet();

        foreach (var salarySetting in input.CitySalarySettings)
        {
            if (!validCityIdSet.Contains(salarySetting.CityId))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("City not found.")
                        .SetCode("CITY_NOT_FOUND")
                        .Build());
            }
        }

        company.Name = trimmedName;
        if (input.DividendPayoutRatio.HasValue)
        {
            company.DividendPayoutRatio = decimal.Round(
                Math.Clamp(input.DividendPayoutRatio.Value, 0m, 1m),
                4,
                MidpointRounding.AwayFromZero);
        }

        foreach (var salarySetting in input.CitySalarySettings
                     .GroupBy(setting => setting.CityId)
                     .Select(group => group.Last()))
        {
            var multiplier = CompanyEconomyCalculator.ClampSalaryMultiplier(salarySetting.SalaryMultiplier);
            var existing = company.CitySalarySettings
                .FirstOrDefault(setting => setting.CityId == salarySetting.CityId);

            if (existing is null)
            {
                var newSetting = new CompanyCitySalarySetting
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    CityId = salarySetting.CityId,
                    SalaryMultiplier = multiplier,
                };

                db.CompanyCitySalarySettings.Add(newSetting);
                company.CitySalarySettings.Add(newSetting);
            }
            else
            {
                existing.SalaryMultiplier = multiplier;
            }
        }

        await db.SaveChangesAsync();
        return company;
    }

    /// <summary>Places a new building on the game map for a company.</summary>
    [Authorize]
    public async Task<Building> PlaceBuilding(
        PlaceBuildingInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var company = await db.Companies.FirstOrDefaultAsync(
            c => c.Id == input.CompanyId && c.PlayerId == userId);

        if (company is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found or you don't own it.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());
        }

        if (!BuildingType.All.Contains(input.Type))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Invalid building type: {input.Type}")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        // Validate media type when placing a media house.
        if (input.Type == BuildingType.MediaHouse)
        {
            if (string.IsNullOrWhiteSpace(input.MediaType) || !Data.Entities.MediaType.All.Contains(input.MediaType))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("A media house requires a valid MediaType: NEWSPAPER, RADIO, or TV.")
                        .SetCode("INVALID_MEDIA_TYPE")
                        .Build());
            }
        }

        var city = await db.Cities.FindAsync(input.CityId);
        if (city is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("City not found.")
                    .SetCode("CITY_NOT_FOUND")
                    .Build());
        }

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();
        await LandService.EnsureMinimumAvailableLotsAsync(db, currentTick, [city.Id]);
        var lotId = await FindCompatibleAvailableLotIdAsync(db, city.Id, input.Type);

        var (_, building) = await PrepareLotPurchaseAsync(
            db,
            company,
            lotId,
            input.Type,
            input.Name,
            Engine.GameConstants.PowerDemandMw(input.Type, 1),
            DateTime.UtcNow,
            city.Id);

        // Apply media type for media houses.
        if (input.Type == BuildingType.MediaHouse && !string.IsNullOrWhiteSpace(input.MediaType))
            building.MediaType = input.MediaType;

        // Bank buildings require a $10,000,000 base-capital deposit.
        if (input.Type == BuildingType.Bank)
        {
            var baseCapitalFundingAccount = await CompanyBankingService.EnsurePreferredAccountAsync(
                db,
                company.Id,
                city.CurrencyCode,
                httpContextAccessor.HttpContext!.RequestAborted);

            if (baseCapitalFundingAccount.Balance < BankBaseCapitalRequirement)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"Opening a bank requires a base capital deposit of ${BankBaseCapitalRequirement:N0}. Your company bank accounts currently hold {baseCapitalFundingAccount.Balance:C0} in {city.CurrencyCode}.")
                        .SetCode("INSUFFICIENT_FUNDS")
                        .Build());
            }

            // Set default interest rates
            building.DepositInterestRatePercent = 3m;   // 3% deposit rate
            building.LendingInterestRatePercent = 8m;   // 8% lending rate

            // Create the base-capital deposit account
            var baseDeposit = new Data.Entities.BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = Guid.NewGuid().ToString("N")[..16],
                CurrencyCode = city.CurrencyCode,
                CompanyId = company.Id,
                BankBuildingId = building.Id,
                Balance = Mutation.BankBaseCapitalRequirement,
                DepositInterestRatePercent = 0m, // No interest on own base capital
                IsBaseCapitalDeposit = true,
                DepositedAtTick = currentTick,
                CreatedAtUtc = DateTime.UtcNow,
                TotalInterestPaid = 0m,
                IsGovernmentAccount = false,
            };

            db.BankAccounts.Add(baseDeposit);

            baseCapitalFundingAccount.Balance -= Mutation.BankBaseCapitalRequirement;
            building.TotalDeposits = Mutation.BankBaseCapitalRequirement;
            building.BaseCapitalDeposited = true;
        }

        await LandService.EnsureMinimumAvailableLotsAsync(db, currentTick, [city.Id]);
        await db.SaveChangesAsync();

        return building;
    }

    /// <summary>Switches the authenticated player's acting account between PERSON and one controlled COMPANY.</summary>
    [Authorize]
    public async Task<AccountContextResult> SwitchAccountContext(
        SwitchAccountContextInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Id == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        if (string.Equals(input.AccountType, AccountContextType.Person, StringComparison.OrdinalIgnoreCase))
        {
            player.ActiveAccountType = AccountContextType.Person;
            player.ActiveCompanyId = null;
            await db.SaveChangesAsync();

            return new AccountContextResult
            {
                ActiveAccountType = AccountContextType.Person,
                ActiveCompanyId = null,
                ActiveAccountName = player.DisplayName,
            };
        }

        if (!string.Equals(input.AccountType, AccountContextType.Company, StringComparison.OrdinalIgnoreCase) || input.CompanyId is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A valid company account selection is required.")
                    .SetCode("INVALID_ACCOUNT_CONTEXT")
                    .Build());
        }

        var companies = await db.Companies
            .Include(company => company.BankAccounts)
            .ToListAsync();
        var targetCompany = companies.FirstOrDefault(company => company.Id == input.CompanyId.Value)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());

        if (targetCompany.PlayerId != userId)
        {
            var shareholdings = await db.Shareholdings
                .Where(holding => holding.CompanyId == targetCompany.Id)
                .ToListAsync();
            var controlledOwnershipRatio = ComputeControlledOwnershipRatio(userId, targetCompany, companies, shareholdings);

            if (controlledOwnershipRatio < 0.5m)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("You need at least 50% combined ownership through your person account and controlled companies to switch into this company.")
                        .SetCode("COMPANY_CONTROL_REQUIRED")
                        .Build());
            }

            targetCompany.PlayerId = userId;
        }

        player.ActiveAccountType = AccountContextType.Company;
        player.ActiveCompanyId = targetCompany.Id;
        await db.SaveChangesAsync();

        return new AccountContextResult
        {
            ActiveAccountType = AccountContextType.Company,
            ActiveCompanyId = targetCompany.Id,
            ActiveAccountName = targetCompany.Name,
        };
    }
}
