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
using Microsoft.Extensions.Options;

namespace Api.Types;

/// <summary>
/// Stock exchange mutations: buying and selling company shares
/// through the personal or company trading account.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>Purchases shares from public investors using either the personal account or the selected company account.</summary>
    [Authorize]
    public async Task<ShareTradeResult> BuyShares(
        BuySharesInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IMasterRankingTelemetryService rankingTelemetry,
        [Service] IOptions<MasterServerRegistrationOptions> masterOptions)
    {
        if (input.ShareCount <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Share count must be greater than zero.")
                    .SetCode("INVALID_SHARE_COUNT")
                    .Build());
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Id == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

    var account = !string.IsNullOrEmpty(input.TradeAccountType)
        ? await ResolveRequestedTradingAccountAsync(db, player, input.TradeAccountType, input.TradeAccountCompanyId)
        : await ResolveActiveTradingAccountAsync(db, player, httpContextAccessor.HttpContext!.User);
        var (companies, shareholdings, sharePrices) = await LoadSharePricingSnapshotAsync(db);
        var targetCompany = companies.FirstOrDefault(company => company.Id == input.CompanyId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());

        var shareCount = decimal.Round(input.ShareCount, 4, MidpointRounding.AwayFromZero);
        var publicFloatShares = SharePriceCalculator.ComputePublicFloat(targetCompany, shareholdings.Where(holding => holding.CompanyId == targetCompany.Id));
        if (publicFloatShares < shareCount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Not enough public-float shares are available at the moment.")
                    .SetCode("INSUFFICIENT_PUBLIC_FLOAT")
                    .Build());
        }

        var sharePrice = sharePrices.GetValueOrDefault(targetCompany.Id);
        var askPrice = SharePriceCalculator.ComputeAskPrice(sharePrice);
        var totalValue = decimal.Round(askPrice * shareCount, 4, MidpointRounding.AwayFromZero);
        var currentTick = await GetCurrentTickAsync(db);
        var settlementAccount = await ResolveTradeSettlementBankAccountAsync(
            db,
            player,
            account,
            input.BankAccountId,
            "USD");
        decimal? personalCashAfterTrade = null;
        decimal? companyCashAfterTrade = null;

        if (account.Company is null)
        {
            if (settlementAccount.Balance < totalValue)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Insufficient selected personal USD bank account balance for this share purchase.")
                        .SetCode("INSUFFICIENT_PERSONAL_FUNDS")
                        .Build());
            }

            settlementAccount.Balance -= totalValue;
            personalCashAfterTrade = settlementAccount.Balance;
            db.PersonTradeRecords.Add(new PersonTradeRecord
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                CompanyId = targetCompany.Id,
                Direction = TradeDirection.Buy,
                ShareCount = shareCount,
                PricePerShare = askPrice,
                TotalValue = totalValue,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            if (settlementAccount.Balance < totalValue)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("The selected company USD bank account does not have enough cash for this share purchase.")
                        .SetCode("INSUFFICIENT_COMPANY_FUNDS")
                        .Build());
            }

            settlementAccount.Balance -= totalValue;
            companyCashAfterTrade = settlementAccount.Balance;
            AddCompanyLedgerEntry(
                db,
                account.Company,
                LedgerCategory.StockPurchase,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Bought {shareCount:0.####} shares in {targetCompany.Name} @ {askPrice:0.00}"),
                -totalValue,
                currentTick,
                settlementAccount.Id);

            if (account.Company.Id == targetCompany.Id)
            {
                targetCompany.TotalSharesIssued = Math.Max(0m, decimal.Round(targetCompany.TotalSharesIssued - shareCount, 4, MidpointRounding.AwayFromZero));
                await RecordSharePriceHistoryAsync(db, targetCompany.Id, askPrice, currentTick);
                await db.SaveChangesAsync();

                return new ShareTradeResult
                {
                    CompanyId = targetCompany.Id,
                    CompanyName = targetCompany.Name,
                    AccountType = AccountContextType.Company,
                    AccountCompanyId = account.Company.Id,
                    AccountName = account.AccountName,
                    ShareCount = shareCount,
                    PricePerShare = askPrice,
                    TotalValue = totalValue,
                    OwnedShareCount = 0m,
                    PublicFloatShares = SharePriceCalculator.ComputePublicFloat(targetCompany, shareholdings.Where(holding => holding.CompanyId == targetCompany.Id)),
                    PersonalCash = personalCashAfterTrade ?? await PersonalBankAccountService.GetGrossCashAsync(db, player),
                    PersonalTaxReserve = player.PersonalTaxReserve,
                    CompanyCash = companyCashAfterTrade,
                };
            }
        }

        var holding = GetOrCreateShareholding(
            db,
            shareholdings,
            targetCompany.Id,
            account.Company is null ? player.Id : null,
            account.Company?.Id);
        holding.ShareCount = decimal.Round(holding.ShareCount + shareCount, 4, MidpointRounding.AwayFromZero);

        await RecordSharePriceHistoryAsync(db, targetCompany.Id, askPrice, currentTick);
        await db.SaveChangesAsync();

        // Fire STOCK_TRADER telemetry (fire-and-forget).
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var serverKey = masterOptions.Value.ServerKey ?? string.Empty;
            _ = rankingTelemetry.ReportEventAsync(
                MasterRankingBountyCodes.StockTrader,
                player.Email,
                uniqueScopeKey: $"{MasterRankingBountyCodes.StockTrader}:{player.Email}:{today}:{serverKey}");
        }

        return new ShareTradeResult
        {
            CompanyId = targetCompany.Id,
            CompanyName = targetCompany.Name,
            AccountType = account.AccountType,
            AccountCompanyId = account.Company?.Id,
            AccountName = account.AccountName,
            ShareCount = shareCount,
            PricePerShare = askPrice,
            TotalValue = totalValue,
            OwnedShareCount = holding.ShareCount,
            PublicFloatShares = SharePriceCalculator.ComputePublicFloat(targetCompany, shareholdings.Where(item => item.CompanyId == targetCompany.Id)),
            PersonalCash = personalCashAfterTrade ?? await PersonalBankAccountService.GetGrossCashAsync(db, player),
            PersonalTaxReserve = player.PersonalTaxReserve,
            CompanyCash = companyCashAfterTrade,
        };
    }

    /// <summary>Sells shares back to the public exchange using either the personal account or the selected company account.</summary>
    [Authorize]
    public async Task<ShareTradeResult> SellShares(
        SellSharesInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IMasterRankingTelemetryService rankingTelemetry,
        [Service] IOptions<MasterServerRegistrationOptions> masterOptions)
    {
        if (input.ShareCount <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Share count must be greater than zero.")
                    .SetCode("INVALID_SHARE_COUNT")
                    .Build());
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Id == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

    var account = !string.IsNullOrEmpty(input.TradeAccountType)
        ? await ResolveRequestedTradingAccountAsync(db, player, input.TradeAccountType, input.TradeAccountCompanyId)
        : await ResolveActiveTradingAccountAsync(db, player, httpContextAccessor.HttpContext!.User);
        var (companies, shareholdings, sharePrices) = await LoadSharePricingSnapshotAsync(db);
        var targetCompany = companies.FirstOrDefault(company => company.Id == input.CompanyId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());

        var shareCount = decimal.Round(input.ShareCount, 4, MidpointRounding.AwayFromZero);
        var holding = shareholdings.FirstOrDefault(candidate =>
            candidate.CompanyId == targetCompany.Id
            && candidate.OwnerPlayerId == (account.Company is null ? player.Id : null)
            && candidate.OwnerCompanyId == account.Company?.Id);

        if (holding is null || holding.ShareCount < shareCount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You do not hold enough shares to complete this sale.")
                    .SetCode("INSUFFICIENT_SHARES")
                    .Build());
        }

        var sharePrice = sharePrices.GetValueOrDefault(targetCompany.Id);
        var bidPrice = SharePriceCalculator.ComputeBidPrice(sharePrice);
        var totalValue = decimal.Round(bidPrice * shareCount, 4, MidpointRounding.AwayFromZero);
        var currentTick = await GetCurrentTickAsync(db);
        var settlementAccount = await ResolveTradeSettlementBankAccountAsync(
            db,
            player,
            account,
            input.BankAccountId,
            "USD");
        decimal? personalCashAfterTrade = null;
        decimal? companyCashAfterTrade = null;

        holding.ShareCount = decimal.Round(holding.ShareCount - shareCount, 4, MidpointRounding.AwayFromZero);
        if (holding.ShareCount <= 0m)
        {
            db.Shareholdings.Remove(holding);
            shareholdings.Remove(holding);
        }

        decimal taxReserved = 0m;

        if (account.Company is null)
        {
            taxReserved = decimal.Round(totalValue * GameConstants.PersonalStockSaleTaxRate, 4, MidpointRounding.AwayFromZero);
            settlementAccount.Balance += totalValue;
            personalCashAfterTrade = settlementAccount.Balance;
            player.PersonalTaxReserve += taxReserved;
            db.PersonTradeRecords.Add(new PersonTradeRecord
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                CompanyId = targetCompany.Id,
                Direction = TradeDirection.Sell,
                ShareCount = shareCount,
                PricePerShare = bidPrice,
                TotalValue = totalValue,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            settlementAccount.Balance += totalValue;
            companyCashAfterTrade = settlementAccount.Balance;
            AddCompanyLedgerEntry(
                db,
                account.Company,
                LedgerCategory.StockSale,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Sold {shareCount:0.####} shares in {targetCompany.Name} @ {bidPrice:0.00}"),
                totalValue,
                currentTick,
                settlementAccount.Id);
        }

        await RecordSharePriceHistoryAsync(db, targetCompany.Id, bidPrice, currentTick);
        await db.SaveChangesAsync();

        // Fire STOCK_TRADER telemetry (fire-and-forget).
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var serverKey = masterOptions.Value.ServerKey ?? string.Empty;
            _ = rankingTelemetry.ReportEventAsync(
                MasterRankingBountyCodes.StockTrader,
                player.Email,
                uniqueScopeKey: $"{MasterRankingBountyCodes.StockTrader}:{player.Email}:{today}:{serverKey}");
        }

        return new ShareTradeResult
        {
            CompanyId = targetCompany.Id,
            CompanyName = targetCompany.Name,
            AccountType = account.AccountType,
            AccountCompanyId = account.Company?.Id,
            AccountName = account.AccountName,
            ShareCount = shareCount,
            PricePerShare = bidPrice,
            TotalValue = totalValue,
            TaxReserved = taxReserved,
            OwnedShareCount = holding.ShareCount > 0m ? holding.ShareCount : 0m,
            PublicFloatShares = SharePriceCalculator.ComputePublicFloat(targetCompany, shareholdings.Where(item => item.CompanyId == targetCompany.Id)),
            PersonalCash = personalCashAfterTrade ?? await PersonalBankAccountService.GetGrossCashAsync(db, player),
            PersonalTaxReserve = player.PersonalTaxReserve,
            CompanyCash = companyCashAfterTrade,
        };
    }

    private static async Task<(List<Company> Companies, List<Shareholding> Shareholdings, Dictionary<Guid, decimal> SharePrices)> LoadSharePricingSnapshotAsync(AppDbContext db)
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

        return (companies, shareholdings, sharePricesUsd);
    }

    private static async Task<BankAccount> ResolveTradeSettlementBankAccountAsync(
        AppDbContext db,
        Player player,
        ActiveTradingAccount account,
        Guid? bankAccountId,
        string requiredCurrencyCode)
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

        var settlementAccount = await db.BankAccounts
            .FirstOrDefaultAsync(candidate => candidate.Id == bankAccountId.Value)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Selected settlement bank account was not found.")
                    .SetCode("BANK_ACCOUNT_NOT_FOUND")
                    .Build());

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

        if (account.Company is null)
        {
            if (settlementAccount.PlayerId != player.Id)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Selected settlement account does not belong to the active personal account.")
                        .SetCode("BANK_ACCOUNT_NOT_OWNED")
                        .Build());
            }
        }
        else if (settlementAccount.CompanyId != account.Company.Id)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Selected settlement account does not belong to the active company account.")
                    .SetCode("BANK_ACCOUNT_NOT_OWNED")
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
        Guid? companyId)
    {
        if (string.Equals(accountType, AccountContextType.Company, StringComparison.Ordinal) && companyId.HasValue)
        {
            var company = await db.Companies.FirstOrDefaultAsync(candidate =>
                candidate.Id == companyId.Value && candidate.PlayerId == player.Id);
            if (company is not null)
            {
                return new ActiveTradingAccount(AccountContextType.Company, company, company.Name);
            }
        }

        return new ActiveTradingAccount(AccountContextType.Person, null, player.DisplayName);
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
}
