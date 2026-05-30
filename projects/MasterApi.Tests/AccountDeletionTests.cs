using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MasterApi.Data;
using MasterApi.Tests.Infrastructure;
using MasterApi.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MasterApi.Tests;

public sealed class AccountDeletionTests
{
    [Fact]
    public async Task RequestAccountDeletion_WithMismatchedEmail_IsRejected()
    {
        var sender = new RecordingEmailSender();
        await using var factory = CreateFactory(sender, out var purge);
        using var client = factory.CreateClient();
        var (email, token) = await RegisterAsync(client, "Mismatch Tester");
        sender.Messages.Clear();

        var result = await RequestDeletionAsync(client, token, "someone-else@example.com");

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.Equal(
            "CONFIRMATION_EMAIL_MISMATCH",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
        Assert.Empty(sender.Messages);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var player = await db.PlayerAccounts.AsNoTracking().SingleAsync(p => p.Email == email);
        Assert.Null(player.DeletionRequestedAtUtc);
        Assert.Null(player.DeletionScheduledAtUtc);
    }

    [Fact]
    public async Task RequestAccountDeletion_MarksAccountAndSendsLocalizedEmail()
    {
        var sender = new RecordingEmailSender();
        await using var factory = CreateFactory(sender, out _);
        using var client = factory.CreateClient();
        var (email, token) = await RegisterAsync(client, "Delete Tester", locale: "sk");
        sender.Messages.Clear();

        var result = await RequestDeletionAsync(client, token, email.ToUpperInvariant());

        Assert.False(result.TryGetProperty("errors", out _));
        var payload = result.GetProperty("data").GetProperty("requestAccountDeletion");
        Assert.True(payload.GetProperty("isPendingDeletion").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("deletionScheduledAtUtc").GetString()));

        var sent = Assert.Single(sender.Messages);
        Assert.Equal(email, sent.RecipientEmail);
        Assert.Equal("Žiadosť o vymazanie účtu Capitalism", sent.Subject);
        Assert.Contains("capitalism.de-4.biatec.io", sent.HtmlBody);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var player = await db.PlayerAccounts.AsNoTracking().SingleAsync(p => p.Email == email);
        Assert.NotNull(player.DeletionRequestedAtUtc);
        Assert.NotNull(player.DeletionScheduledAtUtc);
        Assert.True(player.DeletionScheduledAtUtc > player.DeletionRequestedAtUtc);
        var cooldown = player.DeletionScheduledAtUtc!.Value - player.DeletionRequestedAtUtc!.Value;
        Assert.Equal(TimeSpan.FromHours(24), cooldown);
    }

    [Fact]
    public async Task MeQuery_ExposesPendingDeletion_AndCancelClearsIt()
    {
        var sender = new RecordingEmailSender();
        await using var factory = CreateFactory(sender, out _);
        using var client = factory.CreateClient();
        var (email, token) = await RegisterAsync(client, "Cancel Tester");

        await RequestDeletionAsync(client, token, email);

        var me = await GraphQlAsync(client, "query { me { isPendingDeletion deletionScheduledAtUtc } }", null, token);
        Assert.True(me.GetProperty("data").GetProperty("me").GetProperty("isPendingDeletion").GetBoolean());

        var cancel = await GraphQlAsync(
            client,
            "mutation { cancelAccountDeletion { isPendingDeletion } }",
            null,
            token);
        Assert.False(cancel.GetProperty("data").GetProperty("cancelAccountDeletion").GetProperty("isPendingDeletion").GetBoolean());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var player = await db.PlayerAccounts.AsNoTracking().SingleAsync(p => p.Email == email);
        Assert.Null(player.DeletionRequestedAtUtc);
        Assert.Null(player.DeletionScheduledAtUtc);
    }

    [Fact]
    public async Task RequestAccountDeletion_WithoutToken_IsUnauthorized()
    {
        var sender = new RecordingEmailSender();
        await using var factory = CreateFactory(sender, out _);
        using var client = factory.CreateClient();

        var result = await RequestDeletionAsync(client, null, "anyone@example.com");

        Assert.True(result.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task ProcessDueDeletions_PurgesGameData_SendsConfirmation_AndRemovesAccount()
    {
        var sender = new RecordingEmailSender();
        await using var factory = CreateFactory(sender, out var purge);
        using var client = factory.CreateClient();
        var (email, token) = await RegisterAsync(client, "Finalize Tester");
        await RequestDeletionAsync(client, token, email);
        sender.Messages.Clear();

        // Force the cooldown to have elapsed.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var player = await db.PlayerAccounts.SingleAsync(p => p.Email == email);
            player.DeletionScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        int deleted;
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
            deleted = await service.ProcessDueDeletionsAsync(CancellationToken.None);
        }

        Assert.Equal(1, deleted);
        Assert.Contains(email, purge.PurgedEmails);
        var confirmation = Assert.Single(sender.Messages);
        Assert.Equal(email, confirmation.RecipientEmail);
        Assert.Equal("Your Capitalism account has been deleted", confirmation.Subject);

        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<MasterDbContext>();
        Assert.False(await verifyDb.PlayerAccounts.AnyAsync(p => p.Email == email));
    }

    [Fact]
    public async Task ProcessDueDeletions_WhenPurgeFails_DefersDeletion()
    {
        var sender = new RecordingEmailSender();
        await using var factory = CreateFactory(sender, out var purge);
        using var client = factory.CreateClient();
        var (email, token) = await RegisterAsync(client, "Defer Tester");
        await RequestDeletionAsync(client, token, email);
        sender.Messages.Clear();
        purge.ThrowOnPurge = true;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var player = await db.PlayerAccounts.SingleAsync(p => p.Email == email);
            player.DeletionScheduledAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        int deleted;
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IAccountDeletionService>();
            deleted = await service.ProcessDueDeletionsAsync(CancellationToken.None);
        }

        Assert.Equal(0, deleted);
        Assert.Empty(sender.Messages);

        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<MasterDbContext>();
        Assert.True(await verifyDb.PlayerAccounts.AnyAsync(p => p.Email == email));
    }

    private static WebApplicationFactory<Program> CreateFactory(
        RecordingEmailSender sender,
        out FakeGameServerAccountPurgeService purge)
    {
        var purgeService = new FakeGameServerAccountPurgeService();
        purge = purgeService;
        return new MasterApiWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
                services.RemoveAll<IGameServerAccountPurgeService>();
                services.AddSingleton<IGameServerAccountPurgeService>(purgeService);
            });
        });
    }

    private static async Task<(string Email, string Token)> RegisterAsync(
        HttpClient client,
        string displayName,
        string locale = "en")
    {
        var email = $"delete-{Guid.NewGuid():N}@example.com";
        var register = await GraphQlAsync(client, """
            mutation Register($input: RegisterInput!) { register(input: $input) { token } }
            """,
            new { input = new { email, displayName, password = "TestPass123!", locale } });
        var token = register.GetProperty("data").GetProperty("register").GetProperty("token").GetString();
        return (email, token!);
    }

    private static Task<JsonElement> RequestDeletionAsync(HttpClient client, string? token, string confirmationEmail)
    {
        return GraphQlAsync(client, """
            mutation Request($input: RequestAccountDeletionInput!) {
              requestAccountDeletion(input: $input) {
                isPendingDeletion
                deletionRequestedAtUtc
                deletionScheduledAtUtc
              }
            }
            """,
            new { input = new { confirmationEmail } },
            token);
    }

    private static async Task<JsonElement> GraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json"),
        };
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessageRequest> Messages { get; } = [];

        public Task<bool> SendAsync(EmailMessageRequest request, CancellationToken cancellationToken)
        {
            Messages.Add(request);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeGameServerAccountPurgeService : IGameServerAccountPurgeService
    {
        public List<string> PurgedEmails { get; } = [];

        public bool ThrowOnPurge { get; set; }

        public Task PurgeAsync(string playerEmail, CancellationToken cancellationToken)
        {
            if (ThrowOnPurge)
            {
                throw new GameServerPurgeException("Simulated shard failure.");
            }

            PurgedEmails.Add(playerEmail);
            return Task.CompletedTask;
        }
    }
}
