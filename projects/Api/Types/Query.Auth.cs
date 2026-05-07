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

public sealed partial class Query
{
    /// <summary>Returns the currently authenticated player's profile.</summary>
    [Authorize]
    public async Task<Player?> GetMe(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IMasterRankingTelemetryService rankingTelemetry,
        [Service] IOptions<MasterServerRegistrationOptions> masterOptions)
    {
        var principal = httpContextAccessor.HttpContext!.User;
        var userId = principal.GetRequiredUserId();
        var player = await db.Players
            .AsNoTracking()
            .Include(p => p.Companies)
                .ThenInclude(c => c.Buildings)
            .FirstOrDefaultAsync(p => p.Id == userId);

        // Fire daily login telemetry — deduplicated per UTC day per server.
        if (player is not null)
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var serverKey = masterOptions.Value.ServerKey ?? string.Empty;
            _ = rankingTelemetry.ReportEventAsync(
                MasterRankingBountyCodes.LoginToGame,
                player.Email,
                uniqueScopeKey: $"{MasterRankingBountyCodes.LoginToGame}:{player.Email}:{today}:{serverKey}");
        }

        return ApplyImpersonationAccountContext(player, principal);
    }

    /// <summary>Returns the authenticated player's personal account, portfolio, and dividend history.</summary>
    [Authorize]
    public async Task<PersonAccountResult?> GetPersonAccount(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var principal = httpContextAccessor.HttpContext!.User;
        var userId = principal.GetRequiredUserId();
        var player = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId);

        if (player is null)
        {
            return null;
        }

        var companies = await db.Companies
            .AsNoTracking()
            .Include(company => company.BankAccounts)
            .OrderBy(company => company.Name)
            .ToListAsync();
        var buildings = await db.Buildings
            .AsNoTracking()
            .Include(building => building.City)
            .ToListAsync();
        var lots = await db.BuildingLots
            .AsNoTracking()
            .Where(lot => lot.OwnerCompanyId.HasValue)
            .ToListAsync();
        var inventories = await db.Inventories
            .AsNoTracking()
            .Include(inventory => inventory.ResourceType)
            .Include(inventory => inventory.ProductType)
            .ToListAsync();
        var shareholdings = await db.Shareholdings.AsNoTracking().ToListAsync();
        var governmentCompanyIds = await GovernmentCompanyQueries.GetGovernmentCompanyIdsAsync(db);
        var localSharePriceByCompany = BuildQuotedSharePriceLookup(companies, buildings, lots, inventories, shareholdings);
        var companyCurrencyCodeById = companies.ToDictionary(
            company => company.Id,
            company => ResolvePrimaryCurrencyCode(company.Id, buildings));
        var usdRate = await GetEurToUsdRateAsync(db);
        var eurRatesByCode = await BuildEurRatesLookupAsync(db, companyCurrencyCodeById.Values);
        var sharePriceByCompany = companies.ToDictionary(
            company => company.Id,
            company => decimal.Round(
                ConvertToUsd(
                    localSharePriceByCompany.GetValueOrDefault(company.Id),
                    companyCurrencyCodeById.GetValueOrDefault(company.Id, "EUR"),
                    eurRatesByCode,
                    usdRate),
                4,
                MidpointRounding.AwayFromZero));
        var companiesById = companies.ToDictionary(company => company.Id);

        var personalAccounts = await db.BankAccounts
            .AsNoTracking()
            .Where(account => account.PlayerId == userId && account.ClosedAtUtc == null)
            .ToListAsync();
        var personalCurrencies = personalAccounts.Select(account => account.CurrencyCode).Distinct().ToList();
        var personalEurRatesByCode = await BuildEurRatesLookupAsync(db, personalCurrencies);

        var portfolio = shareholdings
            .Where(holding =>
                holding.OwnerPlayerId == userId
                && holding.ShareCount > 0m
                && !governmentCompanyIds.Contains(holding.CompanyId))
            .Select(holding =>
            {
                var company = companiesById[holding.CompanyId];
                var sharePrice = sharePriceByCompany.GetValueOrDefault(holding.CompanyId);
                return new PortfolioHoldingResult
                {
                    CompanyId = company.Id,
                    CompanyName = company.Name,
                    ShareCount = holding.ShareCount,
                    OwnershipRatio = company.TotalSharesIssued > 0m
                        ? decimal.Round(holding.ShareCount / company.TotalSharesIssued, 4, MidpointRounding.AwayFromZero)
                        : 0m,
                    SharePrice = sharePrice,
                    MarketValue = decimal.Round(holding.ShareCount * sharePrice, 4, MidpointRounding.AwayFromZero),
                };
            })
            .OrderByDescending(holding => holding.MarketValue)
            .ThenBy(holding => holding.CompanyName)
            .ToList();

        var dividendPayments = await db.DividendPayments
            .AsNoTracking()
            .Where(payment => payment.RecipientPlayerId == userId)
            .OrderByDescending(payment => payment.RecordedAtTick)
            .ToListAsync();

        var stockTrades = await db.PersonTradeRecords
            .AsNoTracking()
            .Where(trade => trade.PlayerId == userId)
            .OrderByDescending(trade => trade.RecordedAtTick)
            .ThenByDescending(trade => trade.RecordedAtUtc)
            .Take(100)
            .ToListAsync();

        var grossPersonalCashUsd = decimal.Round(
            personalAccounts.Sum(account => ConvertToUsd(account.Balance, account.CurrencyCode, personalEurRatesByCode, usdRate)),
            4,
            MidpointRounding.AwayFromZero);

        var result = new PersonAccountResult
        {
            PlayerId = player.Id,
            DisplayName = player.DisplayName,
            PersonalCash = grossPersonalCashUsd,
            TaxReserve = player.PersonalTaxReserve,
            AvailableCash = grossPersonalCashUsd - player.PersonalTaxReserve,
            TotalNetWealth = (grossPersonalCashUsd - player.PersonalTaxReserve)
                + portfolio.Sum(holding => holding.MarketValue),
            ActiveAccountType = player.ActiveAccountType,
            ActiveCompanyId = player.ActiveCompanyId,
            Shareholdings = portfolio,
            DividendPayments = dividendPayments
                .Select(payment => new DividendPaymentResult
                {
                    Id = payment.Id,
                    CompanyId = payment.CompanyId,
                    CompanyName = companiesById.GetValueOrDefault(payment.CompanyId)?.Name ?? string.Empty,
                    ShareCount = payment.ShareCount,
                    AmountPerShare = payment.AmountPerShare,
                    TotalAmount = payment.TotalAmount,
                    GameYear = payment.GameYear,
                    RecordedAtTick = payment.RecordedAtTick,
                    RecordedAtUtc = payment.RecordedAtUtc,
                    Description = payment.Description,
                })
                .ToList(),
            StockTrades = stockTrades
                .Select(trade => new PersonTradeRecordResult
                {
                    Id = trade.Id,
                    CompanyId = trade.CompanyId,
                    CompanyName = companiesById.GetValueOrDefault(trade.CompanyId)?.Name ?? string.Empty,
                    Direction = trade.Direction,
                    ShareCount = trade.ShareCount,
                    PricePerShare = trade.PricePerShare,
                    TotalValue = trade.TotalValue,
                    RecordedAtTick = trade.RecordedAtTick,
                    RecordedAtUtc = trade.RecordedAtUtc,
                })
                .ToList(),
        };

        ApplyImpersonationAccountContext(result, principal);
        return result;
    }

    private static Player? ApplyImpersonationAccountContext(Player? player, ClaimsPrincipal principal)
    {
        if (player is null)
        {
            return null;
        }

        var effectiveAccountType = principal.GetEffectiveAccountType();
        if (string.IsNullOrWhiteSpace(effectiveAccountType))
        {
            return player;
        }

        player.ActiveAccountType = effectiveAccountType;
        player.ActiveCompanyId = principal.GetEffectiveCompanyId();
        return player;
    }

    private static void ApplyImpersonationAccountContext(PersonAccountResult result, ClaimsPrincipal principal)
    {
        var effectiveAccountType = principal.GetEffectiveAccountType();
        if (string.IsNullOrWhiteSpace(effectiveAccountType))
        {
            return;
        }

        result.ActiveAccountType = effectiveAccountType;
        result.ActiveCompanyId = principal.GetEffectiveCompanyId();
    }
}
