using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Capitalism.Shared.Security;
using MasterApi.Tests.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace MasterApi.Tests;

public sealed class SupportTicketIntegrationTests : IClassFixture<MasterApiWebApplicationFactory>
{
    private const string SharedJwtIssuer = "Capitalism";
    private const string SharedJwtAudience = "Capitalism";
    private const string SharedJwtSigningKey = "ChangeThisSigningKeyBeforeProduction123!";

    private readonly HttpClient _client;

    public SupportTicketIntegrationTests(MasterApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string CreateRootAdminToken()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SharedJwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: SharedJwtIssuer,
            audience: SharedJwtAudience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, "root@example.com"),
                new Claim(ClaimTypes.Name, "Root Admin"),
                new Claim(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeMaster),
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<JsonElement> GraphQlAsync(
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

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private async Task<(string Token, string PlayerId, string Email)> RegisterAsync(string email)
    {
                var registerResult = await GraphQlAsync(_client, """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
                player { id email }
              }
            }
            """, new
        {
            input = new
            {
                email,
                displayName = email.Split('@')[0],
                password = "password123",
            }
        });

        if (registerResult.TryGetProperty("errors", out _))
        {
            var loginResult = await GraphQlAsync(_client, """
                mutation Login($input: LoginInput!) {
                  login(input: $input) {
                    token
                    player { id email }
                  }
                }
                """, new
            {
                input = new
                {
                    email,
                    password = "password123",
                }
            });

            var loginPayload = loginResult.GetProperty("data").GetProperty("login");
            return (
                loginPayload.GetProperty("token").GetString()!,
                loginPayload.GetProperty("player").GetProperty("id").GetString()!,
                loginPayload.GetProperty("player").GetProperty("email").GetString()!);
        }

        var payload = registerResult.GetProperty("data").GetProperty("register");
        return (
            payload.GetProperty("token").GetString()!,
            payload.GetProperty("player").GetProperty("id").GetString()!,
            payload.GetProperty("player").GetProperty("email").GetString()!);
    }

    [Fact]
    public async Task CreateSupportTicket_InvalidType_ReturnsErrorCode()
    {
        var (token, _, _) = await RegisterAsync($"support-invalid-{Guid.NewGuid():N}@example.com");

        var result = await GraphQlAsync(_client, """
            mutation Create($input: CreateSupportTicketInput!) {
              createSupportTicket(input: $input) { id }
            }
            """, new
        {
            input = new
            {
                ticketType = "NOT_A_TYPE",
                title = "Broken support form",
                markdownSource = "This support ticket content is sufficiently long for validation.",
            }
        }, token);

        var errors = result.GetProperty("errors");
        Assert.Equal("INVALID_SUPPORT_TICKET_TYPE", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task MySupportTickets_ReturnsOnlyAuthenticatedUsersTickets()
    {
        var (aliceToken, _, _) = await RegisterAsync($"support-alice-{Guid.NewGuid():N}@example.com");
        var (bobToken, _, _) = await RegisterAsync($"support-bob-{Guid.NewGuid():N}@example.com");

        await GraphQlAsync(_client, """
            mutation Create($input: CreateSupportTicketInput!) {
              createSupportTicket(input: $input) { id }
            }
            """, new
        {
            input = new
            {
                ticketType = "BUG",
                title = "Alice ticket",
                markdownSource = "Alice markdown content is intentionally long to pass the validation checks.",
            }
        }, aliceToken);

        await GraphQlAsync(_client, """
            mutation Create($input: CreateSupportTicketInput!) {
              createSupportTicket(input: $input) { id }
            }
            """, new
        {
            input = new
            {
                ticketType = "SUGGESTION",
                title = "Bob ticket",
                markdownSource = "Bob markdown content is intentionally long to pass the validation checks.",
            }
        }, bobToken);

        var aliceResult = await GraphQlAsync(_client, """
            query {
              mySupportTickets {
                title
                createdByEmail
              }
            }
            """, token: aliceToken);

        var items = aliceResult.GetProperty("data").GetProperty("mySupportTickets");
        Assert.Single(items.EnumerateArray());
        Assert.Equal("Alice ticket", items[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task SupportTicketStatusTransition_InvalidTransition_ReturnsErrorCode()
    {
        var (ownerToken, _, _) = await RegisterAsync($"support-owner-{Guid.NewGuid():N}@example.com");
        var adminToken = CreateRootAdminToken();

        var createResult = await GraphQlAsync(_client, """
            mutation Create($input: CreateSupportTicketInput!) {
              createSupportTicket(input: $input) { id }
            }
            """, new
        {
            input = new
            {
                ticketType = "OTHER",
                title = "Cannot move status backwards",
                markdownSource = "This ticket verifies status lifecycle transitions with valid markdown body.",
            }
        }, ownerToken);

        var ticketId = createResult.GetProperty("data").GetProperty("createSupportTicket").GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(ticketId));

        await GraphQlAsync(_client, """
            mutation Update($input: UpdateSupportTicketStatusInput!) {
              updateSupportTicketStatus(input: $input) { status }
            }
            """, new
        {
            input = new
            {
                ticketId,
                status = "IN_PROGRESS",
                note = "Investigating",
            }
        }, adminToken);

        var invalidResult = await GraphQlAsync(_client, """
            mutation Update($input: UpdateSupportTicketStatusInput!) {
              updateSupportTicketStatus(input: $input) { status }
            }
            """, new
        {
            input = new
            {
                ticketId,
                status = "SUBMITTED",
                note = "Rollback",
            }
        }, adminToken);

        var errors = invalidResult.GetProperty("errors");
        Assert.Equal(
            "INVALID_SUPPORT_TICKET_STATUS_TRANSITION",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SupportTicketPreview_IsModerationGated_AndApprovedPreviewIsSanitized()
    {
        var (ownerToken, _, _) = await RegisterAsync($"support-moderation-{Guid.NewGuid():N}@example.com");
        var adminToken = CreateRootAdminToken();

        var createResult = await GraphQlAsync(_client, """
            mutation Create($input: CreateSupportTicketInput!) {
              createSupportTicket(input: $input) {
                id
                moderationState
                sanitizedPreviewHtml
                containsUnsafeContent
              }
            }
            """, new
        {
            input = new
            {
                ticketType = "BUG",
                title = "Unsafe markdown sample",
                markdownSource = "<script>alert(1)</script> Click [here](javascript:alert(2)) and ![img](http://example.com/a.png)",
            }
        }, ownerToken);

        Assert.False(
            createResult.TryGetProperty("errors", out var createErrors),
            createResult.TryGetProperty("errors", out _)
                ? createErrors.ToString()
                : "Create support ticket returned null data.");

        var created = createResult.GetProperty("data").GetProperty("createSupportTicket");
        var ticketId = created.GetProperty("id").GetString();
        Assert.Equal("PENDING", created.GetProperty("moderationState").GetString());
        Assert.Null(created.GetProperty("sanitizedPreviewHtml").GetString());
        Assert.True(created.GetProperty("containsUnsafeContent").GetBoolean());

        var approvedResult = await GraphQlAsync(_client, """
            mutation Moderate($input: ModerateSupportTicketInput!) {
              moderateSupportTicket(input: $input) {
                moderationState
                sanitizedPreviewHtml
                extractedUrls
                extractedImages
              }
            }
            """, new
        {
            input = new
            {
                ticketId,
                approve = true,
                note = "Reviewed and approved",
            }
        }, adminToken);

        Assert.False(
            approvedResult.TryGetProperty("errors", out var approvedErrors),
            approvedResult.TryGetProperty("errors", out _)
                ? approvedErrors.ToString()
                : "Moderation mutation returned null data.");

        var approved = approvedResult.GetProperty("data").GetProperty("moderateSupportTicket");
        Assert.Equal("APPROVED", approved.GetProperty("moderationState").GetString());
        var previewHtml = approved.GetProperty("sanitizedPreviewHtml").GetString();
        Assert.NotNull(previewHtml);
        Assert.DoesNotContain("<script", previewHtml!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("http://example.com/a.png", approved.GetProperty("extractedImages")[0].GetString());
    }

    [Fact]
    public async Task SupportTicketsAdmin_RequiresGlobalAdminAccess()
    {
        var (token, _, _) = await RegisterAsync($"support-user-{Guid.NewGuid():N}@example.com");

        var result = await GraphQlAsync(_client, """
            query {
              supportTicketsAdmin {
                id
              }
            }
            """, token: token);

        var errors = result.GetProperty("errors");
        Assert.Equal("GLOBAL_ADMIN_REQUIRED", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }
}
