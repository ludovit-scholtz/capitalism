using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class TickPhaseTimingsQueryTests
{
    private const string Query = """
        query {
          tickPhaseTimings {
            sampledTicks lastTick lastTotalMs averageTotalMs maxTotalMs phaseBudgetMs
            phases { name lastMs averageMs maxMs aboveBudget }
          }
        }
        """;

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Timing Test", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task TickPhaseTimings_Unauthenticated_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task TickPhaseTimings_NonAdmin_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var token = await RegisterAsync(client, $"timing-player-{Guid.NewGuid():N}@example.com");

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, token: token);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task TickPhaseTimings_Admin_SeesRecordedPhasesSlowestFirst()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var email = $"timing-admin-{Guid.NewGuid():N}@example.com";
        var token = await RegisterAsync(client, email);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Players.FirstAsync(p => p.Email == email)).Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<TickTimingRecorder>()
                .Record(7, 180, new Dictionary<string, long> { ["Cheap"] = 20, ["Expensive"] = 160 });
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, token: token);

        var timings = result.GetProperty("data").GetProperty("tickPhaseTimings");
        Assert.Equal(1, timings.GetProperty("sampledTicks").GetInt32());
        Assert.Equal(7, timings.GetProperty("lastTick").GetInt64());
        var phases = timings.GetProperty("phases");
        Assert.Equal("Expensive", phases[0].GetProperty("name").GetString());
        Assert.True(phases[0].GetProperty("aboveBudget").GetBoolean());
        Assert.False(phases[1].GetProperty("aboveBudget").GetBoolean());
    }
}
