using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Tests.Infrastructure;
using MasterApi.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MasterApi.Tests;

public sealed class EmailSupportTests
{
    [Fact]
    public async Task Register_SendsLocalizedRegistrationEmailOnce_AndStoresAccessUrl()
    {
        var fakeSender = new RecordingEmailSender();
        await using var factory = CreateFactory(fakeSender);
        using var client = factory.CreateClient();
        var email = $"email-{Guid.NewGuid():N}@example.com";

        var result = await GraphQlAsync(client, """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                player { email preferredLocale }
              }
            }
            """,
            new
            {
                input = new
                {
                    email,
                    displayName = "Email Tester",
                    password = "TestPass123!",
                    locale = "sk-SK",
                    currentUrl = "https://capitalism.example.com/register?server=one",
                },
            });

        Assert.False(result.TryGetProperty("errors", out _));
        Assert.Equal("sk", result.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("preferredLocale").GetString());
        var sent = Assert.Single(fakeSender.Messages);
        Assert.Equal(email, sent.RecipientEmail);
        Assert.Equal("Vitajte v Capitalism", sent.Subject);
        Assert.Contains("https://capitalism.example.com/register?server=one", sent.HtmlBody);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var player = await db.PlayerAccounts.AsNoTracking().SingleAsync(item => item.Email == email);
        Assert.True(player.HasReceivedRegistrationEmail);
        Assert.Equal("sk", player.PreferredLocale);
        Assert.Equal("https://capitalism.example.com/register?server=one", player.LastAccessedUrl);
    }

    [Fact]
    public async Task Login_StoresLanguagePreference()
    {
        var fakeSender = new RecordingEmailSender();
        await using var factory = CreateFactory(fakeSender);
        using var client = factory.CreateClient();
        var email = $"login-locale-{Guid.NewGuid():N}@example.com";
        await GraphQlAsync(client, """
            mutation Register($input: RegisterInput!) { register(input: $input) { token } }
            """,
            new { input = new { email, displayName = "Locale Tester", password = "TestPass123!", locale = "en" } });

        var login = await GraphQlAsync(client, """
            mutation Login($input: LoginInput!) {
              login(input: $input) { player { email preferredLocale } }
            }
            """,
            new
            {
                input = new
                {
                    email,
                    password = "TestPass123!",
                    locale = "de-DE",
                    currentUrl = "https://capitalism.example.com/login",
                },
            });

        Assert.False(login.TryGetProperty("errors", out _));
        Assert.Equal("de", login.GetProperty("data").GetProperty("login").GetProperty("player").GetProperty("preferredLocale").GetString());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var player = await db.PlayerAccounts.AsNoTracking().SingleAsync(item => item.Email == email);
        Assert.Equal("de", player.PreferredLocale);
        Assert.Equal("https://capitalism.example.com/login", player.LastAccessedUrl);
    }

    [Fact]
    public async Task WeeklyReport_IncludesActiveServersBountiesAndChangelog()
    {
        var fakeSender = new RecordingEmailSender();
        await using var factory = CreateFactory(fakeSender, enableWeeklyReports: true);
        using var client = factory.CreateClient();
        var email = $"weekly-{Guid.NewGuid():N}@example.com";
        await GraphQlAsync(client, """
            mutation Register($input: RegisterInput!) { register(input: $input) { token } }
            """,
            new { input = new { email, displayName = "Weekly Tester", password = "TestPass123!", locale = "de" } });
        fakeSender.Messages.Clear();

        var now = new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);
        await SeedWeeklyReportDataAsync(factory, email, now);

        using (var scope = factory.Services.CreateScope())
        {
            var reports = scope.ServiceProvider.GetRequiredService<IWeeklyEmailReportService>();
            var sent = await reports.SendDueWeeklyReportsAsync(now, CancellationToken.None);
            Assert.Equal(1, sent);
        }

        var message = Assert.Single(fakeSender.Messages);
        Assert.Equal("Wöchentlicher Capitalism-Bericht", message.Subject);
        Assert.Contains("Central Europe", message.HtmlBody);
        Assert.Contains("Bounties: 42", message.HtmlBody);
        Assert.Contains("Market expansion", message.HtmlBody);

        using var verifyScope = factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var player = await db.PlayerAccounts.AsNoTracking().SingleAsync(item => item.Email == email);
        Assert.Equal(now, player.LastWeeklyEmailSentAtUtc);
    }

    private static WebApplicationFactory<Program> CreateFactory(RecordingEmailSender sender, bool enableWeeklyReports = false)
    {
        return new MasterApiWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Email:Enabled"] = "true",
                    ["Email:WeeklyReportsEnabled"] = enableWeeklyReports.ToString(),
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
            });
        });
    }

    private static async Task SeedWeeklyReportDataAsync(WebApplicationFactory<Program> factory, string email, DateTime now)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var player = await db.PlayerAccounts.SingleAsync(item => item.Email == email);
        var server = new GameServerNode
        {
            Id = Guid.NewGuid(),
            ServerKey = "central-europe",
            ServerKeyHash = "weekly-test-server-hash",
            DisplayName = "Central Europe",
            Description = "Weekly report test shard",
            Region = "EU",
            Environment = "testing",
            BackendUrl = "https://game.example.com",
            GraphqlUrl = "https://game.example.com/graphql",
            FrontendUrl = "https://game.example.com/app",
            Version = "1.0.0",
            PlayerCount = 3,
            CompanyCount = 2,
            CurrentTick = 120,
            RegisteredAtUtc = now.AddDays(-8),
            LastHeartbeatAtUtc = now.AddMinutes(-1),
            IsActive = true,
            ExpiresAtUtc = now.AddHours(1),
        };
        var bounty = new MasterRankingBountyDefinition
        {
            Id = Guid.NewGuid(),
            Code = "WEEKLY_TEST",
            DisplayName = "Weekly test",
            Description = "Weekly test bounty",
            RewardPoints = 42m,
            CreatedAtUtc = now.AddDays(-2),
            UpdatedAtUtc = now.AddDays(-2),
        };
        db.GameServers.Add(server);
        db.MasterRankingBountyDefinitions.Add(bounty);
        db.MasterRankingRewardRecords.Add(new MasterRankingRewardRecord
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            BountyDefinitionId = bounty.Id,
            PointsAwarded = 42m,
            Status = RankingRewardStatus.Awarded,
            UniquenessKey = "weekly-test",
            ServerKey = server.ServerKey,
            EventDateUtc = now.AddDays(-1),
            AwardedAtUtc = now.AddDays(-1),
        });
        db.MasterRankingPlayerSnapshots.Add(new MasterRankingPlayerSnapshot
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            TotalPoints = 42m,
            GlobalRank = 7,
            PreviousGlobalRank = 9,
            UpdatedAtUtc = now.AddDays(-1),
        });
        db.GameNewsEntries.Add(new GameNewsEntry
        {
            Id = Guid.NewGuid(),
            EntryType = GameNewsEntryType.Changelog,
            Status = GameNewsEntryStatus.Published,
            CreatedByEmail = "admin@example.com",
            UpdatedByEmail = "admin@example.com",
            CreatedAtUtc = now.AddDays(-1),
            UpdatedAtUtc = now.AddDays(-1),
            PublishedAtUtc = now.AddDays(-1),
            Localizations =
            [
                new GameNewsEntryLocalization
                {
                    Id = Guid.NewGuid(),
                    Locale = "en",
                    Title = "Market expansion",
                    Summary = "New weekly email reporting is available.",
                    HtmlContent = "<p>New weekly email reporting is available.</p>",
                },
                new GameNewsEntryLocalization
                {
                    Id = Guid.NewGuid(),
                    Locale = "de",
                    Title = "Market expansion",
                    Summary = "Neue wöchentliche E-Mail-Berichte sind verfügbar.",
                    HtmlContent = "<p>Neue wöchentliche E-Mail-Berichte sind verfügbar.</p>",
                },
            ],
        });
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> GraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
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
}
