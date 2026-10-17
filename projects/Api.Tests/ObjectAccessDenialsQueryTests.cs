using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>#392: admins can see recent object-authorization denials (the real reason), nobody else can.</summary>
public sealed class ObjectAccessDenialsQueryTests
{
    private const string Query = """
        query($limit: Int) {
          objectAccessDenials(limit: $limit) {
            totalRecorded windowSize probeSignals
            byObjectType { key count }
            byReason { key count }
            recent { occurredAtUtc actorUserId objectType objectId reason isProbeSignal }
          }
        }
        """;

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Denial Test", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task ObjectAccessDenials_Unauthenticated_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { limit = 10 });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
        Assert.False(result.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Object
            && data.TryGetProperty("objectAccessDenials", out var leaked) && leaked.ValueKind == System.Text.Json.JsonValueKind.Object);
    }

    [Fact]
    public async Task ObjectAccessDenials_NonAdmin_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = await RegisterAsync(client, $"denial-player-{Guid.NewGuid():N}@example.com");

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { limit = 10 }, token);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ObjectAccessDenials_Admin_SeesAProbeWithItsRealReason()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var adminEmail = $"denial-admin-{Guid.NewGuid():N}@example.com";
        var adminToken = await RegisterAsync(client, adminEmail);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Players.FirstAsync(p => p.Email == adminEmail)).Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        // A player probes an id that does not exist: the caller only ever sees the uniform FORBIDDEN.
        var prober = await RegisterAsync(client, $"denial-prober-{Guid.NewGuid():N}@example.com");
        var probedId = Guid.NewGuid();
        var probe = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: CloseBankAccountInput!) { closeBankAccount(input: $input) { __typename } }",
            new { input = new { depositId = probedId, amount = 0m } },
            prober);
        Assert.Equal("FORBIDDEN", probe.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { limit = 10 }, adminToken);

        var report = result.GetProperty("data").GetProperty("objectAccessDenials");
        Assert.True(report.GetProperty("totalRecorded").GetInt64() >= 1);
        Assert.True(report.GetProperty("probeSignals").GetInt32() >= 1);
        var newest = report.GetProperty("recent")[0];
        Assert.Equal("bank_account", newest.GetProperty("objectType").GetString());
        Assert.Equal(probedId.ToString(), newest.GetProperty("objectId").GetString(), ignoreCase: true);
        Assert.Equal("not_found", newest.GetProperty("reason").GetString());
        Assert.True(newest.GetProperty("isProbeSignal").GetBoolean());
        Assert.Contains(report.GetProperty("byReason").EnumerateArray(), r => r.GetProperty("key").GetString() == "not_found");
    }

    [Fact]
    public async Task ObjectAccessDenials_LimitIsClamped()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var adminEmail = $"denial-limit-{Guid.NewGuid():N}@example.com";
        var adminToken = await RegisterAsync(client, adminEmail);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Players.FirstAsync(p => p.Email == adminEmail)).Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { limit = 1_000_000 }, adminToken);

        Assert.False(result.TryGetProperty("errors", out var errors), $"limit must be clamped, not rejected: {errors}");
        Assert.True(result.GetProperty("data").GetProperty("objectAccessDenials").GetProperty("recent").GetArrayLength() <= 500);
    }
}
