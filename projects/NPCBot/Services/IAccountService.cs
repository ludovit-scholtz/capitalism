using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Abstraction over the game API account operations.
/// Extracted so <see cref="BotOrchestrator"/> can be tested with a fake implementation.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Registers a new NPC account. Falls back to login on DUPLICATE_EMAIL.
    /// </summary>
    Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(BotAccount bot, CancellationToken ct);

    /// <summary>Logs in with the bot's credentials and returns the new token.</summary>
    Task<(string token, DateTime expiresAt)> LoginAsync(BotAccount bot, CancellationToken ct);

    /// <summary>Fetches the full player profile for the authenticated bot.</summary>
    Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct);

    /// <summary>Fetches the current game tick and configuration.</summary>
    Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct);

    /// <summary>Fetches the current global player rankings.</summary>
    Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct);

    /// <summary>Updates the minimum sale price on a PUBLIC_SALES building unit.</summary>
    Task<UnitSummary> UpdatePublicSalesPriceAsync(string unitId, decimal newMinPrice, string token, CancellationToken ct);
}
