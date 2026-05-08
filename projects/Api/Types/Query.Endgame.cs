using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns winner/freeze status and the real-world billionaire benchmark used by this shard.
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

        return new EndgameStatusResult
        {
            GameEnded = gameState?.GameEnded ?? false,
            WinnerPlayerId = gameState?.WinnerPlayerId,
            WinnerDisplayName = gameState?.WinnerDisplayName,
            WinnerCompanyName = gameState?.WinnerCompanyName,
            GameEndedAtUtc = gameState?.GameEndedAtUtc,
            WinningThresholdUsd = benchmarks
                .OrderBy(item => item.Rank)
                .ThenByDescending(item => item.WealthUsd)
                .FirstOrDefault()?.WealthUsd
                ?? EndgameCatalog.DefaultWinningThresholdUsd,
            TopRealWorldRichest = benchmarks
                .OrderBy(item => item.Rank)
                .ThenByDescending(item => item.WealthUsd)
                .Select(item => new RealWorldWealthResult
                {
                    Id = item.Id,
                    Rank = item.Rank,
                    Name = item.Name,
                    WealthUsd = item.WealthUsd,
                })
                .ToList(),
        };
    }
}
