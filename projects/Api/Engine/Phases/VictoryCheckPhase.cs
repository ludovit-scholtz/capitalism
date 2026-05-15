using System.Net;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Engine.Phases;

/// <summary>
/// Checks whether any player has exceeded the configured net-worth threshold and,
/// if so, marks the shard as Concluded and records a VictoryNewsletter.
/// Runs at order 9999 (after EndgamePhase at 1200) so it sees any changes from that phase.
/// </summary>
public sealed class VictoryCheckPhase(
    IOptions<GameRulesOptions> rulesOptions,
    ILogger<VictoryCheckPhase> logger) : ITickPhase
{
    public string Name => "VictoryCheck";
    public int Order => 9999;

    public async Task ProcessAsync(TickContext context)
    {
        // Idempotent: if the shard is already concluded skip.
        if (context.GameState.GameEnded)
        {
            return;
        }

        var db = context.Db;
        var threshold = rulesOptions.Value.BillionaireNetWorthBenchmarkUsd;

        var players = await db.Players
            .AsNoTracking()
            .Where(p => p.Role != PlayerRole.Admin
                && p.Email != GovernmentActorConstants.GovernmentEmail)
            .ToListAsync();
        if (players.Count == 0)
        {
            return;
        }

        var shareholdings = await db.Shareholdings
            .AsNoTracking()
            .Where(h => h.ShareCount > 0m)
            .ToListAsync();

        var companies = context.CompaniesById.Values.ToList();
        var buildings = context.BuildingsById.Values.ToList();
        var lots = context.LotsByCompany.Values.SelectMany(list => list).DistinctBy(l => l.Id).ToList();
        var inventories = context.InventoryByBuilding.Values.SelectMany(list => list).DistinctBy(i => i.Id).ToList();

        var companyCurrencyById = companies.ToDictionary(
            c => c.Id,
            c => buildings
                .Where(b => b.CompanyId == c.Id)
                .Select(b => b.City?.CurrencyCode)
                .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code))
                ?? "EUR");

        var personalAccounts = context.BankAccountsById.Values
            .Where(a => a.PlayerId.HasValue && a.ClosedAtUtc == null)
            .ToList();

        var goldBalances = await db.PlayerGoldBalances.AsNoTracking().ToListAsync();
        var pools = await db.GoldAmmPools.AsNoTracking().ToListAsync();
        var positions = await db.GoldAmmPositions.AsNoTracking().ToListAsync();

        var currencies = personalAccounts
            .Select(a => a.CurrencyCode)
            .Concat(companyCurrencyById.Values)
            .Concat(pools.Select(p => p.CurrencyCode))
            .Append("USD")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var eurRates = await FxRateHelper.BuildEurRatesLookupAsync(db, currencies);

        var baseEquity = SharePriceCalculator.ComputeBaseEquityByCompany(companies, buildings, lots, inventories);
        var quotedSharePriceLocal = SharePriceCalculator.ComputeQuotedSharePriceByCompany(companies, baseEquity, shareholdings);
        var quotedSharePriceUsd = quotedSharePriceLocal.ToDictionary(
            kvp => kvp.Key,
            kvp => FxRateHelper.ConvertToUsd(kvp.Value, companyCurrencyById.GetValueOrDefault(kvp.Key, "EUR"), eurRates));

        var poolGoldPriceUsd = pools.ToDictionary(
            p => p.Id,
            p => p.GoldReserve <= 0m || p.FiatReserve <= 0m
                ? 0m
                : FxRateHelper.ConvertToUsd(p.FiatReserve / p.GoldReserve, p.CurrencyCode, eurRates));
        var fallbackGoldPriceUsd = poolGoldPriceUsd.Values
            .Where(v => v > 0m)
            .DefaultIfEmpty(EndgameCatalog.DefaultGoldPriceUsd)
            .Average();

        var ranked = players
            .Select(player =>
            {
                var cashUsd = personalAccounts
                    .Where(a => a.PlayerId == player.Id)
                    .Sum(a => FxRateHelper.ConvertToUsd(a.Balance, a.CurrencyCode, eurRates));
                var sharesUsd = shareholdings
                    .Where(h => h.OwnerPlayerId == player.Id)
                    .Sum(h => h.ShareCount * quotedSharePriceUsd.GetValueOrDefault(h.CompanyId));
                var goldUsd = goldBalances
                    .Where(b => b.PlayerId == player.Id)
                    .Sum(b => b.Balance * fallbackGoldPriceUsd);
                var lpUsd = positions
                    .Where(pos => pos.PlayerId == player.Id)
                    .Sum(pos =>
                    {
                        var pool = pools.FirstOrDefault(p => p.Id == pos.PoolId);
                        if (pool is null || pool.TotalLiquidityShares <= 0m || pos.LiquidityShares <= 0m)
                        {
                            return 0m;
                        }

                        var ratio = pos.LiquidityShares / pool.TotalLiquidityShares;
                        var fiatUsd = FxRateHelper.ConvertToUsd(pool.FiatReserve * ratio, pool.CurrencyCode, eurRates);
                        var gpUsd = poolGoldPriceUsd.GetValueOrDefault(pool.Id) is var gp && gp > 0 ? gp : fallbackGoldPriceUsd;
                        return fiatUsd + pool.GoldReserve * ratio * gpUsd;
                    });

                return (player.Id, player.DisplayName,
                    TotalWealthUsd: decimal.Round(cashUsd + sharesUsd + goldUsd + lpUsd, 4, MidpointRounding.AwayFromZero));
            })
            .OrderByDescending(r => r.TotalWealthUsd)
            .ThenBy(r => r.Id)
            .Take(10)
            .ToList();

        var winner = ranked.Count > 0 ? ranked[0] : default;
        if (winner == default || winner.TotalWealthUsd < threshold)
        {
            return;
        }

        var winnerCompanyName = shareholdings
            .Where(h => h.OwnerPlayerId == winner.Id && h.ShareCount > 0m)
            .Select(h => new
            {
                h.CompanyId,
                MarketValue = h.ShareCount * quotedSharePriceUsd.GetValueOrDefault(h.CompanyId),
            })
            .OrderByDescending(i => i.MarketValue)
            .Select(i => context.CompaniesById.GetValueOrDefault(i.CompanyId)?.Name)
            .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
            ?? context.CompaniesById.Values.FirstOrDefault(c => c.PlayerId == winner.Id)?.Name
            ?? "N/A";

        var concludedAtUtc = DateTime.UtcNow;
        context.GameState.GameEnded = true;
        context.GameState.GameEndedAtUtc = concludedAtUtc;
        context.GameState.WinnerPlayerId = winner.Id;
        context.GameState.WinnerDisplayName = winner.DisplayName;
        context.GameState.WinnerCompanyName = winnerCompanyName;
        context.GameState.WinnerNetWorth = winner.TotalWealthUsd;
        context.GameState.ShardState = GameShardState.Concluded;

        // Gather newsletter statistics.
        var fxTrades = await db.Set<ForexTradeRecord>()
            .AsNoTracking()
            .Select(t => new { t.FromAmount, t.FromCurrencyCode })
            .ToListAsync();
        var totalFxVolumeUsd = fxTrades.Sum(t => FxRateHelper.ConvertToUsd(t.FromAmount, t.FromCurrencyCode, eurRates));
        var totalFxCount = fxTrades.Count;
        var totalProductsSold = await db.Set<PublicSalesRecord>()
            .AsNoTracking()
            .SumAsync(r => (decimal?)r.QuantitySold) ?? 0m;
        var activeCities = await db.Set<Building>()
            .AsNoTracking()
            .Select(b => b.CityId)
            .Distinct()
            .CountAsync();
        var gameDuration = context.GameState.CurrentTick;

        var top10Json = System.Text.Json.JsonSerializer.Serialize(ranked.Select(r => new
        {
            playerId = r.Id,
            displayName = r.DisplayName,
            totalWealthUsd = r.TotalWealthUsd,
        }));

        db.VictoryNewsletters.Add(new VictoryNewsletter
        {
            Id = Guid.NewGuid(),
            WinnerPlayerId = winner.Id,
            WinnerDisplayName = winner.DisplayName,
            WinnerCompanyName = winnerCompanyName,
            WinnerNetWorthUsd = winner.TotalWealthUsd,
            Top10RankingsJson = top10Json,
            TotalFxTradeCount = totalFxCount,
            TotalFxVolumeUsd = totalFxVolumeUsd,
            TotalProductsSold = totalProductsSold,
            ActiveCitiesCount = activeCities,
            GameDurationTicks = gameDuration,
            CreatedAtUtc = concludedAtUtc,
        });

        logger.LogInformation(
            "[VictoryCheck] Shard concluded at tick {Tick}. Winner: {Winner} ({Company}) — ${Wealth:N0}.",
            context.CurrentTick,
            winner.DisplayName,
            winnerCompanyName,
            winner.TotalWealthUsd);
    }
}
