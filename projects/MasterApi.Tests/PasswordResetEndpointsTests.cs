using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MasterApi.Tests;

public sealed class PasswordResetEndpointsTests : IClassFixture<MasterApiWebApplicationFactory>
{
    private readonly MasterApiWebApplicationFactory _factory;

    public PasswordResetEndpointsTests(MasterApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ForgotPassword_ExistingAccount_ReturnsNeutralMessage_AndStoresHashedToken()
    {
        using var client = _factory.CreateClient();
        var email = $"player-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email, "Reset Me", "StartPass1!");

        var response = await client.PostAsJsonAsync("/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(payload);
        Assert.Equal("If an account exists, a reset link has been sent.", payload!.Message);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var token = await db.PasswordResetTokens
            .AsNoTracking()
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .FirstOrDefaultAsync(entry => entry.PlayerAccount!.Email == email);
        Assert.NotNull(token);
        Assert.NotEqual(string.Empty, token!.TokenHash);
        Assert.True(token.ExpiresAtUtc > token.CreatedAtUtc);
    }

    [Fact]
    public async Task ForgotPassword_MissingAccount_StillReturnsNeutralMessage()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/forgot-password", new { email = "unknown@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.NotNull(payload);
        Assert.Equal("If an account exists, a reset link has been sent.", payload!.Message);
    }

    [Fact]
    public async Task ForgotPassword_ExceedsLimit_Returns429()
    {
        using var client = _factory.CreateClient();
        var email = $"throttle-{Guid.NewGuid():N}@example.com";

        for (var i = 0; i < 3; i++)
        {
            var okResponse = await client.PostAsJsonAsync("/auth/forgot-password", new { email });
            Assert.Equal(HttpStatusCode.OK, okResponse.StatusCode);
        }

        var limitedResponse = await client.PostAsJsonAsync("/auth/forgot-password", new { email });

        Assert.Equal(HttpStatusCode.TooManyRequests, limitedResponse.StatusCode);
        var payload = await limitedResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("RATE_LIMIT_EXCEEDED", payload!.Code);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_Succeeds_AndTokenCannotBeReused()
    {
        await using var isolatedFactory = new MasterApiWebApplicationFactory();
        using var client = isolatedFactory.CreateClient();
        var email = $"replay-{Guid.NewGuid():N}@example.com";
        var oldPassword = "OldPass123!";
        var newPassword = "NewPass123!";
        await RegisterAsync(client, email, "Replay User", oldPassword);

        var rawToken = await CreateRawResetTokenAsync(isolatedFactory, email);

        var resetResponse = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = rawToken,
            newPassword,
        });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        var replayResponse = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = rawToken,
            newPassword = "AnotherPass1!",
        });
        Assert.Equal(HttpStatusCode.BadRequest, replayResponse.StatusCode);
        var replayPayload = await replayResponse.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(replayPayload);
        Assert.Equal("RESET_TOKEN_INVALID_OR_EXPIRED", replayPayload!.Code);

        var loginWithOldPassword = await LoginAsync(client, email, oldPassword);
        Assert.True(loginWithOldPassword.TryGetProperty("errors", out var oldErrors));
        Assert.Equal("INVALID_CREDENTIALS", oldErrors[0].GetProperty("extensions").GetProperty("code").GetString());

        var loginWithNewPassword = await LoginAsync(client, email, newPassword);
        Assert.False(loginWithNewPassword.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_ReturnsInvalidOrExpired()
    {
        await using var isolatedFactory = new MasterApiWebApplicationFactory();
        using var client = isolatedFactory.CreateClient();
        var email = $"expired-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email, "Expired User", "OldPass123!");

        var rawToken = await CreateRawResetTokenAsync(isolatedFactory, email, expiresAtUtc: DateTime.UtcNow.AddMinutes(-5));

        var response = await client.PostAsJsonAsync("/auth/reset-password", new
        {
            token = rawToken,
            newPassword = "NextPass123!",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("RESET_TOKEN_INVALID_OR_EXPIRED", payload!.Code);
    }

    [Fact]
    public async Task PasswordResetEndpoints_WhenPasswordAuthDisabled_ReturnMethodNotAllowed()
    {
        using var disabledFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:PasswordAuthEnabled"] = "false",
                });
            });
        });
        using var client = disabledFactory.CreateClient();

        var forgotResponse = await client.PostAsJsonAsync("/auth/forgot-password", new { email = "disabled@example.com" });
        var resetResponse = await client.PostAsJsonAsync("/auth/reset-password", new { token = "token", newPassword = "Password123!" });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, forgotResponse.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, resetResponse.StatusCode);
    }

    private static async Task RegisterAsync(HttpClient client, string email, string displayName, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query = """
                        mutation Register($input: RegisterInput!) {
                          register(input: $input) {
                            token
                          }
                        }
                        """,
                    variables = new
                    {
                        input = new
                        {
                            email,
                            displayName,
                            password,
                        },
                    },
                }),
                Encoding.UTF8,
                "application/json"),
        };

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<JsonElement> LoginAsync(HttpClient client, string email, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query = """
                        mutation Login($input: LoginInput!) {
                          login(input: $input) {
                            token
                          }
                        }
                        """,
                    variables = new
                    {
                        input = new
                        {
                            email,
                            password,
                        },
                    },
                }),
                Encoding.UTF8,
                "application/json"),
        };

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static async Task<string> CreateRawResetTokenAsync(
        MasterApiWebApplicationFactory factory,
        string email,
        DateTime? expiresAtUtc = null)
    {
        var rawToken = Guid.NewGuid().ToString("N");
        var hash = MasterApi.Utilities.PasswordResetService.ComputeTokenHash(rawToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var account = await db.PlayerAccounts.FirstAsync(player => player.Email == email);
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = account.Id,
            TokenHash = hash,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            ExpiresAtUtc = expiresAtUtc ?? DateTime.UtcNow.AddMinutes(30),
        });
        await db.SaveChangesAsync();
        return rawToken;
    }

    private sealed record MessageResponse(string Message);

    private sealed record ErrorResponse(string Message, string Code);
}
