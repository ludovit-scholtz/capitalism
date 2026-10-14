using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Pins the per-year history returned by <c>companyLedger</c>. The history is aggregated per year /
/// category / sign (it used to load every ledger row of the company into memory), so these tests make
/// sure the aggregation matches row-by-row accounting, including sign handling, zero-amount rows and
/// first/last recorded ticks.
/// </summary>
public sealed class CompanyLedgerHistoryTests
{
    private const long TicksPerYear = Api.Engine.GameConstants.TicksPerYear;

    private const string Query = """
        query Ledger($companyId: UUID!) {
          companyLedger(companyId: $companyId) {
            history {
              gameYear
              totalRevenue
              totalLaborCosts
              totalEnergyCosts
              netIncome
              totalTaxPaid
              taxableIncome
              firstRecordedTick
              lastRecordedTick
            }
          }
        }
        """;

    private static LedgerEntry Entry(Guid companyId, string category, decimal amount, long tick) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        Category = category,
        Amount = amount,
        RecordedAtTick = tick,
        Description = "history test",
        RecordedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public void HistoryAggregateQuery_TranslatesToGroupedSqlForNpgsql()
    {
        // The unit tests run on the InMemory provider, which evaluates LINQ on the client and would hide a
        // query PostgreSQL cannot translate. Building the SQL with the Npgsql provider (no connection is
        // opened) proves the production query translates and is grouped in the database.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=ledger-query-translation;Username=test;Password=test")
            .Options;
        using var dbContext = new AppDbContext(options);

        var sql = Api.Types.Query.HistoryAggregateQuery(dbContext.LedgerEntries, Guid.NewGuid()).ToQueryString();

        Assert.Contains("GROUP BY", sql, StringComparison.Ordinal);
        Assert.Contains("SUM(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIN(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MAX(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task History_AggregatesEachGameYearLikeRowByRowAccounting()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var register = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email = $"ledger-history-{Guid.NewGuid():N}@test.com", displayName = "History", password = "Password1!" } });
        var token = register.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var me = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(me.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        Guid companyId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Name = $"History-{Guid.NewGuid():N}",
                FoundedAtUtc = DateTime.UtcNow,
            };
            db.Companies.Add(company);
            companyId = company.Id;

            db.LedgerEntries.AddRange(
                // Game year 2000 (ticks 0 .. 8759)
                Entry(companyId, LedgerCategory.Revenue, 0m, 50),            // zero row still counts for the first tick
                Entry(companyId, LedgerCategory.Revenue, 1000m, 100),
                Entry(companyId, LedgerCategory.Revenue, 500m, 5000),
                Entry(companyId, LedgerCategory.LaborCost, -200m, 300),
                Entry(companyId, LedgerCategory.LaborCost, 40m, 350),         // positive labor row is ignored
                Entry(companyId, LedgerCategory.EnergyCost, -50m, 400),
                Entry(companyId, LedgerCategory.Tax, -30m, 8000),
                // Game year 2001 (ticks 8760 .. 17519)
                Entry(companyId, LedgerCategory.Revenue, 2000m, TicksPerYear + 10),
                Entry(companyId, LedgerCategory.PurchasingCost, -700m, TicksPerYear + 240),
                Entry(companyId, LedgerCategory.LaborCost, -100m, TicksPerYear + 740));
            await db.SaveChangesAsync();
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId }, token);

        var history = result.GetProperty("data").GetProperty("companyLedger").GetProperty("history")
            .EnumerateArray()
            .ToDictionary(item => item.GetProperty("gameYear").GetInt32());

        var y2000 = history[2000];
        Assert.Equal(1500m, y2000.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(200m, y2000.GetProperty("totalLaborCosts").GetDecimal());
        Assert.Equal(50m, y2000.GetProperty("totalEnergyCosts").GetDecimal());
        Assert.Equal(30m, y2000.GetProperty("totalTaxPaid").GetDecimal());
        Assert.Equal(1250m, y2000.GetProperty("taxableIncome").GetDecimal());
        Assert.Equal(1220m, y2000.GetProperty("netIncome").GetDecimal());
        Assert.Equal(50, y2000.GetProperty("firstRecordedTick").GetInt64());
        Assert.Equal(8000, y2000.GetProperty("lastRecordedTick").GetInt64());

        var y2001 = history[2001];
        Assert.Equal(2000m, y2001.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(100m, y2001.GetProperty("totalLaborCosts").GetDecimal());
        Assert.Equal(0m, y2001.GetProperty("totalEnergyCosts").GetDecimal());
        Assert.Equal(0m, y2001.GetProperty("totalTaxPaid").GetDecimal());
        Assert.Equal(1200m, y2001.GetProperty("taxableIncome").GetDecimal());
        Assert.Equal(1200m, y2001.GetProperty("netIncome").GetDecimal());
        Assert.Equal(TicksPerYear + 10, y2001.GetProperty("firstRecordedTick").GetInt64());
        Assert.Equal(TicksPerYear + 740, y2001.GetProperty("lastRecordedTick").GetInt64());
    }
}
