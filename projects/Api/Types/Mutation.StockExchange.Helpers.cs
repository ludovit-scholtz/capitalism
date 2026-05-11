using System.Globalization;
using System.Security.Claims;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Types;

/// <summary>
/// Stock exchange mutations: buying and selling company shares
/// through the personal or company trading account.
/// </summary>
public sealed partial class Mutation
{
    private static async Task<(List<Company> Companies, List<Shareholding> Shareholdings, Dictionary<Guid, decimal> SharePrices, HashSet<Guid> GovernmentCompanyIds)> LoadSharePricingSnapshotAsync(AppDbContext db)
    {
        var companies = await db.Companies
            .Include(company => company.BankAccounts)
            .ToListAsync();
        var buildings = await db.Buildings
            .Include(building => building.City)
            .ToListAsync();
        var lots = await db.BuildingLots.Where(lot => lot.OwnerCompanyId.HasValue).ToListAsync();
        var inventories = await db.Inventories
            .Include(inventory => inventory.ResourceType)
            .Include(inventory => inventory.ProductType)
            .ToListAsync();
        var shareholdings = await db.Shareholdings.ToListAsync();

        var baseEquityByCompany = SharePriceCalculator.ComputeBaseEquityByCompany(companies, buildings, lots, inventories);
        var localSharePrices = SharePriceCalculator.ComputeQuotedSharePriceByCompany(companies, baseEquityByCompany, shareholdings);
        var companyCurrencyCodeById = companies.ToDictionary(
            company => company.Id,
            company => buildings
                .Where(building => building.CompanyId == company.Id)
                .Select(building => building.City?.CurrencyCode)
                .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code))
                ?? "EUR");

        var usdRate = await GetEurToUsdRateAsync(db);
        var eurRatesByCode = await BuildEurRatesLookupAsync(db, companyCurrencyCodeById.Values);
        var sharePricesUsd = companies.ToDictionary(
            company => company.Id,
            company => decimal.Round(
                ConvertToUsd(
                    localSharePrices.GetValueOrDefault(company.Id),
                    companyCurrencyCodeById.GetValueOrDefault(company.Id, "EUR"),
                    eurRatesByCode,
                    usdRate),
                4,
                MidpointRounding.AwayFromZero));

        var governmentCompanyIds = await GovernmentCompanyQueries.GetGovernmentCompanyIdsAsync(db);

        return (companies, shareholdings, sharePricesUsd, governmentCompanyIds);
    }

    private static bool IsGovernmentCompany(HashSet<Guid> governmentCompanyIds, Company? company)
        => company is not null && governmentCompanyIds.Contains(company.Id);

    private static GraphQLException CreateGovernmentSharesNotTradeableException()
        => new(
            ErrorBuilder.New()
                .SetMessage("Government company shares cannot be traded on the stock exchange.")
                .SetCode("GOVERNMENT_SHARES_NOT_TRADEABLE")
                .Build());

    private static async Task<BankAccount> ResolveTradeSettlementBankAccountAsync(
        AppDbContext db,
        Player player,
        ActiveTradingAccount account,
        Guid? bankAccountId,
        string requiredCurrencyCode,
        ObjectAuthorizationService objectAuthorization,
        CancellationToken cancellationToken)
    {
        if (!bankAccountId.HasValue)
        {
            // Auto-resolve: personal accounts use the player EUR settlement account;
            // company accounts use the company EUR settlement account.
            if (account.Company is null)
            {
                return await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(db, player);
            }
            var companyEurAccount = await db.BankAccounts
                .FirstOrDefaultAsync(a => a.CompanyId == account.Company.Id && a.CurrencyCode == "EUR" && a.ClosedAtUtc == null);
            if (companyEurAccount is null)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("The active company does not have a EUR settlement account for stock trades.")
                        .SetCode("BANK_ACCOUNT_REQUIRED")
                        .Build());
            }
            return companyEurAccount;
        }

        var settlementAccount = await objectAuthorization.RequireOwnedAsync(
            actorUserId: player.Id,
            requestedObjectType: "bank_account",
            requestedObjectId: bankAccountId.Value,
            loadEntityAsync: token => db.BankAccounts
                .FirstOrDefaultAsync(candidate => candidate.Id == bankAccountId.Value, token),
            isOwnedByActor: candidate => account.Company is null
                ? candidate.PlayerId == player.Id
                : candidate.CompanyId == account.Company.Id,
            cancellationToken: cancellationToken);

        if (settlementAccount.ClosedAtUtc.HasValue)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Selected settlement bank account is closed.")
                    .SetCode("BANK_ACCOUNT_CLOSED")
                    .Build());
        }

        if (!string.Equals(settlementAccount.CurrencyCode, requiredCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Stock trading settlement supports only {requiredCurrencyCode} accounts.")
                    .SetCode("INVALID_SETTLEMENT_CURRENCY")
                    .Build());
        }

        return settlementAccount;
    }

    private static async Task<decimal> GetEurToUsdRateAsync(AppDbContext db)
    {
        var rate = await db.FxRates
            .AsNoTracking()
            .Where(r => r.BaseCurrencyCode == "EUR" && r.QuoteCurrencyCode == "USD")
            .OrderByDescending(r => r.RateDate)
            .Select(r => r.Rate)
            .FirstOrDefaultDeterministicAsync();
        return rate > 0 ? rate : 1.08m;
    }

    private static async Task<Dictionary<string, decimal>> BuildEurRatesLookupAsync(
        AppDbContext db,
        IEnumerable<string> currencyCodes)
    {
        var codes = currencyCodes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(code => !string.Equals(code, "EUR", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var lookup = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["EUR"] = 1m,
        };

        if (codes.Count > 0)
        {
            var dbRates = await db.FxRates
                .AsNoTracking()
                .Where(r => r.BaseCurrencyCode == "EUR" && codes.Contains(r.QuoteCurrencyCode))
                .GroupBy(r => r.QuoteCurrencyCode)
                .Select(group => new
                {
                    CurrencyCode = group.Key,
                    Rate = group.OrderByDescending(r => r.RateDate).Select(r => r.Rate).First(),
                })
                .ToListAsync();

            foreach (var row in dbRates)
            {
                lookup[row.CurrencyCode] = row.Rate;
            }
        }

        var fallbacks = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["CZK"] = 25.20m,
            ["USD"] = 1.08m,
            ["GBP"] = 0.86m,
            ["CNY"] = 7.84m,
            ["INR"] = 90.50m,
        };

        foreach (var code in codes.Where(code => !lookup.ContainsKey(code)))
        {
            lookup[code] = fallbacks.TryGetValue(code, out var fallbackRate) ? fallbackRate : 1m;
        }

        return lookup;
    }

    private static decimal ConvertToUsd(
        decimal amount,
        string currencyCode,
        Dictionary<string, decimal> eurRatesByCode,
        decimal usdRate)
    {
        if (amount == 0m)
        {
            return 0m;
        }

        if (string.Equals(currencyCode, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        var eurUnitsPerLocal = eurRatesByCode.TryGetValue(currencyCode, out var rate) && rate > 0m ? rate : 1m;
        var amountInEur = amount / eurUnitsPerLocal;
        return amountInEur * usdRate;
    }

    private static async Task RecordSharePriceHistoryAsync(AppDbContext db, Guid companyId, decimal sharePrice, long currentTick)
    {
        var latestEntryForTick = await db.SharePriceHistoryEntries
            .Where(entry => entry.CompanyId == companyId && entry.RecordedAtTick == currentTick)
            .OrderByDescending(entry => entry.RecordedAtUtc)
            .FirstOrDefaultDeterministicAsync();

        if (latestEntryForTick is null)
        {
            db.SharePriceHistoryEntries.Add(new SharePriceHistoryEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                SharePrice = sharePrice,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
            return;
        }

        latestEntryForTick.SharePrice = sharePrice;
        latestEntryForTick.RecordedAtUtc = DateTime.UtcNow;
    }

    private static async Task<ActiveTradingAccount> ResolveRequestedTradingAccountAsync(
        AppDbContext db,
        Player player,
        string accountType,
        Guid? companyId,
        string mutationName,
        ILogger<Mutation> logger)
    {
        if (string.Equals(accountType, AccountContextType.Company, StringComparison.Ordinal))
        {
            if (!companyId.HasValue)
            {
                throw CreateInvalidClientOverrideException(
                    logger,
                    mutationName,
                    player,
                    accountType,
                    companyId,
                    "tradeAccountCompanyId is required when tradeAccountType is COMPANY.");
            }

            var company = await db.Companies.FirstOrDefaultAsync(candidate =>
                candidate.Id == companyId.Value && candidate.PlayerId == player.Id);
            if (company is null)
            {
                throw CreateInvalidClientOverrideException(
                    logger,
                    mutationName,
                    player,
                    accountType,
                    companyId,
                    "tradeAccountCompanyId does not belong to the authenticated player.");
            }

            return new ActiveTradingAccount(AccountContextType.Company, company, company.Name);
        }

        if (string.Equals(accountType, AccountContextType.Person, StringComparison.Ordinal))
        {
            if (companyId.HasValue)
            {
                throw CreateInvalidClientOverrideException(
                    logger,
                    mutationName,
                    player,
                    accountType,
                    companyId,
                    "tradeAccountCompanyId must be null when tradeAccountType is PERSON.");
            }

            return new ActiveTradingAccount(AccountContextType.Person, null, player.DisplayName);
        }

        throw CreateInvalidClientOverrideException(
            logger,
            mutationName,
            player,
            accountType,
            companyId,
            "tradeAccountType must be PERSON or COMPANY.");
    }

    private static async Task<ActiveTradingAccount> ResolveActiveTradingAccountAsync(AppDbContext db, Player player, ClaimsPrincipal principal)
    {
        var effectiveAccountType = principal.GetEffectiveAccountType() ?? player.ActiveAccountType;
        var effectiveCompanyId = principal.GetEffectiveCompanyId() ?? player.ActiveCompanyId;

        if (string.Equals(effectiveAccountType, AccountContextType.Company, StringComparison.Ordinal)
            && effectiveCompanyId.HasValue)
        {
            var company = await db.Companies.FirstOrDefaultAsync(candidate =>
                candidate.Id == effectiveCompanyId.Value && candidate.PlayerId == player.Id);
            if (company is not null)
            {
                return new ActiveTradingAccount(AccountContextType.Company, company, company.Name);
            }
        }

        if (!principal.IsImpersonating())
        {
            player.ActiveAccountType = AccountContextType.Person;
            player.ActiveCompanyId = null;
        }

        return new ActiveTradingAccount(AccountContextType.Person, null, player.DisplayName);
    }

    private static Shareholding GetOrCreateShareholding(
        AppDbContext db,
        List<Shareholding> shareholdings,
        Guid companyId,
        Guid? ownerPlayerId,
        Guid? ownerCompanyId)
    {
        var existing = shareholdings.FirstOrDefault(holding =>
            holding.CompanyId == companyId
            && holding.OwnerPlayerId == ownerPlayerId
            && holding.OwnerCompanyId == ownerCompanyId);

        if (existing is not null)
        {
            return existing;
        }

        var holding = new Shareholding
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OwnerPlayerId = ownerPlayerId,
            OwnerCompanyId = ownerCompanyId,
            ShareCount = 0m,
        };
        db.Shareholdings.Add(holding);
        shareholdings.Add(holding);
        return holding;
    }

    private static decimal ComputeControlledOwnershipRatio(
        Guid playerId,
        Company targetCompany,
        IEnumerable<Company> companies,
        IEnumerable<Shareholding> shareholdings)
    {
        if (targetCompany.TotalSharesIssued <= 0m)
        {
            return 0m;
        }

        var controlledCompanyIds = companies
            .Where(company => company.PlayerId == playerId)
            .Select(company => company.Id)
            .ToHashSet();

        var controlledShares = shareholdings
            .Where(holding => holding.CompanyId == targetCompany.Id
                && (holding.OwnerPlayerId == playerId
                    || (holding.OwnerCompanyId.HasValue && controlledCompanyIds.Contains(holding.OwnerCompanyId.Value))))
            .Sum(holding => holding.ShareCount);

        return decimal.Round(controlledShares / targetCompany.TotalSharesIssued, 4, MidpointRounding.AwayFromZero);
    }

    private sealed record ActiveTradingAccount(string AccountType, Company? Company, string AccountName);

    private static GraphQLException CreateInvalidClientOverrideException(
        ILogger<Mutation> logger,
        string mutationName,
        Player player,
        string? requestedAccountType,
        Guid? requestedCompanyId,
        string reason)
    {
        logger.LogWarning(
            "Rejected stock trade client override. Mutation: {MutationName}, PlayerId: {PlayerId}, RequestedAccountType: {RequestedAccountType}, RequestedCompanyId: {RequestedCompanyId}, Reason: {Reason}, OccurredAtUtc: {OccurredAtUtc}",
            mutationName,
            player.Id,
            requestedAccountType,
            requestedCompanyId,
            reason,
            DateTime.UtcNow);

        return new GraphQLException(
            ErrorBuilder.New()
                .SetMessage("This action could not be completed. Please refresh and try again.")
                .SetCode("INVALID_CLIENT_OVERRIDE")
                .Build());
    }
}
