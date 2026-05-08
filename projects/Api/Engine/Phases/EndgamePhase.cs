using System.Globalization;
using System.Net;
using Api.Data;
using Api.Data.Entities;
using Api.Types;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Engine.Phases;

/// <summary>
/// Declares the game winner once a player exceeds the configured billionaire benchmark.
/// Also publishes the final newsletter entry with top-10 ranking and summary stats.
/// </summary>
public sealed class EndgamePhase(
    IMasterGameAdministrationService masterGameAdministrationService,
    ILogger<EndgamePhase> logger) : ITickPhase
{
    public string Name => "Endgame";
    public int Order => 1200;

    public async Task ProcessAsync(TickContext context)
    {
        if (context.GameState.GameEnded)
        {
            return;
        }

        var db = context.Db;
        var players = await db.Players
            .AsNoTracking()
            .Where(player => player.Role != PlayerRole.Admin
                && player.Email != GovernmentActorConstants.GovernmentEmail)
            .ToListAsync();
        if (players.Count == 0)
        {
            return;
        }

        var shareholdings = await db.Shareholdings
            .AsNoTracking()
            .Where(holding => holding.ShareCount > 0m)
            .ToListAsync();

        var companies = context.CompaniesById.Values.ToList();
        var buildings = context.BuildingsById.Values.ToList();
        var lots = context.LotsByCompany.Values.SelectMany(list => list).DistinctBy(lot => lot.Id).ToList();
        var inventories = context.InventoryByBuilding.Values.SelectMany(list => list).DistinctBy(item => item.Id).ToList();

        var companyCurrencyById = companies.ToDictionary(
            company => company.Id,
            company => ResolvePrimaryCurrencyCode(company.Id, buildings));

        var personalAccounts = context.BankAccountsById.Values
            .Where(account => account.PlayerId.HasValue
                && account.ClosedAtUtc == null)
            .ToList();

        var goldBalances = await db.PlayerGoldBalances
            .AsNoTracking()
            .ToListAsync();
        var pools = await db.GoldAmmPools
            .AsNoTracking()
            .ToListAsync();
        var positions = await db.GoldAmmPositions
            .AsNoTracking()
            .ToListAsync();

        var currencies = personalAccounts
            .Select(account => account.CurrencyCode)
            .Concat(companyCurrencyById.Values)
            .Concat(pools.Select(pool => pool.CurrencyCode))
            .Append("USD")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var eurRates = await FxRateHelper.BuildEurRatesLookupAsync(db, currencies);

        var baseEquityByCompany = SharePriceCalculator.ComputeBaseEquityByCompany(companies, buildings, lots, inventories);
        var quotedSharePriceLocalByCompany = SharePriceCalculator.ComputeQuotedSharePriceByCompany(companies, baseEquityByCompany, shareholdings);
        var quotedSharePriceUsdByCompany = quotedSharePriceLocalByCompany.ToDictionary(
            pair => pair.Key,
            pair => FxRateHelper.ConvertToUsd(
                pair.Value,
                companyCurrencyById.GetValueOrDefault(pair.Key, "EUR"),
                eurRates));

        var poolGoldPriceUsdByPoolId = pools.ToDictionary(
            pool => pool.Id,
            pool =>
            {
                if (pool.GoldReserve <= 0m || pool.FiatReserve <= 0m)
                {
                    return 0m;
                }

                var localPrice = pool.FiatReserve / pool.GoldReserve;
                return FxRateHelper.ConvertToUsd(localPrice, pool.CurrencyCode, eurRates);
            });
        var fallbackGoldPriceUsd = poolGoldPriceUsdByPoolId.Values.Where(value => value > 0m).DefaultIfEmpty(3_000m).Average();

        var top10 = players
            .Select(player =>
            {
                var cashUsd = personalAccounts
                    .Where(account => account.PlayerId == player.Id)
                    .Sum(account => FxRateHelper.ConvertToUsd(account.Balance, account.CurrencyCode, eurRates));
                var sharesUsd = shareholdings
                    .Where(holding => holding.OwnerPlayerId == player.Id)
                    .Sum(holding => holding.ShareCount * quotedSharePriceUsdByCompany.GetValueOrDefault(holding.CompanyId));
                var goldUsd = goldBalances
                    .Where(balance => balance.PlayerId == player.Id)
                    .Sum(balance => balance.Balance * fallbackGoldPriceUsd);
                var lpUsd = positions
                    .Where(position => position.PlayerId == player.Id)
                    .Sum(position =>
                    {
                        var pool = pools.FirstOrDefault(candidate => candidate.Id == position.PoolId);
                        if (pool is null || pool.TotalLiquidityShares <= 0m || position.LiquidityShares <= 0m)
                        {
                            return 0m;
                        }

                        var ratio = position.LiquidityShares / pool.TotalLiquidityShares;
                        var fiatUsd = FxRateHelper.ConvertToUsd(pool.FiatReserve * ratio, pool.CurrencyCode, eurRates);
                        var poolGoldPriceUsd = poolGoldPriceUsdByPoolId.GetValueOrDefault(pool.Id);
                        if (poolGoldPriceUsd <= 0m)
                        {
                            poolGoldPriceUsd = fallbackGoldPriceUsd;
                        }
                        var goldShareUsd = (pool.GoldReserve * ratio) * poolGoldPriceUsd;
                        return fiatUsd + goldShareUsd;
                    });

                return new EndgameRankingRow(
                    player.Id,
                    player.DisplayName,
                    decimal.Round(cashUsd + sharesUsd + goldUsd + lpUsd, 4, MidpointRounding.AwayFromZero));
            })
            .OrderByDescending(row => row.TotalWealthUsd)
            .ThenBy(row => row.PlayerId)
            .Take(10)
            .ToList();

        var winner = top10.FirstOrDefault();
        if (winner is null || winner.TotalWealthUsd < EndgameCatalog.WinningThresholdUsd)
        {
            return;
        }

        var winnerCompanyName = shareholdings
            .Where(holding => holding.OwnerPlayerId == winner.PlayerId && holding.ShareCount > 0m)
            .Select(holding => new
            {
                holding.CompanyId,
                MarketValue = holding.ShareCount * quotedSharePriceUsdByCompany.GetValueOrDefault(holding.CompanyId),
            })
            .OrderByDescending(item => item.MarketValue)
            .Select(item => context.CompaniesById.GetValueOrDefault(item.CompanyId)?.Name)
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? context.CompaniesById.Values.FirstOrDefault(company => company.PlayerId == winner.PlayerId)?.Name
            ?? "N/A";

        var endedAtUtc = DateTime.UtcNow;
        context.GameState.GameEnded = true;
        context.GameState.GameEndedAtUtc = endedAtUtc;
        context.GameState.WinnerPlayerId = winner.PlayerId;
        context.GameState.WinnerDisplayName = winner.DisplayName;
        context.GameState.WinnerCompanyName = winnerCompanyName;

        try
        {
            var localizations = await BuildFinalNewsLocalizationsAsync(
                db,
                top10,
                winner.DisplayName,
                winnerCompanyName,
                endedAtUtc,
                context.GameState.GameStartedAtUtc,
                eurRates);

            await masterGameAdministrationService.UpsertGameNewsEntryAsync(
                requesterEmail: GameConstants.SystemRequesterEmail,
                entryId: null,
                entryType: "CHANGELOG",
                status: "PUBLISHED",
                localizations: localizations,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish endgame final newsletter.");
        }

        logger.LogInformation(
            "Game ended at tick {Tick}. Winner: {Winner} ({WinnerCompany}) with {Wealth} USD.",
            context.CurrentTick,
            winner.DisplayName,
            winnerCompanyName,
            winner.TotalWealthUsd);
    }

    private static async Task<List<GameNewsLocalizationInput>> BuildFinalNewsLocalizationsAsync(
        DbContext db,
        IReadOnlyList<EndgameRankingRow> top10,
        string winnerDisplayName,
        string winnerCompanyName,
        DateTime endedAtUtc,
        DateTime startedAtUtc,
        IReadOnlyDictionary<string, decimal> eurRates)
    {
        var fxTrades = await db.Set<ForexTradeRecord>()
            .AsNoTracking()
            .Select(trade => new { trade.FromAmount, trade.FromCurrencyCode })
            .ToListAsync();
        var totalFxVolumeUsd = fxTrades.Sum(trade => FxRateHelper.ConvertToUsd(trade.FromAmount, trade.FromCurrencyCode, eurRates));
        var totalProductsSold = await db.Set<PublicSalesRecord>()
            .AsNoTracking()
            .SumAsync(record => (decimal?)record.QuantitySold) ?? 0m;
        var activeCities = await db.Set<Building>()
            .AsNoTracking()
            .Select(building => building.CityId)
            .Distinct()
            .CountAsync();

        var duration = endedAtUtc - startedAtUtc;
        var durationText = $"{Math.Max(0, duration.TotalDays):0.0} days";
        var leaderboardHtml = string.Join(
            string.Empty,
            top10.Select((entry, index) =>
                $"<li><strong>#{index + 1}</strong> {WebUtility.HtmlEncode(entry.DisplayName)} — ${entry.TotalWealthUsd:N0}</li>"));

        var htmlTemplate =
            $"<div class=\"endgame-news\" style=\"border:2px solid #d4af37;padding:16px;border-radius:12px\">" +
            "<p>🏆 <strong>Game Over</strong></p>" +
            $"<p><strong>{WebUtility.HtmlEncode(winnerDisplayName)}</strong> ({WebUtility.HtmlEncode(winnerCompanyName)}) won this shard.</p>" +
            $"<p><strong>Duration:</strong> {WebUtility.HtmlEncode(startedAtUtc.ToString("u", CultureInfo.InvariantCulture))} → {WebUtility.HtmlEncode(endedAtUtc.ToString("u", CultureInfo.InvariantCulture))} ({durationText})</p>" +
            $"<p><strong>Total FX volume:</strong> ${totalFxVolumeUsd:N0}<br/><strong>Total products sold:</strong> {totalProductsSold:N0}<br/><strong>Active cities:</strong> {activeCities}</p>" +
            "<p><strong>Final Top 10</strong></p>" +
            $"<ol>{leaderboardHtml}</ol>" +
            "</div>";

        return
        [
            new()
            {
                Locale = "en",
                Title = "🏆 Endgame reached — final rankings published",
                Summary = $"{winnerDisplayName} has surpassed the billionaire benchmark and won this server.",
                HtmlContent = htmlTemplate,
            },
            new()
            {
                Locale = "sk",
                Title = "🏆 Endgame dosiahnutý — finálne poradie zverejnené",
                Summary = $"{winnerDisplayName} prekonal miliardársky benchmark a vyhral tento server.",
                HtmlContent = htmlTemplate,
            },
            new()
            {
                Locale = "de",
                Title = "🏆 Endgame erreicht — finales Ranking veröffentlicht",
                Summary = $"{winnerDisplayName} hat den Milliardärs-Benchmark übertroffen und diesen Server gewonnen.",
                HtmlContent = htmlTemplate,
            },
        ];
    }

    private static string ResolvePrimaryCurrencyCode(Guid companyId, IEnumerable<Building> buildings) =>
        buildings
            .Where(building => building.CompanyId == companyId)
            .Select(building => building.City?.CurrencyCode)
            .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code))
        ?? "EUR";

    private sealed record EndgameRankingRow(Guid PlayerId, string DisplayName, decimal TotalWealthUsd);
}
