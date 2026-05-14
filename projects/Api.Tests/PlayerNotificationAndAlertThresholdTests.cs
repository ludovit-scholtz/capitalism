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

    [Fact]
    public async Task MyNotifications_OnlyUnread_AndExpiryFilter_Work()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, email) = await RegisterAndGetTokenAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(playerItem => playerItem.Email == email);
            var now = DateTime.UtcNow;

            db.PlayerNotifications.AddRange(
                new PlayerNotification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    Type = PlayerNotificationType.ProductionHalted,
                    Severity = PlayerNotificationSeverity.Critical,
                    Title = "Critical",
                    Message = "Critical message",
                    IsRead = false,
                    CreatedAtTick = 300,
                    CreatedAtUtc = now.AddMinutes(-1),
                    ExpiresAtUtc = now.AddDays(1),
                },
                new PlayerNotification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    Type = PlayerNotificationType.OversupplyWarning,
                    Severity = PlayerNotificationSeverity.Warning,
                    Title = "Read item",
                    Message = "Read message",
                    IsRead = true,
                    ReadAtUtc = now.AddMinutes(-2),
                    CreatedAtTick = 299,
                    CreatedAtUtc = now.AddMinutes(-2),
                    ExpiresAtUtc = now.AddDays(1),
                },
                new PlayerNotification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    Type = PlayerNotificationType.PriceSpike,
                    Severity = PlayerNotificationSeverity.Warning,
                    Title = "Expired",
                    Message = "Expired message",
                    IsRead = false,
                    CreatedAtTick = 298,
                    CreatedAtUtc = now.AddMinutes(-3),
                    ExpiresAtUtc = now.AddMinutes(-1),
                });

            await db.SaveChangesAsync();
        }

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query MyNotifications($first: Int!, $onlyUnread: Boolean!) {
              myNotifications(first: $first, onlyUnread: $onlyUnread) {
                totalCount
                edges {
                  node {
                    id
                    type
                    severity
                    isRead
                    expiresAtUtc
                  }
                }
              }
              notificationCount
            }
            """,
            new
            {
                first = 20,
                onlyUnread = true,
            },
            token);

        var data = result.GetProperty("data");
        Assert.Equal(1, data.GetProperty("notificationCount").GetInt32());

        var connection = data.GetProperty("myNotifications");
        Assert.Equal(1, connection.GetProperty("totalCount").GetInt32());
        var edge = connection.GetProperty("edges")[0].GetProperty("node");
        Assert.Equal(PlayerNotificationType.ProductionHalted, edge.GetProperty("type").GetString());
        Assert.Equal(PlayerNotificationSeverity.Critical, edge.GetProperty("severity").GetString());
        Assert.False(edge.GetProperty("isRead").GetBoolean());
    }

    [Fact]
    public async Task MyNotifications_Pagination_AndOwnershipIsolation_Work()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (tokenA, emailA) = await RegisterAndGetTokenAsync(client);
        var (tokenB, emailB) = await RegisterAndGetTokenAsync(client);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var playerA = await db.Players.FirstAsync(playerItem => playerItem.Email == emailA);
            var playerB = await db.Players.FirstAsync(playerItem => playerItem.Email == emailB);
            var now = DateTime.UtcNow;

            db.PlayerNotifications.AddRange(
                new PlayerNotification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = playerA.Id,
                    Type = PlayerNotificationType.BuildingOfferReceived,
                    Title = "A-1",
                    Message = "A-1",
                    CreatedAtTick = 20,
                    CreatedAtUtc = now,
                },
                new PlayerNotification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = playerA.Id,
                    Type = PlayerNotificationType.BuildingOfferReceived,
                    Title = "A-2",
                    Message = "A-2",
                    CreatedAtTick = 19,
                    CreatedAtUtc = now.AddSeconds(-1),
                },
                new PlayerNotification
                {
                    Id = Guid.NewGuid(),
                    PlayerId = playerB.Id,
                    Type = PlayerNotificationType.BuildingOfferReceived,
                    Title = "B-1",
                    Message = "B-1",
                    CreatedAtTick = 18,
                    CreatedAtUtc = now.AddSeconds(-2),
                });

            await db.SaveChangesAsync();
        }

        var page1 = await ExecuteGraphQlAsync(
            client,
            """
            query FirstPage($first: Int!) {
              myNotifications(first: $first) {
                edges {
                  cursor
                  node { title }
                }
                pageInfo { hasNextPage endCursor }
              }
            }
            """,
            new { first = 1 },
            tokenA);

        var page1Connection = page1.GetProperty("data").GetProperty("myNotifications");
        Assert.Equal(1, page1Connection.GetProperty("edges").GetArrayLength());
        Assert.Equal("A-1", page1Connection.GetProperty("edges")[0].GetProperty("node").GetProperty("title").GetString());
        Assert.True(page1Connection.GetProperty("pageInfo").GetProperty("hasNextPage").GetBoolean());
        var endCursor = page1Connection.GetProperty("pageInfo").GetProperty("endCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(endCursor));

        var page2 = await ExecuteGraphQlAsync(
            client,
            """
            query SecondPage($first: Int!, $after: String) {
              myNotifications(first: $first, after: $after) {
                edges {
                  node { title }
                }
              }
            }
            """,
            new { first = 10, after = endCursor },
            tokenA);
        Assert.Equal("A-2", page2.GetProperty("data").GetProperty("myNotifications").GetProperty("edges")[0].GetProperty("node").GetProperty("title").GetString());

        var playerBResult = await ExecuteGraphQlAsync(
            client,
            """
            query BView($first: Int!) {
              myNotifications(first: $first) {
                edges {
                  node { title }
                }
              }
            }
            """,
            new { first = 20 },
            tokenB);
        var titlesB = playerBResult.GetProperty("data").GetProperty("myNotifications").GetProperty("edges");
        Assert.Equal(1, titlesB.GetArrayLength());
        Assert.Equal("B-1", titlesB[0].GetProperty("node").GetProperty("title").GetString());
    }

    [Fact]
    public async Task MarkNotificationsRead_AndMarkAllNotificationsRead_Work()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, email) = await RegisterAndGetTokenAsync(client);
        Guid firstId;
        Guid secondId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(playerItem => playerItem.Email == email);

            var first = new PlayerNotification
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                Type = PlayerNotificationType.LoanPaymentDue,
                Title = "Loan payment due",
                Message = "Due soon",
                IsRead = false,
                CreatedAtTick = 10,
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            };
            var second = new PlayerNotification
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                Type = PlayerNotificationType.LoanDefault,
                Title = "Loan default",
                Message = "Defaulted",
                IsRead = false,
                CreatedAtTick = 11,
                CreatedAtUtc = DateTime.UtcNow,
            };

            db.PlayerNotifications.AddRange(first, second);
            await db.SaveChangesAsync();
            firstId = first.Id;
            secondId = second.Id;
        }

        var markOne = await ExecuteGraphQlAsync(
            client,
            """
            mutation MarkOne($ids: [UUID!]!) {
              markNotificationsRead(ids: $ids)
            }
            """,
            new { ids = new[] { firstId.ToString() } },
            token);
        Assert.True(markOne.GetProperty("data").GetProperty("markNotificationsRead").GetBoolean());

        var countAfterOne = await ExecuteGraphQlAsync(client, "query { notificationCount }", token: token);
        Assert.Equal(1, countAfterOne.GetProperty("data").GetProperty("notificationCount").GetInt32());

        var markAll = await ExecuteGraphQlAsync(client, "mutation { markAllNotificationsRead }", token: token);
        Assert.True(markAll.GetProperty("data").GetProperty("markAllNotificationsRead").GetBoolean());

        var countAfterAll = await ExecuteGraphQlAsync(client, "query { notificationCount }", token: token);
        Assert.Equal(0, countAfterAll.GetProperty("data").GetProperty("notificationCount").GetInt32());
        Assert.NotEqual(firstId, secondId);
    }
}
