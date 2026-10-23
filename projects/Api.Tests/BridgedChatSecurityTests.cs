using System.Text;
using System.Text.Json;
using Api.Security;
using Api.Tests.Infrastructure;

namespace Api.Tests;

/// <summary>
/// Hardening of the Discord bridge mutation (#658): both shared keys must match, and a leaked key must not be able to flood the
/// in-game chat past the same per-player limit a signed-in player has.
/// </summary>
public sealed class BridgedChatSecurityTests
{
    private const string RegistrationKey = "local-master-registration-key";
    private const string ServerKey = "capitalism-local";

    private const string BridgeMutation = """
        mutation Bridge($input: PostBridgedChatMessageInput!) {
          postBridgedChatMessage(input: $input) { delivered }
        }
        """;

    [Theory]
    [InlineData("wrong-registration-key", ServerKey, "INVALID_REGISTRATION_KEY")]
    [InlineData(RegistrationKey, "wrong-server-key", "INVALID_SERVER_KEY")]
    [InlineData("", ServerKey, "INVALID_REGISTRATION_KEY")]
    [InlineData(RegistrationKey, "", "INVALID_SERVER_KEY")]
    public async Task PostBridgedChatMessage_WrongKey_IsRejectedWithItsOwnCode(string registrationKey, string serverKey, string expectedCode)
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteAsync(client, registrationKey, serverKey, "someone@test.com", "hello");

        Assert.Equal(expectedCode, result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostBridgedChatMessage_CorrectKeys_DeliversTheMessage()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var email = await RegisterAsync(client);

        var result = await ExecuteAsync(client, $" {RegistrationKey} ", $" {ServerKey} ", email, "hello from discord");

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        Assert.True(result.GetProperty("data").GetProperty("postBridgedChatMessage").GetProperty("delivered").GetBoolean());
    }

    [Fact]
    public async Task PostBridgedChatMessage_OverTheChatRateLimit_IsRateLimited()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var email = await RegisterAsync(client);

        for (var i = 0; i < ChatRateLimitService.MaxMessagesPerWindow; i++)
        {
            var allowed = await ExecuteAsync(client, RegistrationKey, ServerKey, email, $"message {i}");
            Assert.False(allowed.TryGetProperty("errors", out _), allowed.ToString());
        }

        var limited = await ExecuteAsync(client, RegistrationKey, ServerKey, email, "one too many");

        Assert.Equal("RATE_LIMITED", limited.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PostBridgedChatMessage_RateLimitIsPerPlayer()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var floodingEmail = await RegisterAsync(client);
        var otherEmail = await RegisterAsync(client);

        for (var i = 0; i <= ChatRateLimitService.MaxMessagesPerWindow; i++)
        {
            await ExecuteAsync(client, RegistrationKey, ServerKey, floodingEmail, $"message {i}");
        }

        var other = await ExecuteAsync(client, RegistrationKey, ServerKey, otherEmail, "still fine");

        Assert.False(other.TryGetProperty("errors", out _), other.ToString());
    }

    private static async Task<JsonElement> ExecuteAsync(HttpClient client, string registrationKey, string serverKey, string email, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query = BridgeMutation,
                    variables = new { input = new { registrationKey, serverKey, playerEmail = email, content } },
                }),
                Encoding.UTF8,
                "application/json"),
        };

        var response = await client.SendAsync(request);
        return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
    }

    private static async Task<string> RegisterAsync(HttpClient client)
    {
        var email = $"bridge-{Guid.NewGuid():N}@test.com";
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    query = "mutation($input: RegisterInput!) { register(input: $input) { token } }",
                    variables = new { input = new { email, password = "Password1!", displayName = "Bridge Tester" } },
                }),
                Encoding.UTF8,
                "application/json"),
        };

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return email;
    }
}
