using System.Text;
using System.Text.Json;
using Api.Security;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Tests for chat rate limiting and message length enforcement.
/// Covers: per-user sliding window, message-too-long, unauthenticated rejection,
/// and the ChatRateLimitService unit contract.
/// </summary>
public sealed class ChatRateLimitTests
{
    // ── Unit tests for ChatRateLimitService ─────────────────────────────────

    [Fact]
    public void ChatRateLimitService_AllowsMessagesUpToLimit()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new ChatRateLimitService(cache, NullLogger<ChatRateLimitService>.Instance);
        var playerId = Guid.NewGuid();

        // All 20 messages within the window should be allowed.
        for (var i = 0; i < ChatRateLimitService.MaxMessagesPerWindow; i++)
        {
            var (isAllowed, _) = svc.TryRecord(playerId);
            Assert.True(isAllowed, $"Message {i + 1} should be allowed (limit is {ChatRateLimitService.MaxMessagesPerWindow}).");
        }
    }

    [Fact]
    public void ChatRateLimitService_Rejects21stMessage()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new ChatRateLimitService(cache, NullLogger<ChatRateLimitService>.Instance);
        var playerId = Guid.NewGuid();

        // Consume the full allowance.
        for (var i = 0; i < ChatRateLimitService.MaxMessagesPerWindow; i++)
        {
            svc.TryRecord(playerId);
        }

        // The 21st attempt must be rejected.
        var (isAllowed, retryAfter) = svc.TryRecord(playerId);
        Assert.False(isAllowed, "21st message within the window should be rejected.");
        Assert.True(retryAfter > 0, "retryAfter should be positive when rate-limited.");
    }

    [Fact]
    public void ChatRateLimitService_19thMessagePasses_20thPasses_21stRejected()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new ChatRateLimitService(cache, NullLogger<ChatRateLimitService>.Instance);
        var playerId = Guid.NewGuid();

        // 1–18: allowed, no assertion needed for each
        for (var i = 0; i < 18; i++) svc.TryRecord(playerId);

        // 19th: allowed
        var (allow19, _) = svc.TryRecord(playerId);
        Assert.True(allow19, "19th message should be allowed.");

        // 20th: allowed
        var (allow20, _) = svc.TryRecord(playerId);
        Assert.True(allow20, "20th message should be allowed.");

        // 21st: rejected
        var (allow21, retry21) = svc.TryRecord(playerId);
        Assert.False(allow21, "21st message should be rejected.");
        Assert.True(retry21 > 0, "retryAfter must be positive.");
    }

    [Fact]
    public void ChatRateLimitService_DifferentPlayersHaveIndependentCounters()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new ChatRateLimitService(cache, NullLogger<ChatRateLimitService>.Instance);
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();

        // Exhaust playerA's limit.
        for (var i = 0; i <= ChatRateLimitService.MaxMessagesPerWindow; i++)
        {
            svc.TryRecord(playerA);
        }

        // playerB should still be allowed.
        var (isAllowed, _) = svc.TryRecord(playerB);
        Assert.True(isAllowed, "Different players must have independent rate-limit counters.");
    }

    [Fact]
    public void ChatRateLimitService_RetryAfterIsWithinWindowDuration()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new ChatRateLimitService(cache, NullLogger<ChatRateLimitService>.Instance);
        var playerId = Guid.NewGuid();

        // Exhaust the limit.
        for (var i = 0; i <= ChatRateLimitService.MaxMessagesPerWindow; i++)
        {
            svc.TryRecord(playerId);
        }

        var (_, retryAfter) = svc.TryRecord(playerId);
        Assert.True(retryAfter <= (int)ChatRateLimitService.WindowSize.TotalSeconds,
            "retryAfter should not exceed the window size.");
        Assert.True(retryAfter > 0, "retryAfter should be positive.");
    }

    // ── Integration tests ─────────────────────────────────────────────────────

    private static string SendChatMutation =>
        """
        mutation SendChatMessage($input: SendChatMessageInput!) {
          sendChatMessage(input: $input) {
            id
            playerDisplayName
            message
          }
        }
        """;

    private static async Task<JsonElement> PostGraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (token is not null)
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client)
    {
        var email = $"chat-rl-{Guid.NewGuid():N}@test.com";
        var result = await PostGraphQlAsync(client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Chat Tester", password = "TestPass123!" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task SendChatMessage_MessageTooLong_Returns_MessageTooLong()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client);

        // 501-character message.
        var longMessage = new string('A', 501);
        var result = await PostGraphQlAsync(client, SendChatMutation,
            new { input = new { message = longMessage } }, token);

        Assert.True(result.TryGetProperty("errors", out var errors), "Expected errors for too-long message.");
        Assert.Equal("MESSAGE_TOO_LONG",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SendChatMessage_ExactlyAtLimit_Succeeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client);

        // Exactly 500 characters — should succeed.
        var exactMessage = new string('B', 500);
        var result = await PostGraphQlAsync(client, SendChatMutation,
            new { input = new { message = exactMessage } }, token);

        Assert.False(result.TryGetProperty("errors", out _), "500-char message should be accepted.");
        Assert.Equal(exactMessage,
            result.GetProperty("data").GetProperty("sendChatMessage").GetProperty("message").GetString());
    }

    [Fact]
    public async Task SendChatMessage_MessageTooLong_IsNotPersisted()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client);

        var longMessage = new string('C', 501);
        await PostGraphQlAsync(client, SendChatMutation,
            new { input = new { message = longMessage } }, token);

        // Verify the message is absent from the chat feed.
        var feedResult = await PostGraphQlAsync(client,
            "query { chatMessages(limit: 50) { message } }", token: token);

        var messages = feedResult.GetProperty("data").GetProperty("chatMessages").EnumerateArray()
            .Select(m => m.GetProperty("message").GetString())
            .ToList();

        Assert.DoesNotContain(longMessage, messages);
    }

    [Fact]
    public async Task SendChatMessage_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // No token — should be rejected with auth error.
        var result = await PostGraphQlAsync(client, SendChatMutation,
            new { input = new { message = "Hello" } });

        Assert.True(result.TryGetProperty("errors", out _), "Unauthenticated send must fail.");
    }

    [Fact]
    public async Task SendChatMessage_20Messages_AllSucceed()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client);

        for (var i = 0; i < ChatRateLimitService.MaxMessagesPerWindow; i++)
        {
            var result = await PostGraphQlAsync(client, SendChatMutation,
                new { input = new { message = $"Message {i + 1}" } }, token);

            Assert.False(result.TryGetProperty("errors", out _),
                $"Message {i + 1} should succeed within the rate limit.");
        }
    }

    [Fact]
    public async Task SendChatMessage_21stMessage_WithinWindow_ReturnsRateLimited()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client);

        // Send 20 messages (the full allowance).
        for (var i = 0; i < ChatRateLimitService.MaxMessagesPerWindow; i++)
        {
            await PostGraphQlAsync(client, SendChatMutation,
                new { input = new { message = $"Message {i + 1}" } }, token);
        }

        // The 21st message must be rate-limited.
        var rateLimitedResult = await PostGraphQlAsync(client, SendChatMutation,
            new { input = new { message = "Exceeds limit" } }, token);

        Assert.True(rateLimitedResult.TryGetProperty("errors", out var errors),
            "21st message should return a rate-limit error.");
        var ext = errors[0].GetProperty("extensions");
        Assert.Equal("RATE_LIMITED", ext.GetProperty("code").GetString());
        Assert.True(ext.TryGetProperty("retryAfter", out var retryAfter),
            "RATE_LIMITED error must include retryAfter extension.");
        Assert.True(retryAfter.GetInt32() > 0, "retryAfter must be positive.");
    }
}
