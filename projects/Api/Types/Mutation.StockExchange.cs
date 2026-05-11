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
    /// <summary>
    /// Purchases shares from public investors using either the personal account or the selected company account.
    /// API-key callers can trade only through company/account identifiers owned by the authenticated principal.
    /// </summary>
    [Authorize]
    public async Task<ShareTradeResult> BuyShares(
        BuySharesInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization,
        [Service] ILogger<Mutation> logger,
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

        var normalizedTradeAccountType = string.IsNullOrWhiteSpace(input.TradeAccountType)
            ? null
            : input.TradeAccountType.Trim().ToUpperInvariant();
        var account = !string.IsNullOrEmpty(normalizedTradeAccountType) || input.TradeAccountCompanyId.HasValue
            ? await ResolveRequestedTradingAccountAsync(db, player, normalizedTradeAccountType ?? string.Empty, input.TradeAccountCompanyId, "buyShares", logger)
            : await ResolveActiveTradingAccountAsync(db, player, httpContextAccessor.HttpContext!.User);
        var (companies, shareholdings, sharePrices, governmentCompanyIds) = await LoadSharePricingSnapshotAsync(db);
        if (IsGovernmentCompany(governmentCompanyIds, account.Company))
        {
            throw CreateGovernmentSharesNotTradeableException();
        }
        var targetCompany = companies.FirstOrDefault(company => company.Id == input.CompanyId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());
        if (IsGovernmentCompany(governmentCompanyIds, targetCompany))
        {
            throw CreateGovernmentSharesNotTradeableException();
        }

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
            "USD",
            objectAuthorization,
            httpContextAccessor.HttpContext!.RequestAborted);
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

    /// <summary>
    /// Sells shares back to the public exchange using either the personal account or the selected company account.
    /// API-key callers can trade only through company/account identifiers owned by the authenticated principal.
    /// </summary>
    [Authorize]
    public async Task<ShareTradeResult> SellShares(
        SellSharesInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization,
        [Service] ILogger<Mutation> logger,
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

        var normalizedTradeAccountType = string.IsNullOrWhiteSpace(input.TradeAccountType)
            ? null
            : input.TradeAccountType.Trim().ToUpperInvariant();
        var account = !string.IsNullOrEmpty(normalizedTradeAccountType) || input.TradeAccountCompanyId.HasValue
            ? await ResolveRequestedTradingAccountAsync(db, player, normalizedTradeAccountType ?? string.Empty, input.TradeAccountCompanyId, "sellShares", logger)
            : await ResolveActiveTradingAccountAsync(db, player, httpContextAccessor.HttpContext!.User);
        var (companies, shareholdings, sharePrices, governmentCompanyIds) = await LoadSharePricingSnapshotAsync(db);
        if (IsGovernmentCompany(governmentCompanyIds, account.Company))
        {
            throw CreateGovernmentSharesNotTradeableException();
        }
        var targetCompany = companies.FirstOrDefault(company => company.Id == input.CompanyId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());
        if (IsGovernmentCompany(governmentCompanyIds, targetCompany))
        {
            throw CreateGovernmentSharesNotTradeableException();
        }

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
            "USD",
            objectAuthorization,
            httpContextAccessor.HttpContext!.RequestAborted);
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
}
