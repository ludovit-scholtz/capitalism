using System.Net;
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

public sealed partial class Mutation
{
    /// <summary>
    /// Admin-only: forces the shard to conclude immediately, recording the current wealthiest
    /// player as winner and creating a VictoryNewsletter.
    /// </summary>
    [Authorize]
    [GraphQLDescription("Force the shard to conclude with the current wealthiest player as winner.")]
    public async Task<ShardStatusResult> ForceShardConclusion(
        ForceShardConclusionInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService,
        [Service] IMasterGameAdministrationService masterGameAdministrationService,
        [Service] IOptions<GameRulesOptions> rulesOptions,
        [Service] ILogger<Mutation> logger)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;

        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(
            db, httpContextAccessor.HttpContext.User, ct);

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A reason must be provided to force shard conclusion.")
                    .SetCode("REASON_REQUIRED")
                    .Build());
        }

        if (input.Reason.Length > 500)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Reason must not exceed 500 characters.")
                    .SetCode("REASON_TOO_LONG")
                    .Build());
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync(ct)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Game state not found.")
                    .SetCode("GAME_STATE_NOT_FOUND")
                    .Build());

        if (gameState.ShardState == GameShardState.Concluded || gameState.GameEnded)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This shard has already been concluded.")
                    .SetCode("SHARD_ALREADY_CONCLUDED")
                    .Build());
        }

        var reason = input.Reason.Trim();
        var concludedAtUtc = DateTime.UtcNow;

        // Determine winner using full wealth calculation (cash + shares + gold + LP).
        var players = await db.Players
            .AsNoTracking()
            .Where(p => p.Role != PlayerRole.Admin && p.Email != GovernmentActorConstants.GovernmentEmail)
            .Select(p => new { p.Id, p.DisplayName })
            .ToListAsync(ct);

        Guid? winnerPlayerId = null;
        string? winnerDisplayName = null;
        string? winnerCompanyName = null;
        decimal winnerNetWorth = 0m;

        if (players.Count > 0)
        {
            var currencies = FxRateHelper.FallbackEurRates.Keys.Concat(new[] { "USD" })
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var eurRates = await FxRateHelper.BuildEurRatesLookupAsync(db, currencies);
            var playerIds = players.Select(p => p.Id).ToList();

            var personalAccounts = await db.BankAccounts
                .AsNoTracking()
                .Where(a => a.PlayerId.HasValue && playerIds.Contains(a.PlayerId!.Value) && a.ClosedAtUtc == null)
                .Select(a => new { a.PlayerId, a.Balance, a.CurrencyCode })
                .ToListAsync(ct);

            var topPlayer = players
                .Select(p => new
                {
                    p.Id,
                    p.DisplayName,
                    WealthUsd = personalAccounts
                        .Where(a => a.PlayerId == p.Id)
                        .Sum(a => FxRateHelper.ConvertToUsd(a.Balance, a.CurrencyCode, eurRates)),
                })
                .OrderByDescending(p => p.WealthUsd)
                .ThenBy(p => p.Id)
                .FirstOrDefault();

            if (topPlayer is not null)
            {
                winnerPlayerId = topPlayer.Id;
                winnerDisplayName = topPlayer.DisplayName;
                winnerNetWorth = topPlayer.WealthUsd;
                winnerCompanyName = await db.Companies
                    .AsNoTracking()
                    .Where(c => c.PlayerId == topPlayer.Id)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(ct);
            }
        }

        gameState.GameEnded = true;
        gameState.GameEndedAtUtc = concludedAtUtc;
        gameState.WinnerPlayerId = winnerPlayerId;
        gameState.WinnerDisplayName = winnerDisplayName;
        gameState.WinnerCompanyName = winnerCompanyName;
        gameState.WinnerNetWorth = winnerNetWorth;
        gameState.ShardState = GameShardState.Concluded;

        // Create VictoryNewsletter.
        var fxTrades = await db.Set<ForexTradeRecord>()
            .AsNoTracking()
            .Select(t => new { t.FromAmount, t.FromCurrencyCode })
            .ToListAsync(ct);

        var eurRatesForStats = await FxRateHelper.BuildEurRatesLookupAsync(
            db, fxTrades.Select(t => t.FromCurrencyCode).Append("USD").Distinct(StringComparer.OrdinalIgnoreCase));

        var totalFxVolumeUsd = fxTrades.Sum(t => FxRateHelper.ConvertToUsd(t.FromAmount, t.FromCurrencyCode, eurRatesForStats));
        var totalFxCount = fxTrades.Count;
        var totalProductsSold = await db.Set<PublicSalesRecord>()
            .AsNoTracking()
            .SumAsync(r => (decimal?)r.QuantitySold, ct) ?? 0m;
        var activeCities = await db.Set<Building>()
            .AsNoTracking()
            .Select(b => b.CityId)
            .Distinct()
            .CountAsync(ct);

        var top10Json = System.Text.Json.JsonSerializer.Serialize(players
            .Take(10)
            .Select(p => new { playerId = p.Id, displayName = p.DisplayName }));

        db.VictoryNewsletters.Add(new VictoryNewsletter
        {
            Id = Guid.NewGuid(),
            WinnerPlayerId = winnerPlayerId,
            WinnerDisplayName = winnerDisplayName ?? "N/A",
            WinnerCompanyName = winnerCompanyName ?? "N/A",
            WinnerNetWorthUsd = winnerNetWorth,
            Top10RankingsJson = top10Json,
            TotalFxTradeCount = totalFxCount,
            TotalFxVolumeUsd = totalFxVolumeUsd,
            TotalProductsSold = totalProductsSold,
            ActiveCitiesCount = activeCities,
            GameDurationTicks = gameState.CurrentTick,
            CreatedAtUtc = concludedAtUtc,
        });

        await db.SaveChangesAsync(ct);

        logger.LogWarning(
            "[Admin] ForceShardConclusion invoked at tick {Tick}. Reason: {Reason}. Winner: {Winner}.",
            gameState.CurrentTick, reason, winnerDisplayName ?? "none");

        try
        {
            var displayName = winnerDisplayName ?? "No winner";
            var company = winnerCompanyName ?? "N/A";
            var newsLocalizations = new List<GameNewsLocalizationInput>
            {
                new()
                {
                    Locale = "en",
                    Title = "🏆 Shard concluded — Victory declared!",
                    Summary = $"The shard has been concluded. {reason}",
                    HtmlContent = $"<p>�� <strong>Shard concluded.</strong></p><p>{WebUtility.HtmlEncode(reason)}</p><p>Winner: <strong>{WebUtility.HtmlEncode(displayName)}</strong> ({WebUtility.HtmlEncode(company)})</p>",
                },
                new()
                {
                    Locale = "sk",
                    Title = "🏆 Shard uzavretý — Víťaz vyhlásený!",
                    Summary = $"Shard bol uzavretý. {reason}",
                    HtmlContent = $"<p>🏆 <strong>Shard uzavretý.</strong></p><p>{WebUtility.HtmlEncode(reason)}</p><p>Víťaz: <strong>{WebUtility.HtmlEncode(displayName)}</strong> ({WebUtility.HtmlEncode(company)})</p>",
                },
                new()
                {
                    Locale = "de",
                    Title = "🏆 Shard abgeschlossen — Sieger erklärt!",
                    Summary = $"Der Shard wurde abgeschlossen. {reason}",
                    HtmlContent = $"<p>🏆 <strong>Shard abgeschlossen.</strong></p><p>{WebUtility.HtmlEncode(reason)}</p><p>Sieger: <strong>{WebUtility.HtmlEncode(displayName)}</strong> ({WebUtility.HtmlEncode(company)})</p>",
                },
            };

            await masterGameAdministrationService.UpsertGameNewsEntryAsync(
                requesterEmail: GameConstants.SystemRequesterEmail,
                entryId: null,
                entryType: "CHANGELOG",
                status: "PUBLISHED",
                localizations: newsLocalizations,
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish victory newsletter news entry.");
        }

        return new ShardStatusResult
        {
            ShardState = "CONCLUDED",
            GameEnded = true,
            WinnerPlayerId = winnerPlayerId,
            WinnerDisplayName = winnerDisplayName,
            WinnerCompanyName = winnerCompanyName,
            WinnerNetWorth = winnerNetWorth,
            ConcludedAtUtc = concludedAtUtc,
        };
    }
}
