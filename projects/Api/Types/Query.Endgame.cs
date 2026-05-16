using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns winner/freeze status, the real-world billionaire benchmark used by this shard,
    /// and the current server-wide leader's net worth for the Race to the Top progress indicator.
    /// </summary>
    public async Task<EndgameStatusResult> GetEndgameStatus([Service] AppDbContext db)
    {
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var benchmarkRows = await db.RealWorldBillionaires
            .AsNoTracking()
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.WealthUsd)
            .ToListAsync();
        var benchmarks = benchmarkRows.Count > 0
            ? benchmarkRows
            : EndgameCatalog.DefaultTopTenRichestPeople
                .Select((item, index) => new RealWorldBillionaire
                {
                    Id = Guid.Parse($"00000000-0000-0000-0000-0000000000{index + 1:00}"),
                    Rank = index + 1,
                    Name = item.Name,
                    WealthUsd = item.WealthUsd,
                })
                .ToList();
        var orderedBenchmarks = benchmarks
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.WealthUsd)
            .ToList();
        var winningThresholdUsd = orderedBenchmarks.FirstOrDefault()?.WealthUsd
            ?? EndgameCatalog.DefaultWinningThresholdUsd;

        // Compute the current server-wide leader's net worth for the Race to Top progress banner.
        var (leaderDisplayName, leaderNetWorthUsd) = await ComputeServerLeaderAsync(db, gameState);

        return new EndgameStatusResult
        {
            GameEnded = gameState?.GameEnded ?? false,
            WinnerPlayerId = gameState?.WinnerPlayerId,
            WinnerDisplayName = gameState?.WinnerDisplayName,
            WinnerCompanyName = gameState?.WinnerCompanyName,
            GameEndedAtUtc = gameState?.GameEndedAtUtc,
            WinningThresholdUsd = winningThresholdUsd,
            TopRealWorldRichest = orderedBenchmarks
                .Select(item => new RealWorldWealthResult
                {
                    Id = item.Id,
                    Rank = item.Rank,
                    Name = item.Name,
                    WealthUsd = item.WealthUsd,
                })
                .ToList(),
            LeaderDisplayName = leaderDisplayName,
            LeaderNetWorthUsd = leaderNetWorthUsd,
        };
    }

    /// <summary>
    /// Returns the display name and approximate personal net worth (USD) of the current server
    /// leader — the player whose personal bank account cash + stock portfolio is highest.
    /// Excludes admin and government accounts.
    /// </summary>
    private static async Task<(string? DisplayName, decimal NetWorthUsd)> ComputeServerLeaderAsync(
        AppDbContext db,
        GameState? gameState)
    {
        // If the game has already ended the winner is the permanent leader.
        if (gameState?.GameEnded == true && gameState.WinnerDisplayName is not null)
        {
            return (gameState.WinnerDisplayName, gameState.WinnerNetWorth ?? 0m);
        }

        var players = await db.Players
            .AsNoTracking()
            .Where(p => p.Role != PlayerRole.Admin
                && p.Email != GovernmentActorConstants.GovernmentEmail)
            .ToListAsync();

        if (players.Count == 0)
        {
            return (null, 0m);
        }

        // Load personal bank accounts for all non-admin players.
        var playerIds = players.Select(p => p.Id).ToList();
        var personalAccounts = await db.BankAccounts
            .AsNoTracking()
            .Where(a => a.PlayerId.HasValue && playerIds.Contains(a.PlayerId!.Value) && a.ClosedAtUtc == null)
            .ToListAsync();

        // Load shareholdings for portfolio valuation.
        var shareholdings = await db.Shareholdings
            .AsNoTracking()
            .Where(h => h.OwnerPlayerId.HasValue && playerIds.Contains(h.OwnerPlayerId!.Value) && h.ShareCount > 0m)
            .ToListAsync();

        var companies = await db.Companies.AsNoTracking().ToListAsync();
        var buildings = await db.Buildings.AsNoTracking().Include(b => b.City).ToListAsync();
        var lots = await db.BuildingLots.AsNoTracking().Where(l => l.OwnerCompanyId.HasValue).ToListAsync();
        var inventories = await db.Inventories.AsNoTracking()
            .Include(i => i.ResourceType)
            .Include(i => i.ProductType)
            .ToListAsync();

        var companyCurrencyById = companies.ToDictionary(
            c => c.Id,
            c => buildings.Where(b => b.CompanyId == c.Id)
                .Select(b => b.City?.CurrencyCode)
                .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code)) ?? "EUR");

        var allCurrencies = personalAccounts.Select(a => a.CurrencyCode)
            .Concat(companyCurrencyById.Values)
            .Append("USD")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var eurRates = await FxRateHelper.BuildEurRatesLookupAsync(db, allCurrencies);

        var localSharePriceByCompany = SharePriceCalculator.ComputeQuotedSharePriceByCompany(
            companies,
            SharePriceCalculator.ComputeBaseEquityByCompany(companies, buildings, lots, inventories),
            shareholdings);
        var sharePriceUsdByCompany = localSharePriceByCompany.ToDictionary(
            kvp => kvp.Key,
            kvp => FxRateHelper.ConvertToUsd(kvp.Value, companyCurrencyById.GetValueOrDefault(kvp.Key, "EUR"), eurRates));

        string? leaderDisplayName = null;
        var leaderNetWorthUsd = 0m;

        foreach (var player in players)
        {
            var cashUsd = personalAccounts
                .Where(a => a.PlayerId == player.Id)
                .Sum(a => FxRateHelper.ConvertToUsd(a.Balance, a.CurrencyCode, eurRates));
            var sharesUsd = shareholdings
                .Where(h => h.OwnerPlayerId == player.Id)
                .Sum(h => h.ShareCount * sharePriceUsdByCompany.GetValueOrDefault(h.CompanyId));
            var totalUsd = decimal.Round(cashUsd + sharesUsd, 4, MidpointRounding.AwayFromZero);
            if (totalUsd > leaderNetWorthUsd)
            {
                leaderNetWorthUsd = totalUsd;
                leaderDisplayName = PublicPlayerDisplayName.Resolve(player);
            }
        }

        return (leaderDisplayName, leaderNetWorthUsd);
    }
}
