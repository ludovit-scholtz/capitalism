using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class PlayerNotificationAndAlertThresholdTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(
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
            request.Headers.Authorization = new("Bearer", token);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<(string Token, string Email)> RegisterAndGetTokenAsync(HttpClient client)
    {
        var email = $"notif-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass123!";

        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new
            {
                input = new
                {
                    email,
                    displayName = "Notification Tester",
                    password,
                },
            });

        var token = result.GetProperty("data").GetProperty("register").GetProperty("token").GetString();
        return (token!, email);
    }

    [Fact]
    public async Task PlayerNotificationInbox_AndMarkAllRead_WorkEndToEnd()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, email) = await RegisterAndGetTokenAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(playerItem => playerItem.Email == email);

            db.PlayerNotifications.AddRange(
                new PlayerNotification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    Type = PlayerNotificationType.BuildingConstructionCompleted,
                    Title = "Construction complete",
                    Message = "Factory A is now online.",
                    CreatedAtTick = 100,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                },
                new PlayerNotification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    Type = PlayerNotificationType.BankAccountLowBalance,
                    Title = "Low balance",
                    Message = "Top up your account.",
                    CreatedAtTick = 101,
                    CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                });

            await db.SaveChangesAsync();
        }

        var inboxResult = await ExecuteGraphQlAsync(
            client,
            """
            query NotificationInbox($limit: Int!) {
              playerNotificationInbox(limit: $limit) {
                unreadCount
                items {
                  id
                  type
                  title
                  isRead
                }
              }
            }
            """,
            new { limit = 20 },
            token);

        var inbox = inboxResult.GetProperty("data").GetProperty("playerNotificationInbox");
        Assert.Equal(2, inbox.GetProperty("unreadCount").GetInt32());
        Assert.Equal(2, inbox.GetProperty("items").GetArrayLength());

        var markAllResult = await ExecuteGraphQlAsync(
            client,
            "mutation { markAllPlayerNotificationsRead }",
            token: token);

        Assert.Equal(2, markAllResult.GetProperty("data").GetProperty("markAllPlayerNotificationsRead").GetInt32());

        var unreadResult = await ExecuteGraphQlAsync(
            client,
            "query { playerNotificationUnreadCount }",
            token: token);

        Assert.Equal(0, unreadResult.GetProperty("data").GetProperty("playerNotificationUnreadCount").GetInt32());
    }

    [Fact]
    public async Task SetBankAccountAlertThreshold_StoresThreshold()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, email) = await RegisterAndGetTokenAsync(client);

        Guid bankAccountId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(playerItem => playerItem.Email == email);
            var city = await db.Cities.FirstAsync(cityItem => cityItem.Name == "Bratislava");

            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                Name = "Threshold Co",
                FoundedAtUtc = DateTime.UtcNow,
                FoundedAtTick = 1,
            };
            db.Companies.Add(company);

            var account = new BankAccount
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                AccountNumber = Guid.NewGuid().ToString("N")[..16],
                CurrencyCode = city.CurrencyCode,
                Balance = 10_000m,
                CreatedAtUtc = DateTime.UtcNow,
                IsGovernmentAccount = false,
            };
            db.BankAccounts.Add(account);

            await db.SaveChangesAsync();
            bankAccountId = account.Id;
        }

        var mutationResult = await ExecuteGraphQlAsync(
            client,
            """
            mutation SetThreshold($input: SetBankAccountAlertThresholdInput!) {
              setBankAccountAlertThreshold(input: $input) {
                bankAccountId
                alertMinBalanceThreshold
              }
            }
            """,
            new
            {
                input = new
                {
                    bankAccountId,
                    minBalanceThreshold = 4500m,
                },
            },
            token);

        var payload = mutationResult.GetProperty("data").GetProperty("setBankAccountAlertThreshold");
        Assert.Equal(bankAccountId.ToString(), payload.GetProperty("bankAccountId").GetString());
        Assert.Equal(4500m, payload.GetProperty("alertMinBalanceThreshold").GetDecimal());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await verifyDb.BankAccounts.FirstAsync(account => account.Id == bankAccountId);
        Assert.Equal(4500m, persisted.AlertMinBalanceThreshold);
    }
}
