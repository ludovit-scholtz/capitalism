using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for the API key authentication path in the NPC bot:
/// BotAccount.IsTokenValid with API key sentinel, GameApiClient header selection,
/// and AccountService.RegisterOrLoginAsync short-circuit when ApiKey is set.
/// </summary>
public sealed class ApiKeyAuthNpcBotTests
{
    // ─── BotAccount.IsTokenValid with API key sentinel ───────────────────────

    [Fact]
    public void IsTokenValid_ApiKeySentinel_AlwaysReturnsTrue()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "ApiBot", Email = "a@b.c", Strategy = "Trading" };
        bot.Token = "APIKEY:some-very-long-random-key-abc123";
        bot.TokenExpiresAtUtc = null; // No expiry set — API keys don't expire.

        Assert.True(bot.IsTokenValid());
        Assert.True(bot.HasValidToken);
    }

    [Fact]
    public void IsTokenValid_ApiKeySentinel_StillTrueWhenExpiredTimeStampSet()
    {
        // Even if expiry was mistakenly set to the past, API key sentinel overrides.
        var bot = new BotAccount { Index = 1, DisplayName = "ApiBot", Email = "a@b.c", Strategy = "Trading" };
        bot.Token = "APIKEY:mykey";
        bot.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(-1);

        Assert.True(bot.IsTokenValid());
    }

    [Fact]
    public void IsTokenValid_NullToken_ReturnsFalse()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "Bot", Email = "a@b.c", Strategy = "S" };
        bot.Token = null;
        Assert.False(bot.IsTokenValid());
    }

    [Fact]
    public void IsTokenValid_ExpiredJwt_ReturnsFalse()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "Bot", Email = "a@b.c", Strategy = "S" };
        bot.Token = "eyJhbGciOiJIUzI1NiJ9.payload.sig";
        bot.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(-2); // expired

        Assert.False(bot.IsTokenValid());
    }

    [Fact]
    public void IsTokenValid_ValidJwt_ReturnsTrue()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "Bot", Email = "a@b.c", Strategy = "S" };
        bot.Token = "eyJhbGciOiJIUzI1NiJ9.payload.sig";
        bot.TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2); // not expired

        Assert.True(bot.IsTokenValid());
    }

    // ─── BotOptions.ApiKey property ──────────────────────────────────────────

    [Fact]
    public void BotOptions_ApiKey_DefaultIsNull()
    {
        var opts = new BotOptions();
        Assert.Null(opts.ApiKey);
    }

    [Fact]
    public void BotOptions_ApiKey_CanBeSet()
    {
        var opts = new BotOptions { ApiKey = "test-api-key-value" };
        Assert.Equal("test-api-key-value", opts.ApiKey);
    }

    // ─── AccountService.RegisterOrLoginAsync with ApiKey configured ──────────

    [Fact]
    public async Task RegisterOrLoginAsync_WithApiKey_ReturnsApiKeySentinelToken()
    {
        // Arrange: configure options with an API key.
        var opts = Microsoft.Extensions.Options.Options.Create(
            new BotOptions { ApiKey = "my-secret-api-key", BotPassword = "unused" });

        // Use a fake HTTP handler that should NOT be called when API key is set.
        var neverCallHandler = new ShouldNotBeCalledHttpHandler();
        var apiClient = new GameApiClient(
            new HttpClient(neverCallHandler),
            opts,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GameApiClient>.Instance);

        var service = new AccountService(
            apiClient, opts,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountService>.Instance);

        var bot = new BotAccount { Index = 1, DisplayName = "KeyBot", Email = "bot@x.com", Strategy = "Trade" };

        // Act
        var (token, expiry) = await service.RegisterOrLoginAsync(bot, CancellationToken.None);

        // Assert: token is the API key sentinel.
        Assert.Equal("APIKEY:my-secret-api-key", token);
        Assert.Equal(DateTime.MaxValue, expiry);
        Assert.False(neverCallHandler.WasCalled, "HTTP should NOT be called when API key is configured.");
    }

    [Fact]
    public async Task RegisterOrLoginAsync_WithoutApiKey_TriesHttpRegistration()
    {
        // Arrange: no API key — should try registration (and get an error from the stub).
        var opts = Microsoft.Extensions.Options.Options.Create(
            new BotOptions { ApiKey = null, BotPassword = "Pwd123!" });

        var failHandler = new AlwaysFailingHttpHandler();
        var apiClient = new GameApiClient(
            new HttpClient(failHandler),
            opts,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<GameApiClient>.Instance);

        var service = new AccountService(
            apiClient, opts,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AccountService>.Instance);

        var bot = new BotAccount { Index = 1, DisplayName = "PwdBot", Email = "bot@x.com", Strategy = "Trade" };

        // Act + Assert: should attempt HTTP and fail with the test handler's exception.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.RegisterOrLoginAsync(bot, CancellationToken.None));
        Assert.True(failHandler.WasCalled, "HTTP should be called when no API key is configured.");
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private sealed class ShouldNotBeCalledHttpHandler : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new InvalidOperationException("HTTP should not be called in API key mode.");
        }
    }

    private sealed class AlwaysFailingHttpHandler : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new HttpRequestException("Test: HTTP is disabled");
        }
    }
}
