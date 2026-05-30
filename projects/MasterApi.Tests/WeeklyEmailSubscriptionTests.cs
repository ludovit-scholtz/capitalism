using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MasterApi.Data;
using MasterApi.Tests.Infrastructure;
using MasterApi.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MasterApi.Tests;

public sealed class WeeklyEmailSubscriptionTests
{
    [Fact]
    public async Task WeeklyReport_SkipsUnsubscribedPlayers()
    {
        var fakeSender = new RecordingEmailSender();
        await using var factory = CreateFactory(fakeSender, enableWeeklyReports: true);
        using var client = factory.CreateClient();
        var email = $"weekly-skip-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email, "Skip Tester");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var player = await db.PlayerAccounts.SingleAsync(item => item.Email == email);
            player.WeeklyReportEmailUnsubscribed = true;
            await db.SaveChangesAsync();
        }

        fakeSender.Messages.Clear();
        var now = new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);
        using (var scope = factory.Services.CreateScope())
        {
            var reports = scope.ServiceProvider.GetRequiredService<IWeeklyEmailReportService>();
            var sent = await reports.SendDueWeeklyReportsAsync(now, CancellationToken.None);
            Assert.Equal(0, sent);
        }

        Assert.Empty(fakeSender.Messages);
    }

    [Fact]
    public async Task WeeklyReport_IncludesUnsubscribeLinkForSubscribedPlayers()
    {
        var fakeSender = new RecordingEmailSender();
        await using var factory = CreateFactory(fakeSender, enableWeeklyReports: true);
        using var client = factory.CreateClient();
        var email = $"weekly-link-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email, "Link Tester");
        fakeSender.Messages.Clear();

        Guid token;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            token = (await db.PlayerAccounts.AsNoTracking().SingleAsync(item => item.Email == email)).EmailUnsubscribeToken;
        }

        var now = new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);
        using (var scope = factory.Services.CreateScope())
        {
            var reports = scope.ServiceProvider.GetRequiredService<IWeeklyEmailReportService>();
            var sent = await reports.SendDueWeeklyReportsAsync(now, CancellationToken.None);
            Assert.Equal(1, sent);
        }

        var message = Assert.Single(fakeSender.Messages);
        Assert.Contains($"/email/unsubscribe?token={token:D}", message.HtmlBody);
        Assert.Contains($"/email/unsubscribe?token={token:D}", message.PlainTextBody);
    }

    [Fact]
    public async Task UnsubscribeByToken_SetsUnsubscribedFlag()
    {
        var fakeSender = new RecordingEmailSender();
        await using var factory = CreateFactory(fakeSender);
        using var client = factory.CreateClient();
        var email = $"unsub-{Guid.NewGuid():N}@example.com";
        await RegisterAsync(client, email, "Unsub Tester");

        Guid token;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            token = (await db.PlayerAccounts.AsNoTracking().SingleAsync(item => item.Email == email)).EmailUnsubscribeToken;
        }

        var result = await GraphQlAsync(client, """
            mutation Unsub($token: UUID!) { unsubscribeFromWeeklyReportEmail(token: $token) }
            """,
            new { token });

        Assert.False(result.TryGetProperty("errors", out _));
        Assert.True(result.GetProperty("data").GetProperty("unsubscribeFromWeeklyReportEmail").GetBoolean());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var player = await db.PlayerAccounts.AsNoTracking().SingleAsync(item => item.Email == email);
            Assert.True(player.WeeklyReportEmailUnsubscribed);
        }
    }

    [Fact]
    public async Task UnsubscribeByToken_UnknownToken_ReturnsNeutralSuccessWithoutLeaking()
    {
        var fakeSender = new RecordingEmailSender();
        await using var factory = CreateFactory(fakeSender);
        using var client = factory.CreateClient();

        var result = await GraphQlAsync(client, """
            mutation Unsub($token: UUID!) { unsubscribeFromWeeklyReportEmail(token: $token) }
            """,
            new { token = Guid.NewGuid() });

        Assert.False(result.TryGetProperty("errors", out _));
        var data = result.GetProperty("data").GetProperty("unsubscribeFromWeeklyReportEmail");
        Assert.True(data.GetBoolean());
        // The response is a bare boolean and never echoes any email address.
        Assert.DoesNotContain("@", result.GetRawText());
    }

    [Fact]
    public async Task AuthenticatedToggle_UpdatesPreference_AndMeReflectsStatus()
    {
        var fakeSender = new RecordingEmailSender();
        await using var factory = CreateFactory(fakeSender);
        using var client = factory.CreateClient();
        var email = $"toggle-{Guid.NewGuid():N}@example.com";
        var token = await RegisterAsync(client, email, "Toggle Tester");

        var meBefore = await GraphQlAsync(client, "query { me { weeklyReportEmailSubscribed } }", null, token);
        Assert.True(meBefore.GetProperty("data").GetProperty("me").GetProperty("weeklyReportEmailSubscribed").GetBoolean());

        var unsubscribe = await GraphQlAsync(client, """
            mutation Toggle($subscribed: Boolean!) { setWeeklyReportEmailSubscription(subscribed: $subscribed) }
            """,
            new { subscribed = false },
            token);
        Assert.False(unsubscribe.GetProperty("data").GetProperty("setWeeklyReportEmailSubscription").GetBoolean());

        var meAfter = await GraphQlAsync(client, "query { me { weeklyReportEmailSubscribed } }", null, token);
        Assert.False(meAfter.GetProperty("data").GetProperty("me").GetProperty("weeklyReportEmailSubscribed").GetBoolean());

        var resubscribe = await GraphQlAsync(client, """
            mutation Toggle($subscribed: Boolean!) { setWeeklyReportEmailSubscription(subscribed: $subscribed) }
            """,
            new { subscribed = true },
            token);
        Assert.True(resubscribe.GetProperty("data").GetProperty("setWeeklyReportEmailSubscription").GetBoolean());
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

    private static async Task<string> RegisterAsync(HttpClient client, string email, string displayName)
    {
        var register = await GraphQlAsync(client, """
            mutation Register($input: RegisterInput!) { register(input: $input) { token } }
            """,
            new { input = new { email, displayName, password = "TestPass123!", locale = "en" } });
        return register.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
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
