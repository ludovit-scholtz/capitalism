using Api.Data;
using Api.Data.Entities;
using Api.Engine.Campaign;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>#602: the "while you were away" summary, computed from the caller's own ledger since a tick.</summary>
public sealed class AwaySummaryTests
{
    private const string AwayQuery = """
        query($sinceTick: Long!) {
          awaySummary(sinceTick: $sinceTick) {
            sinceTick currentTick ticksAway revenueUsd costsUsd netUsd loanPaymentsCount
          }
        }
        """;

    // ── pure calculator ─────────────────────────────────────────────────────

    [Fact]
    public void Summarize_SumsRevenueAndOperatingCostsAndCountsLoanPayments()
    {
        var summary = AwaySummaryCalculator.Summarize(
        [
            new AwayLedgerRow(LedgerCategory.Revenue, 1_000m),
            new AwayLedgerRow(LedgerCategory.Revenue, 500m),
            new AwayLedgerRow(LedgerCategory.RentIncome, 200m),
            new AwayLedgerRow(LedgerCategory.LaborCost, -300m),
            new AwayLedgerRow(LedgerCategory.Tax, -100m),
            new AwayLedgerRow(LedgerCategory.LoanRepaymentPrincipal, -250m),
            new AwayLedgerRow(LedgerCategory.LoanRepaymentPrincipal, -250m),
        ]);

        Assert.Equal(1_700m, summary.RevenueUsd);
        Assert.Equal(-400m, summary.CostsUsd);
        Assert.Equal(1_300m, summary.NetUsd);
        Assert.Equal(2, summary.LoanPaymentsCount);
    }

    [Fact]
    public void Summarize_IgnoresInvestmentsAndTransfers()
    {
        var summary = AwaySummaryCalculator.Summarize(
        [
            new AwayLedgerRow(LedgerCategory.PropertyPurchase, -50_000m),
            new AwayLedgerRow(LedgerCategory.ConstructionCost, -10_000m),
            new AwayLedgerRow(LedgerCategory.BankAccountTransferIn, 7_000m),
            new AwayLedgerRow(LedgerCategory.LoanOrigination, 20_000m),
            new AwayLedgerRow(LedgerCategory.Dividend, -1_000m),
        ]);

        Assert.Equal(0m, summary.RevenueUsd);
        Assert.Equal(0m, summary.CostsUsd);
        Assert.Equal(0m, summary.NetUsd);
        Assert.Equal(0, summary.LoanPaymentsCount);
    }

    [Fact]
    public void Summarize_OfNothing_IsZero()
    {
        var summary = AwaySummaryCalculator.Summarize([]);

        Assert.Equal(0m, summary.NetUsd);
        Assert.Equal(0, summary.LoanPaymentsCount);
    }

    [Fact]
    public void ClampSinceTick_KeepsTheWindowInsideTheRetainedHistory()
    {
        Assert.Equal(900, AwaySummaryCalculator.ClampSinceTick(900, 1_000));
        Assert.Equal(1_000, AwaySummaryCalculator.ClampSinceTick(5_000, 1_000));
        Assert.Equal(1_000 - AwaySummaryCalculator.MaxWindowTicks, AwaySummaryCalculator.ClampSinceTick(0, 1_000));
        Assert.Equal(0, AwaySummaryCalculator.ClampSinceTick(-5, 10));
    }

    // ── GraphQL ─────────────────────────────────────────────────────────────

    private sealed record Seeded(string Token, Guid PlayerId, Guid CompanyId, Guid AccountId);

    private static async Task<Seeded> SeedPlayerAsync(ApiWebApplicationFactory factory, HttpClient client, string currency = "USD")
    {
        var email = $"away-{Guid.NewGuid():N}@example.com";
        var reg = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Away", password = "TestPass123!" } });
        var token = reg.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.FirstAsync(p => p.Email == email);
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Away Co", FoundedAtUtc = DateTime.UtcNow };
        var account = new BankAccount
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = currency, Balance = 1_000m,
            AccountNumber = Random.Shared.NextInt64(7_000_000_000_000_000, 7_999_999_999_999_999).ToString(), CreatedAtUtc = DateTime.UtcNow,
        };
        db.Companies.Add(company);
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();
        return new Seeded(token, player.Id, company.Id, account.Id);
    }

    private static async Task SetTickAsync(ApiWebApplicationFactory factory, long tick)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var state = await db.GameStates.OrderBy(s => s.Id).FirstAsync();
        state.CurrentTick = tick;
        await db.SaveChangesAsync();
    }

    private static async Task AddLedgerAsync(ApiWebApplicationFactory factory, Seeded p, string category, decimal amount, long tick)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(), CompanyId = p.CompanyId, BankAccountId = p.AccountId, Category = category,
            Description = "away test", Amount = amount, RecordedAtTick = tick, RecordedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AwaySummary_Unauthenticated_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(client, AwayQuery, new { sinceTick = 0 });

        Assert.True(result.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task AwaySummary_CountsOnlyEntriesAfterTheSinceTickUpToNow()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client);
        await SetTickAsync(factory, 100);
        await AddLedgerAsync(factory, p, LedgerCategory.Revenue, 999m, 50); // before the window
        await AddLedgerAsync(factory, p, LedgerCategory.Revenue, 100m, 60); // exactly at sinceTick: already seen
        await AddLedgerAsync(factory, p, LedgerCategory.Revenue, 3_000m, 61);
        await AddLedgerAsync(factory, p, LedgerCategory.LaborCost, -1_000m, 90);
        await AddLedgerAsync(factory, p, LedgerCategory.LoanRepaymentPrincipal, -500m, 95);
        await AddLedgerAsync(factory, p, LedgerCategory.Revenue, 777m, 101); // after the current tick: not part of the world yet

        var result = await TestHelpers.ExecuteGraphQlAsync(client, AwayQuery, new { sinceTick = 60 }, p.Token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var summary = result.GetProperty("data").GetProperty("awaySummary");
        Assert.Equal(60, summary.GetProperty("sinceTick").GetInt64());
        Assert.Equal(100, summary.GetProperty("currentTick").GetInt64());
        Assert.Equal(40, summary.GetProperty("ticksAway").GetInt64());
        Assert.Equal(3_000m, summary.GetProperty("revenueUsd").GetDecimal());
        Assert.Equal(-1_000m, summary.GetProperty("costsUsd").GetDecimal());
        Assert.Equal(2_000m, summary.GetProperty("netUsd").GetDecimal());
        Assert.Equal(1, summary.GetProperty("loanPaymentsCount").GetInt32());
    }

    [Fact]
    public async Task AwaySummary_NeverIncludesAnotherPlayersLedger()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var mine = await SeedPlayerAsync(factory, client);
        var theirs = await SeedPlayerAsync(factory, client);
        await SetTickAsync(factory, 100);
        await AddLedgerAsync(factory, mine, LedgerCategory.Revenue, 100m, 80);
        await AddLedgerAsync(factory, theirs, LedgerCategory.Revenue, 50_000m, 80);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, AwayQuery, new { sinceTick = 0 }, mine.Token);

        Assert.Equal(100m, result.GetProperty("data").GetProperty("awaySummary").GetProperty("revenueUsd").GetDecimal());
    }

    [Fact]
    public async Task AwaySummary_ConvertsForeignCurrencyEntriesToUsd()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client, currency: "EUR");
        await SetTickAsync(factory, 100);
        await AddLedgerAsync(factory, p, LedgerCategory.Revenue, 1_000m, 80);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, AwayQuery, new { sinceTick = 0 }, p.Token);

        var revenue = result.GetProperty("data").GetProperty("awaySummary").GetProperty("revenueUsd").GetDecimal();
        Assert.True(revenue > 1_000m, $"1,000 EUR should be worth more than 1,000 USD but was {revenue}");
    }

    [Fact]
    public async Task AwaySummary_WithASinceTickInTheFuture_ReportsNothing()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client);
        await SetTickAsync(factory, 100);
        await AddLedgerAsync(factory, p, LedgerCategory.Revenue, 100m, 80);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, AwayQuery, new { sinceTick = 9_999 }, p.Token);

        var summary = result.GetProperty("data").GetProperty("awaySummary");
        Assert.Equal(100, summary.GetProperty("sinceTick").GetInt64());
        Assert.Equal(0, summary.GetProperty("ticksAway").GetInt64());
        Assert.Equal(0m, summary.GetProperty("netUsd").GetDecimal());
    }

    [Fact]
    public async Task AwaySummary_LooksBackAtMostTheMaximumWindow()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client);
        var now = AwaySummaryCalculator.MaxWindowTicks + 500L;
        await SetTickAsync(factory, now);
        await AddLedgerAsync(factory, p, LedgerCategory.Revenue, 5_000m, 100); // older than the window
        await AddLedgerAsync(factory, p, LedgerCategory.Revenue, 10m, now - 5);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, AwayQuery, new { sinceTick = 0 }, p.Token);

        var summary = result.GetProperty("data").GetProperty("awaySummary");
        Assert.Equal(now - AwaySummaryCalculator.MaxWindowTicks, summary.GetProperty("sinceTick").GetInt64());
        Assert.Equal(10m, summary.GetProperty("revenueUsd").GetDecimal());
    }

    [Fact]
    public async Task AwaySummary_ForAPlayerWithoutCompanies_IsZero()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var reg = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email = $"away-none-{Guid.NewGuid():N}@example.com", displayName = "None", password = "TestPass123!" } });
        var token = reg.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        await SetTickAsync(factory, 100);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, AwayQuery, new { sinceTick = 10 }, token);

        Assert.Equal(0m, result.GetProperty("data").GetProperty("awaySummary").GetProperty("netUsd").GetDecimal());
    }
}
