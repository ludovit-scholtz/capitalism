using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Api.Tests;

/// <summary>
/// Integration tests for the FX quote-nonce lifecycle and slippage-guard enforcement
/// introduced by the Security Fairness Hardening initiative.
///
/// Each test uses an isolated <see cref="ApiWebApplicationFactory"/> so that concurrent
/// suite runs do not share quote-nonce state.
/// </summary>
public sealed class FxQuoteLifecycleTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { query, variables }),
            System.Text.Encoding.UTF8, "application/json");
        if (token != null)
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string> RegisterAndLoginAsync(HttpClient client, string email)
    {
        await ExecuteGraphQlAsync(client,
            """
            mutation Register($input: RegisterInput!) {
                register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName = "FxTestUser", password = "TestPass123!" } });

        var loginResult = await ExecuteGraphQlAsync(client,
            """
            mutation Login($input: LoginInput!) {
                login(input: $input) { token }
            }
            """,
            new { input = new { email, password = "TestPass123!" } });

        return loginResult.GetProperty("data").GetProperty("login").GetProperty("token").GetString()!;
    }

    /// <summary>
    /// Ensures the player has a personal settlement bank account with sufficient funds.
    /// </summary>
    private static async Task SetPersonalSettlementBalanceAsync(AppDbContext db, Guid playerId, decimal amount)
    {
        var player = await db.Players.FirstAsync(p => p.Id == playerId);
        // Fund both the USD settlement account and an EUR tracked account so EUR→CZK swaps can proceed.
        await PersonalBankAccountService.SetTrackedGrossCashAsync(db, player, amount);
        var eurAccount = await PersonalBankAccountService.EnsureTrackedAccountAsync(db, playerId, "EUR");
        eurAccount.Balance = amount;
        await db.SaveChangesAsync();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForexQuote_ReturnsQuoteNonceAndTimestamp()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, $"quote-fields-{Guid.NewGuid():N}@example.com");

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(meResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPersonalSettlementBalanceAsync(db, playerId, 500m);

        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($input: GetForexQuoteInput!) {
                forexQuote(input: $input) {
                    quoteNonce quotedAtUtc quoteExpiresInSeconds rate fromAmount toAmount
                }
            }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m } },
            token);

        var quote = result.GetProperty("data").GetProperty("forexQuote");
        var nonceStr = quote.GetProperty("quoteNonce").GetString();
        Assert.False(string.IsNullOrEmpty(nonceStr));
        Assert.True(Guid.TryParse(nonceStr, out _), "quoteNonce must be a valid UUID");
        Assert.False(string.IsNullOrEmpty(quote.GetProperty("quotedAtUtc").GetString()));
        Assert.Equal(30, quote.GetProperty("quoteExpiresInSeconds").GetInt32());
        Assert.True(quote.GetProperty("rate").GetDecimal() > 0);
    }

    [Fact]
    public async Task GetForexQuote_EurToPln_ReturnsQuote()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, $"quote-pln-{Guid.NewGuid():N}@example.com");

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(meResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPersonalSettlementBalanceAsync(db, playerId, 500m);

        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($input: GetForexQuoteInput!) {
                forexQuote(input: $input) {
                    quoteNonce toCurrencyCode rate toAmount
                }
            }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "PLN", amount = 100m } },
            token);

        Assert.False(result.TryGetProperty("errors", out _), "PLN forex quote should succeed.");

        var quote = result.GetProperty("data").GetProperty("forexQuote");
        Assert.False(string.IsNullOrEmpty(quote.GetProperty("quoteNonce").GetString()));
        Assert.Equal("PLN", quote.GetProperty("toCurrencyCode").GetString());
        Assert.True(quote.GetProperty("rate").GetDecimal() > 0m);
        Assert.True(quote.GetProperty("toAmount").GetDecimal() > 0m);
    }

    [Fact]
    public async Task ExecuteForexSwap_WithValidQuoteNonce_Succeeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, $"nonce-valid-{Guid.NewGuid():N}@example.com");

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(meResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPersonalSettlementBalanceAsync(db, playerId, 1000m);

        // Obtain a quote (generates a nonce server-side).
        var quoteResult = await ExecuteGraphQlAsync(client,
            """
            query Q($input: GetForexQuoteInput!) {
                forexQuote(input: $input) { quoteNonce rate }
            }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 50m } },
            token);
        var nonce = quoteResult.GetProperty("data").GetProperty("forexQuote").GetProperty("quoteNonce").GetString();
        Assert.False(string.IsNullOrEmpty(nonce));

        // Execute the swap using the returned nonce — must succeed.
        var swapResult = await ExecuteGraphQlAsync(client,
            """
            mutation M($input: ExecuteForexSwapInput!) {
                executeForexSwap(input: $input) { tradeId fromCurrencyCode toAmount }
            }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 50m, quoteNonce = nonce } },
            token);

        var trade = swapResult.GetProperty("data").GetProperty("executeForexSwap");
        Assert.Equal("EUR", trade.GetProperty("fromCurrencyCode").GetString());
        Assert.True(trade.GetProperty("toAmount").GetDecimal() > 0);
    }

    [Fact]
    public async Task ExecuteForexSwap_ReplayAttack_SecondCallReturnsQuoteAlreadyUsed()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, $"nonce-replay-{Guid.NewGuid():N}@example.com");

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(meResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPersonalSettlementBalanceAsync(db, playerId, 2000m);

        // Obtain a quote.
        var quoteResult = await ExecuteGraphQlAsync(client,
            """
            query Q($input: GetForexQuoteInput!) { forexQuote(input: $input) { quoteNonce } }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m } },
            token);
        var nonce = quoteResult.GetProperty("data").GetProperty("forexQuote").GetProperty("quoteNonce").GetString();

        // First execution — should succeed.
        var first = await ExecuteGraphQlAsync(client,
            """
            mutation M($input: ExecuteForexSwapInput!) { executeForexSwap(input: $input) { tradeId } }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m, quoteNonce = nonce } },
            token);
        Assert.True(first.GetProperty("data").TryGetProperty("executeForexSwap", out _));

        // Second execution with the same nonce — must be rejected with QUOTE_ALREADY_USED.
        var second = await ExecuteGraphQlAsync(client,
            """
            mutation M($input: ExecuteForexSwapInput!) { executeForexSwap(input: $input) { tradeId } }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m, quoteNonce = nonce } },
            token);
        var errors = second.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.Equal("QUOTE_ALREADY_USED",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteForexSwap_StaleQuote_ReturnsQuoteExpired()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, $"nonce-stale-{Guid.NewGuid():N}@example.com");

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(meResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPersonalSettlementBalanceAsync(db, playerId, 500m);

        // Manually insert an expired nonce (issued QuoteTtlSeconds + 1 seconds ago).
        var expiredNonce = Guid.NewGuid();
        db.FxQuoteNonces.Add(new FxQuoteNonce
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Nonce = expiredNonce,
            FromCurrencyCode = "EUR",
            ToCurrencyCode = "CZK",
            Rate = 25m,
            IssuedAtUtc = DateTime.UtcNow.AddSeconds(-(Api.Types.Query.QuoteTtlSeconds + 1)),
        });
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation M($input: ExecuteForexSwapInput!) { executeForexSwap(input: $input) { tradeId } }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m, quoteNonce = expiredNonce } },
            token);

        var errors = result.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.Equal("QUOTE_EXPIRED",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteForexSwap_UnknownNonce_ReturnsQuoteNonceNotFound()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, $"nonce-unknown-{Guid.NewGuid():N}@example.com");

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(meResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPersonalSettlementBalanceAsync(db, playerId, 500m);

        var fakeNonce = Guid.NewGuid();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation M($input: ExecuteForexSwapInput!) { executeForexSwap(input: $input) { tradeId } }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m, quoteNonce = fakeNonce } },
            token);

        var errors = result.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.Equal("QUOTE_NONCE_NOT_FOUND",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteForexSwap_SlippageExceeded_ReturnsSlippageExceeded()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, $"slippage-exc-{Guid.NewGuid():N}@example.com");

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(meResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPersonalSettlementBalanceAsync(db, playerId, 500m);

        // Insert a nonce whose quoted rate is absurdly different from the live rate (~25 CZK/EUR).
        // Using 0.0001 causes deviation far in excess of 50 BPS, guaranteeing SLIPPAGE_EXCEEDED.
        var nonce = Guid.NewGuid();
        db.FxQuoteNonces.Add(new FxQuoteNonce
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Nonce = nonce,
            FromCurrencyCode = "EUR",
            ToCurrencyCode = "CZK",
            Rate = 0.0001m,
            IssuedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // acceptedSlippageBps = 50 (0.5 %); actual deviation > 100 % → must be rejected.
        var result = await ExecuteGraphQlAsync(client,
            """
            mutation M($input: ExecuteForexSwapInput!) { executeForexSwap(input: $input) { tradeId } }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m, quoteNonce = nonce, acceptedSlippageBps = 50 } },
            token);

        var errors = result.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.Equal("SLIPPAGE_EXCEEDED",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ExecuteForexSwap_FreshQuoteWithinSlippage_Succeeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, $"slippage-ok-{Guid.NewGuid():N}@example.com");

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(meResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPersonalSettlementBalanceAsync(db, playerId, 500m);

        // Obtain a real quote from the server — the stored nonce rate will exactly match the
        // live rate, so any non-zero slippage tolerance must accept the trade.
        var quoteResult = await ExecuteGraphQlAsync(client,
            """
            query Q($input: GetForexQuoteInput!) {
                forexQuote(input: $input) { quoteNonce rate }
            }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m } },
            token);
        var nonce = quoteResult.GetProperty("data").GetProperty("forexQuote").GetProperty("quoteNonce").GetString();

        // 10 000 BPS (100 %) tolerance — should always succeed when rate is identical.
        var swapResult = await ExecuteGraphQlAsync(client,
            """
            mutation M($input: ExecuteForexSwapInput!) {
                executeForexSwap(input: $input) { tradeId toAmount }
            }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m, quoteNonce = nonce, acceptedSlippageBps = 10000 } },
            token);

        var trade = swapResult.GetProperty("data").GetProperty("executeForexSwap");
        Assert.True(trade.GetProperty("toAmount").GetDecimal() > 0);
    }

    [Fact]
    public async Task FxSecurityAuditLog_IsCreated_WhenNonceRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndLoginAsync(client, $"audit-log-{Guid.NewGuid():N}@example.com");

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(meResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SetPersonalSettlementBalanceAsync(db, playerId, 500m);

        var fakeNonce = Guid.NewGuid();

        // Unknown nonce → QUOTE_NONCE_NOT_FOUND rejection.
        await ExecuteGraphQlAsync(client,
            """
            mutation M($input: ExecuteForexSwapInput!) { executeForexSwap(input: $input) { tradeId } }
            """,
            new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m, quoteNonce = fakeNonce } },
            token);

        // A FxSecurityAuditLog row must have been written for this rejection.
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await verifyDb.FxSecurityAuditLogs
            .Where(l => l.PlayerId == playerId && l.RejectionReason == "QUOTE_NONCE_NOT_FOUND")
            .FirstOrDefaultAsync();

        Assert.NotNull(log);
        Assert.Equal(playerId, log.PlayerId);
        Assert.False(string.IsNullOrEmpty(log.NonceHash));
        Assert.True(log.OccurredAtUtc > DateTime.UtcNow.AddMinutes(-1));
    }
}
