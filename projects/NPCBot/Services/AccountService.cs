using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Handles NPC bot account registration, authentication, and state queries
/// against the Capitalism game API.
/// </summary>
public sealed class AccountService : IAccountService
{
    private const string MeQuery = """
        {
          me {
            id displayName email
            onboardingCompletedAtUtc
            onboardingCurrentStep onboardingIndustry
            onboardingCityId onboardingCompanyId
            onboardingFactoryLotId onboardingShopBuildingId
            companies { id name cash buildings { id name type cityId units { id unitType minPrice } } }
          }
        }
        """;

    private const string RegisterMutation = """
        mutation Register($input: RegisterInput!) {
          register(input: $input) {
            token expiresAtUtc
            player { id displayName email onboardingCompletedAtUtc }
          }
        }
        """;

    private const string LoginMutation = """
        mutation Login($input: LoginInput!) {
          login(input: $input) {
            token expiresAtUtc
            player { id displayName email onboardingCompletedAtUtc }
          }
        }
        """;

    private const string GameStateQuery = """
        { gameState { currentTick tickIntervalSeconds taxCycleTicks } }
        """;

    private const string RankingsQuery = """
        { rankings { rank displayName netWorth } }
        """;

    private const string UpdatePublicSalesPriceMutation = """
        mutation UpdatePublicSalesPrice($input: UpdatePublicSalesPriceInput!) {
          updatePublicSalesPrice(input: $input) { id unitType minPrice }
        }
        """;

    private readonly GameApiClient _api;
    private readonly BotOptions _options;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        GameApiClient api,
        IOptions<BotOptions> options,
        ILogger<AccountService> logger)
    {
        _api = api;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Registers a new NPC account. If the e-mail is already taken (DUPLICATE_EMAIL),
    /// attempts to log in with the stored password instead (idempotent behaviour).
    /// When <see cref="BotOptions.ApiKey"/> is configured, skips registration/login and
    /// returns a token sentinel that uses API key authentication on all subsequent requests.
    /// </summary>
    public async Task<(string token, DateTime expiresAt)> RegisterOrLoginAsync(
        BotAccount bot,
        CancellationToken ct)
    {
        // API key mode: skip password-based registration/login entirely.
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogInformation("{Bot} Using API key authentication — skipping registration/login.", bot);
            return ($"APIKEY:{_options.ApiKey}", DateTime.MaxValue);
        }

        try
        {
            _logger.LogInformation("{Bot} Registering new account…", bot);
            var result = await _api.ExecuteAsync<RegisterWrapper>(
                RegisterMutation,
                new { input = new { email = bot.Email, displayName = bot.DisplayName, password = _options.BotPassword } },
                ct: ct);

            _logger.LogInformation("{Bot} Registration succeeded.", bot);
            return (result.Register.Token, result.Register.ExpiresAtUtc);
        }
        catch (GraphQLException ex) when (ex.Code == "DUPLICATE_EMAIL")
        {
            _logger.LogInformation("{Bot} Account already exists — logging in.", bot);
            return await LoginAsync(bot, ct);
        }
    }

    /// <summary>Logs in with the bot's credentials and returns the new token.</summary>
    public async Task<(string token, DateTime expiresAt)> LoginAsync(
        BotAccount bot,
        CancellationToken ct)
    {
        var result = await _api.ExecuteAsync<LoginWrapper>(
            LoginMutation,
            new { input = new { email = bot.Email, password = _options.BotPassword } },
            ct: ct);

        _logger.LogInformation("{Bot} Login succeeded.", bot);
        return (result.Login.Token, result.Login.ExpiresAtUtc);
    }

    /// <summary>Fetches the full player profile for the authenticated bot.</summary>
    public async Task<PlayerProfile> FetchProfileAsync(string token, CancellationToken ct)
    {
        var result = await _api.ExecuteAsync<MeWrapper>(MeQuery, bearerToken: token, ct: ct);
        return result.Me;
    }

    /// <summary>Fetches the current game tick and configuration.</summary>
    public async Task<GameStateSummary> FetchGameStateAsync(CancellationToken ct)
    {
        var result = await _api.ExecuteAsync<GameStateWrapper>(GameStateQuery, ct: ct);
        return result.GameState;
    }

    /// <summary>Fetches the current global player rankings.</summary>
    public async Task<List<RankingEntry>> FetchRankingsAsync(CancellationToken ct)
    {
        var result = await _api.ExecuteAsync<RankingsWrapper>(RankingsQuery, ct: ct);
        return result.Rankings;
    }

    /// <summary>
    /// Updates the minimum sale price on a PUBLIC_SALES building unit.
    /// Takes effect from the next tick without requiring a queued upgrade plan.
    /// </summary>
    /// <param name="unitId">The ID of the PUBLIC_SALES unit to update.</param>
    /// <param name="newMinPrice">The new minimum sale price. Must be greater than zero.</param>
    /// <param name="token">Bearer token for the authenticated bot.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated unit summary.</returns>
    public async Task<UnitSummary> UpdatePublicSalesPriceAsync(
        string unitId,
        decimal newMinPrice,
        string token,
        CancellationToken ct)
    {
        var result = await _api.ExecuteAsync<UpdatePriceWrapper>(
            UpdatePublicSalesPriceMutation,
            new { input = new { unitId, newMinPrice } },
            bearerToken: token,
            ct: ct);

        return result.UpdatePublicSalesPrice;
    }

    // ── Wrapper types ─────────────────────────────────────────────────────────

    private sealed record RegisterWrapper(AuthPayload Register);
    private sealed record LoginWrapper(AuthPayload Login);
    private sealed record MeWrapper(PlayerProfile Me);
    private sealed record GameStateWrapper(GameStateSummary GameState);
    private sealed record RankingsWrapper(List<RankingEntry> Rankings);
    private sealed record UpdatePriceWrapper(UnitSummary UpdatePublicSalesPrice);
}
