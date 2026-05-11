using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Types;

public sealed partial class Mutation
{
    [Authorize]
    public async Task<GameAdminPlayerSummary> SetPlayerInvisibleInChat(
        SetPlayerInvisibleInChatInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(db, httpContextAccessor.HttpContext!.User, httpContextAccessor.HttpContext.RequestAborted);
        var player = await db.Players
            .Include(candidate => candidate.Companies)
            .ThenInclude(company => company.BankAccounts)
            .FirstOrDefaultAsync(candidate => candidate.Id == input.PlayerId, httpContextAccessor.HttpContext.RequestAborted)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        player.IsInvisibleInChat = input.IsInvisibleInChat;
        await db.SaveChangesAsync(httpContextAccessor.HttpContext.RequestAborted);
        var personalCash = await PersonalBankAccountService.GetGrossCashAsync(db, player, httpContextAccessor.HttpContext.RequestAborted);

        return new GameAdminPlayerSummary
        {
            Id = player.Id,
            Email = player.Email,
            DisplayName = player.DisplayName,
            Role = player.Role,
            IsInvisibleInChat = player.IsInvisibleInChat,
            CreatedAtUtc = player.CreatedAtUtc,
            LastLoginAtUtc = player.LastLoginAtUtc,
            PersonalCash = personalCash,
            TotalCompanyCash = player.Companies.Sum(CompanyBankingService.GetTotalBalance),
            TotalCompanyEquity = player.Companies.Sum(CompanyBankingService.GetTotalBalance),
            CompanyCount = player.Companies.Count,
            CityNames = [],
            Companies = player.Companies.Select(company => new GameAdminCompanySummary
            {
                Id = company.Id,
                Name = company.Name,
                Cash = CompanyBankingService.GetTotalBalance(company),
            }).ToList(),
        };
    }

    [Authorize]
    public async Task<GameAdminPlayerSummary> SetLocalGameAdminRole(
        SetLocalGameAdminRoleInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        await gameAdminAuthorizationService.RequireRootAccessAsync(db, httpContextAccessor.HttpContext!.User, httpContextAccessor.HttpContext.RequestAborted);
        var player = await db.Players
            .Include(candidate => candidate.Companies)
            .ThenInclude(company => company.BankAccounts)
            .FirstOrDefaultAsync(candidate => candidate.Id == input.PlayerId, httpContextAccessor.HttpContext.RequestAborted)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        player.Role = input.IsAdmin ? PlayerRole.Admin : PlayerRole.Player;
        await db.SaveChangesAsync(httpContextAccessor.HttpContext.RequestAborted);
        var personalCash = await PersonalBankAccountService.GetGrossCashAsync(db, player, httpContextAccessor.HttpContext.RequestAborted);

        return new GameAdminPlayerSummary
        {
            Id = player.Id,
            Email = player.Email,
            DisplayName = player.DisplayName,
            Role = player.Role,
            IsInvisibleInChat = player.IsInvisibleInChat,
            CreatedAtUtc = player.CreatedAtUtc,
            LastLoginAtUtc = player.LastLoginAtUtc,
            PersonalCash = personalCash,
            TotalCompanyCash = player.Companies.Sum(CompanyBankingService.GetTotalBalance),
            TotalCompanyEquity = player.Companies.Sum(CompanyBankingService.GetTotalBalance),
            CompanyCount = player.Companies.Count,
            CityNames = [],
            Companies = player.Companies.Select(company => new GameAdminCompanySummary
            {
                Id = company.Id,
                Name = company.Name,
                Cash = CompanyBankingService.GetTotalBalance(company),
            }).ToList(),
        };
    }

    [Authorize]
    public async Task<GlobalGameAdminGrantSummary> AssignGlobalGameAdminRole(
        ManageGlobalGameAdminRoleInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService,
        [Service] IMasterGameAdministrationService masterGameAdministrationService)
    {
        var accessContext = await gameAdminAuthorizationService.RequireRootAccessAsync(db, httpContextAccessor.HttpContext!.User, httpContextAccessor.HttpContext.RequestAborted);
        return await masterGameAdministrationService.AssignGlobalGameAdminAsync(accessContext.ActorPlayer.Email, input.Email, httpContextAccessor.HttpContext.RequestAborted);
    }

    [Authorize]
    public async Task<bool> RemoveGlobalGameAdminRole(
        ManageGlobalGameAdminRoleInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService,
        [Service] IMasterGameAdministrationService masterGameAdministrationService)
    {
        var accessContext = await gameAdminAuthorizationService.RequireRootAccessAsync(db, httpContextAccessor.HttpContext!.User, httpContextAccessor.HttpContext.RequestAborted);
        await masterGameAdministrationService.RemoveGlobalGameAdminAsync(accessContext.ActorPlayer.Email, input.Email, httpContextAccessor.HttpContext.RequestAborted);
        return true;
    }

    [Authorize]
    public async Task<RealWorldBillionaireAdminRecord> UpdateRealWorldBillionaire(
        UpdateRealWorldBillionaireInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(db, httpContextAccessor.HttpContext!.User, httpContextAccessor.HttpContext.RequestAborted);
        var target = await db.RealWorldBillionaires
            .FirstOrDefaultAsync(item => item.Id == input.Id, httpContextAccessor.HttpContext.RequestAborted)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Real-world billionaire benchmark not found.")
                    .SetCode("REAL_WORLD_BENCHMARK_NOT_FOUND")
                    .Build());

        var duplicateRank = await db.RealWorldBillionaires
            .AnyAsync(item => item.Rank == input.Rank && item.Id != input.Id, httpContextAccessor.HttpContext.RequestAborted);
        if (duplicateRank)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Rank {input.Rank} is already used by another benchmark.")
                    .SetCode("REAL_WORLD_BENCHMARK_RANK_CONFLICT")
                    .Build());
        }

        target.Rank = input.Rank;
        target.Name = input.Name.Trim();
        target.WealthUsd = decimal.Round(input.WealthUsd, 2, MidpointRounding.AwayFromZero);
        target.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(httpContextAccessor.HttpContext.RequestAborted);

        return new RealWorldBillionaireAdminRecord
        {
            Id = target.Id,
            Rank = target.Rank,
            Name = target.Name,
            WealthUsd = target.WealthUsd,
            UpdatedAtUtc = target.UpdatedAtUtc,
        };
    }

    [Authorize]
    public async Task<GameNewsEntryResult> UpsertGameNewsEntry(
        UpsertGameNewsEntryInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService,
        [Service] IMasterGameAdministrationService masterGameAdministrationService)
    {
        var accessContext = await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(db, httpContextAccessor.HttpContext!.User, httpContextAccessor.HttpContext.RequestAborted);
        return await masterGameAdministrationService.UpsertGameNewsEntryAsync(
            accessContext.ActorPlayer.Email,
            input.EntryId,
            input.EntryType,
            input.Status,
            input.Localizations,
            httpContextAccessor.HttpContext.RequestAborted);
    }

    [Authorize]
    public async Task<bool> MarkGameNewsRead(
        MarkGameNewsReadInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IMasterGameAdministrationService masterGameAdministrationService)
    {
        var effectiveUserId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var playerEmail = await db.Players
            .AsNoTracking()
            .Where(player => player.Id == effectiveUserId)
            .Select(player => player.Email)
            .FirstOrDefaultAsync(httpContextAccessor.HttpContext.RequestAborted)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        await masterGameAdministrationService.MarkGameNewsReadAsync(playerEmail, input.EntryIds, httpContextAccessor.HttpContext.RequestAborted);
        return true;
    }

    [Authorize]
    public async Task<int> MarkAllGameNewsRead(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IMasterGameAdministrationService masterGameAdministrationService)
    {
        var effectiveUserId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var playerEmail = await db.Players
            .AsNoTracking()
            .Where(player => player.Id == effectiveUserId)
            .Select(player => player.Email)
            .FirstOrDefaultAsync(httpContextAccessor.HttpContext.RequestAborted)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        return await masterGameAdministrationService.MarkAllGameNewsReadAsync(playerEmail, httpContextAccessor.HttpContext.RequestAborted);
    }

    /// <summary>
    /// Admin override: immediately ends the current game shard, records the current leader
    /// as the winner (if any players exist), and publishes a final newsletter.
    /// Requires admin dashboard access.
    /// </summary>
    [Authorize]
    public async Task<EndgameStatusResult> EndShardManually(
        EndShardManuallyInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService,
        [Service] IMasterGameAdministrationService masterGameAdministrationService,
        [Service] ILogger<Mutation> logger)
    {
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(
            db,
            httpContextAccessor.HttpContext!.User,
            httpContextAccessor.HttpContext.RequestAborted);

        if (input.Reason is { Length: > 500 })
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Reason must not exceed 500 characters.")
                    .SetCode("REASON_TOO_LONG")
                    .Build());
        }

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync(httpContextAccessor.HttpContext.RequestAborted)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Game state not found.")
                    .SetCode("GAME_STATE_NOT_FOUND")
                    .Build());

        if (gameState.GameEnded)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The game has already ended.")
                    .SetCode("GAME_ALREADY_ENDED")
                    .Build());
        }

        var reason = string.IsNullOrWhiteSpace(input.Reason) ? "Manual admin override." : input.Reason.Trim();
        var endedAtUtc = DateTime.UtcNow;

        // Determine the current leader from personal bank balances.
        var players = await db.Players
            .AsNoTracking()
            .Where(p => p.Role != PlayerRole.Admin && p.Email != GovernmentActorConstants.GovernmentEmail)
            .Select(p => new { p.Id, p.DisplayName })
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);

        Guid? winnerPlayerId = null;
        string? winnerDisplayName = null;
        string? winnerCompanyName = null;

        if (players.Count > 0)
        {
            var currencies = FxRateHelper.FallbackEurRates.Keys.Concat(new[] { "USD" }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var eurRates = await FxRateHelper.BuildEurRatesLookupAsync(db, currencies);
            var playerIds = players.Select(p => p.Id).ToList();
            var personalAccounts = await db.BankAccounts
                .AsNoTracking()
                .Where(a => a.PlayerId.HasValue && playerIds.Contains(a.PlayerId!.Value) && a.ClosedAtUtc == null)
                .Select(a => new { a.PlayerId, a.Balance, a.CurrencyCode })
                .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);

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
                winnerCompanyName = (await db.Companies
                    .AsNoTracking()
                    .Where(c => c.PlayerId == topPlayer.Id)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync(httpContextAccessor.HttpContext.RequestAborted));
            }
        }

        gameState.GameEnded = true;
        gameState.GameEndedAtUtc = endedAtUtc;
        gameState.WinnerPlayerId = winnerPlayerId;
        gameState.WinnerDisplayName = winnerDisplayName;
        gameState.WinnerCompanyName = winnerCompanyName;
        await db.SaveChangesAsync(httpContextAccessor.HttpContext.RequestAborted);

        logger.LogWarning(
            "Game shard ended manually by admin at {EndedAtUtc}. Reason: {Reason}. Winner: {Winner}.",
            endedAtUtc,
            reason,
            winnerDisplayName ?? "none");

        try
        {
            var displayName = winnerDisplayName ?? "No winner";
            var company = winnerCompanyName ?? "N/A";
            var newsLocalizations = new List<GameNewsLocalizationInput>
            {
                new()
                {
                    Locale = "en",
                    Title = "🛑 Shard ended by administrator",
                    Summary = $"An administrator has ended this game shard. {reason}",
                    HtmlContent = $"<p>🛑 <strong>Shard ended by administrator.</strong></p><p>{System.Net.WebUtility.HtmlEncode(reason)}</p><p>Leader at time of closure: <strong>{System.Net.WebUtility.HtmlEncode(displayName)}</strong> ({System.Net.WebUtility.HtmlEncode(company)})</p>",
                },
                new()
                {
                    Locale = "sk",
                    Title = "🛑 Shard ukončený administrátorom",
                    Summary = $"Administrátor ukončil tento shard. {reason}",
                    HtmlContent = $"<p>🛑 <strong>Shard ukončený administrátorom.</strong></p><p>{System.Net.WebUtility.HtmlEncode(reason)}</p><p>Líder v čase ukončenia: <strong>{System.Net.WebUtility.HtmlEncode(displayName)}</strong> ({System.Net.WebUtility.HtmlEncode(company)})</p>",
                },
                new()
                {
                    Locale = "de",
                    Title = "🛑 Shard durch Administrator beendet",
                    Summary = $"Ein Administrator hat diesen Shard beendet. {reason}",
                    HtmlContent = $"<p>🛑 <strong>Shard durch Administrator beendet.</strong></p><p>{System.Net.WebUtility.HtmlEncode(reason)}</p><p>Führender Spieler: <strong>{System.Net.WebUtility.HtmlEncode(displayName)}</strong> ({System.Net.WebUtility.HtmlEncode(company)})</p>",
                },
            };

            await masterGameAdministrationService.UpsertGameNewsEntryAsync(
                requesterEmail: GameConstants.SystemRequesterEmail,
                entryId: null,
                entryType: "CHANGELOG",
                status: "PUBLISHED",
                localizations: newsLocalizations,
                cancellationToken: httpContextAccessor.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish manual-end newsletter.");
        }

        var benchmarkRows = await db.RealWorldBillionaires
            .AsNoTracking()
            .OrderBy(item => item.Rank)
            .ThenByDescending(item => item.WealthUsd)
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);
        var winningThresholdUsd = benchmarkRows.FirstOrDefault()?.WealthUsd ?? EndgameCatalog.DefaultWinningThresholdUsd;

        return new EndgameStatusResult
        {
            GameEnded = true,
            WinnerPlayerId = winnerPlayerId,
            WinnerDisplayName = winnerDisplayName,
            WinnerCompanyName = winnerCompanyName,
            GameEndedAtUtc = endedAtUtc,
            WinningThresholdUsd = winningThresholdUsd,
            TopRealWorldRichest = benchmarkRows
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
