using Api.Data;
using Api.Utilities;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns winner/freeze status and the real-world billionaire benchmark used by this shard.
    /// </summary>
    public async Task<EndgameStatusResult> GetEndgameStatus([Service] AppDbContext db)
    {
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        return new EndgameStatusResult
        {
            GameEnded = gameState?.GameEnded ?? false,
            WinnerPlayerId = gameState?.WinnerPlayerId,
            WinnerDisplayName = gameState?.WinnerDisplayName,
            WinnerCompanyName = gameState?.WinnerCompanyName,
            GameEndedAtUtc = gameState?.GameEndedAtUtc,
            WinningThresholdUsd = EndgameCatalog.WinningThresholdUsd,
            TopRealWorldRichest = EndgameCatalog.TopFiveRichestPeople
                .Select(item => new RealWorldWealthResult
                {
                    Name = item.Name,
                    WealthUsd = item.WealthUsd,
                })
                .ToList(),
        };
    }
}
