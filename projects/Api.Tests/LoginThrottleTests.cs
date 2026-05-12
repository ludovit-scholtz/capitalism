using System.Text;
using System.Text.Json;
using Api.Configuration;
using Api.Security;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests;

/// <summary>
/// Integration tests covering password-auth abuse controls:
/// login throttling, counter-reset on success, duplicate-email normalization.
/// </summary>
public sealed class LoginThrottleTests
{
    // ── Unit tests for LoginThrottleService ─────────────────────────────────

    [Fact]
    public void LoginThrottleService_CounterIncrements_UnderThreshold()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AuthOptions { PasswordAuthEnabled = true, MaxFailedLoginAttempts = 5, LockoutWindowMinutes = 15 });
        var svc = new LoginThrottleService(cache, options, NullLogger<LoginThrottleService>.Instance);

        var email = "test@example.com";
        Assert.False(svc.IsThrottled(email));

        for (var i = 0; i < 4; i++)
        {
            var locked = svc.RecordFailure(email);
            Assert.False(locked, $"Should not lock on attempt {i + 1}");
            Assert.False(svc.IsThrottled(email));
        }
    }

    [Fact]
    public void LoginThrottleService_LocksOnThresholdHit()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AuthOptions { PasswordAuthEnabled = true, MaxFailedLoginAttempts = 5, LockoutWindowMinutes = 15 });
        var svc = new LoginThrottleService(cache, options, NullLogger<LoginThrottleService>.Instance);

        var email = "locked@example.com";

        for (var i = 0; i < 4; i++) svc.RecordFailure(email);
        Assert.False(svc.IsThrottled(email), "Should not be throttled before threshold");

        var triggered = svc.RecordFailure(email);
        Assert.True(triggered, "5th failure should trigger lockout");
        Assert.True(svc.IsThrottled(email), "Account should be throttled after lockout triggered");
    }

    [Fact]
    public void LoginThrottleService_SuccessResetsCounter()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AuthOptions { PasswordAuthEnabled = true, MaxFailedLoginAttempts = 5, LockoutWindowMinutes = 15 });
        var svc = new LoginThrottleService(cache, options, NullLogger<LoginThrottleService>.Instance);

        var email = "reset@example.com";

        // Accumulate 4 failures
        for (var i = 0; i < 4; i++) svc.RecordFailure(email);
        Assert.False(svc.IsThrottled(email));

        // Successful login clears the counter
        svc.RecordSuccess(email);

        // The next 4 failures should not trigger lockout (fresh counter)
        for (var i = 0; i < 4; i++) svc.RecordFailure(email);
        Assert.False(svc.IsThrottled(email), "Counter should have been reset");
    }

    [Fact]
    public void LoginThrottleService_ThrottledAfterLockoutClearedBySuccess()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new AuthOptions { PasswordAuthEnabled = true, MaxFailedLoginAttempts = 3, LockoutWindowMinutes = 15 });
        var svc = new LoginThrottleService(cache, options, NullLogger<LoginThrottleService>.Instance);

        var email = "cycle@example.com";

        // Trigger lockout
        for (var i = 0; i < 3; i++) svc.RecordFailure(email);
        Assert.True(svc.IsThrottled(email));

        // RecordSuccess clears both the counter and the lockout
        svc.RecordSuccess(email);
        Assert.False(svc.IsThrottled(email), "Success should clear the lockout");

        // Must be able to accumulate failures again from scratch
        for (var i = 0; i < 2; i++) svc.RecordFailure(email);
        Assert.False(svc.IsThrottled(email), "Should need full threshold to re-lock");
    }

    // ── Integration tests using an isolated factory (InMemory DB) ────────────

    private static async Task<JsonElement> PostGraphQlAsync(HttpClient client, string query, object? variables = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    [Fact]
    public async Task Login_After5FailedAttempts_ReturnsLoginThrottled()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Register the account first.
        const string email = "throttle-test@example.com";
        const string password = "CorrectPass1!";
        await PostGraphQlAsync(client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "ThrottleUser", password } });

        // Send 5 failed login attempts with the wrong password.
        for (var i = 0; i < 5; i++)
        {
            var r = await PostGraphQlAsync(client,
                "mutation Login($input: LoginInput!) { login(input: $input) { token } }",
                new { input = new { email, password = "WrongPass!" } });

            Assert.True(r.TryGetProperty("errors", out _), $"Attempt {i + 1} should fail");
        }

        // The 6th attempt should return LOGIN_THROTTLED even with the correct password.
        var throttledResult = await PostGraphQlAsync(client,
            "mutation Login($input: LoginInput!) { login(input: $input) { token } }",
            new { input = new { email, password } });

        Assert.True(throttledResult.TryGetProperty("errors", out var errors), "6th attempt must fail");
        Assert.Equal("LOGIN_THROTTLED", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsFailureCounter()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        const string email = "counter-reset@example.com";
        const string password = "CorrectPass1!";
        await PostGraphQlAsync(client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "ResetUser", password } });

        // 4 failures — just under the 5-attempt lockout threshold.
        for (var i = 0; i < 4; i++)
        {
            await PostGraphQlAsync(client,
                "mutation Login($input: LoginInput!) { login(input: $input) { token } }",
                new { input = new { email, password = "WrongPass!" } });
        }

        // Successful login should reset the counter.
        var successResult = await PostGraphQlAsync(client,
            "mutation Login($input: LoginInput!) { login(input: $input) { token } }",
            new { input = new { email, password } });
        var token = successResult.GetProperty("data").GetProperty("login").GetProperty("token").GetString();
        Assert.NotEmpty(token!);

        // After successful login, a fresh run of 4 failures should still NOT trigger lockout.
        for (var i = 0; i < 4; i++)
        {
            await PostGraphQlAsync(client,
                "mutation Login($input: LoginInput!) { login(input: $input) { token } }",
                new { input = new { email, password = "WrongPass!" } });
        }

        // One more correct login should still succeed (counter was reset again by the previous success).
        var secondSuccess = await PostGraphQlAsync(client,
            "mutation Login($input: LoginInput!) { login(input: $input) { token } }",
            new { input = new { email, password } });
        Assert.False(secondSuccess.TryGetProperty("errors", out _), "Login should succeed after counter reset");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsNeutralMessage()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        const string email = "neutral-message@example.com";
        const string password = "Pass123!";
        await PostGraphQlAsync(client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "FirstUser", password } });

        var result = await PostGraphQlAsync(client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "SecondUser", password } });

        Assert.True(result.TryGetProperty("errors", out var errors), "Duplicate registration must fail");
        var message = errors[0].GetProperty("message").GetString()!;
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();

        Assert.Equal("DUPLICATE_EMAIL", code);
        // Message should be neutral — must NOT say "A player with this email already exists" (the old message
        // that confirmed account existence). The new message uses intentionally ambiguous phrasing.
        Assert.DoesNotContain("A player with", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("may already exist", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_UnknownEmail_DoesNotRevealAccountExistence()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Login with an email that was never registered.
        var result = await PostGraphQlAsync(client,
            "mutation Login($input: LoginInput!) { login(input: $input) { token } }",
            new { input = new { email = "nonexistent-throttle@example.com", password = "AnyPassword1!" } });

        Assert.True(result.TryGetProperty("errors", out var errors));
        // Must return generic INVALID_CREDENTIALS, not a message confirming the email is unknown.
        Assert.Equal("INVALID_CREDENTIALS", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }
}
