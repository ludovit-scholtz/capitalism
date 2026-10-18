using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine.Campaign;
using Api.Tests.Infrastructure;
using Capitalism.Shared.Tutorial;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>#602: first-week campaign query and reward claim: auth, ownership, idempotency, ledger and balance.</summary>
public sealed class FirstWeekCampaignGraphQlTests
{
    private const string CampaignQuery = """
        query {
          firstWeekCampaign {
            started dayNumber claimedCount allClaimed
            goals { code day progress target isUnlocked isComplete isClaimed canClaim rewardAmount rewardCurrencyCode }
          }
        }
        """;

    private const string ClaimMutation = """
        mutation($input: ClaimCampaignRewardInput!) {
          claimCampaignReward(input: $input) { claimed goalCode amount currencyCode }
        }
        """;

    private sealed record Player_(string Token, Guid PlayerId, Guid CompanyId, Guid AccountId);

    private static async Task<Player_> SeedPlayerAsync(
        ApiWebApplicationFactory factory,
        HttpClient client,
        TimeSpan onboardedAgo,
        bool onboarded = true,
        string[]? milestones = null)
    {
        var email = $"campaign-{Guid.NewGuid():N}@example.com";
        var reg = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Campaign", password = "TestPass123!" } });
        var token = reg.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.FirstAsync(p => p.Email == email);
        if (onboarded) player.OnboardingCompletedAtUtc = DateTime.UtcNow - onboardedAgo;
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Campaign Co", FoundedAtUtc = DateTime.UtcNow };
        var account = new BankAccount
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = FirstWeekCampaignCatalog.RewardCurrency, Balance = 1_000m,
            AccountNumber = "7000000000000001", CreatedAtUtc = DateTime.UtcNow,
        };
        player.ActiveCompanyId = company.Id;
        db.Companies.Add(company);
        db.BankAccounts.Add(account);
        db.Buildings.Add(new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Campaign Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow,
        });
        foreach (var milestone in milestones ?? [])
        {
            db.TutorialProgresses.Add(new TutorialProgress
            {
                Id = Guid.NewGuid(), PlayerId = player.Id, Milestone = milestone, IsCompleted = true, CompletedAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return new Player_(token, player.Id, company.Id, account.Id);
    }

    private static JsonElement Goal(JsonElement result, string code)
        => result.GetProperty("data").GetProperty("firstWeekCampaign").GetProperty("goals").EnumerateArray()
            .Single(g => g.GetProperty("code").GetString() == code);

    [Fact]
    public async Task Campaign_Unauthenticated_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var query = await TestHelpers.ExecuteGraphQlAsync(client, CampaignQuery);
        var claim = await TestHelpers.ExecuteGraphQlAsync(client, ClaimMutation, new { input = new { goalCode = FirstWeekCampaignCatalog.FirstSale } });

        Assert.True(query.TryGetProperty("errors", out _));
        Assert.True(claim.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Campaign_ForAPlayerWhoHasNotFinishedOnboarding_HasNotStarted()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client, TimeSpan.Zero, onboarded: false);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, CampaignQuery, token: p.Token);

        var campaign = result.GetProperty("data").GetProperty("firstWeekCampaign");
        Assert.False(campaign.GetProperty("started").GetBoolean());
        Assert.Equal(0, campaign.GetProperty("dayNumber").GetInt32());
    }

    [Fact]
    public async Task Campaign_OnDayOne_UnlocksOnlyTheFirstGoal_AndReportsProgressFromRealData()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client, TimeSpan.FromHours(2), milestones: [TutorialMilestoneCodes.FirstResourceSold]);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, CampaignQuery, token: p.Token);

        Assert.Equal(1, result.GetProperty("data").GetProperty("firstWeekCampaign").GetProperty("dayNumber").GetInt32());
        var first = Goal(result, FirstWeekCampaignCatalog.FirstSale);
        Assert.True(first.GetProperty("canClaim").GetBoolean());
        Assert.Equal(2000m, first.GetProperty("rewardAmount").GetDecimal());
        Assert.Equal("USD", first.GetProperty("rewardCurrencyCode").GetString());
        var second = Goal(result, FirstWeekCampaignCatalog.SecondBuilding);
        Assert.False(second.GetProperty("isUnlocked").GetBoolean());
        Assert.Equal(1, second.GetProperty("progress").GetInt32()); // one building owned so far
    }

    [Fact]
    public async Task Claim_PaysTheRewardOnce_WithALedgerEntry_AndIsIdempotent()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client, TimeSpan.FromHours(2), milestones: [TutorialMilestoneCodes.FirstResourceSold]);

        var first = await TestHelpers.ExecuteGraphQlAsync(client, ClaimMutation, new { input = new { goalCode = FirstWeekCampaignCatalog.FirstSale } }, p.Token);
        var second = await TestHelpers.ExecuteGraphQlAsync(client, ClaimMutation, new { input = new { goalCode = FirstWeekCampaignCatalog.FirstSale } }, p.Token);

        Assert.True(first.GetProperty("data").GetProperty("claimCampaignReward").GetProperty("claimed").GetBoolean());
        Assert.Equal(2000m, first.GetProperty("data").GetProperty("claimCampaignReward").GetProperty("amount").GetDecimal());
        Assert.False(second.GetProperty("data").GetProperty("claimCampaignReward").GetProperty("claimed").GetBoolean());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3_000m, (await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == p.AccountId)).Balance); // 1,000 + one reward
        var entry = await db.LedgerEntries.AsNoTracking().SingleAsync(e => e.CompanyId == p.CompanyId && e.Category == LedgerCategory.CampaignReward);
        Assert.Equal((2000m, p.AccountId), (entry.Amount, entry.BankAccountId));
        Assert.Single(await db.PlayerCampaignClaims.AsNoTracking().Where(c => c.PlayerId == p.PlayerId).ToListAsync());
    }

    [Theory]
    [InlineData("NOT_A_GOAL", "CAMPAIGN_GOAL_UNKNOWN")]
    [InlineData(FirstWeekCampaignCatalog.SecondBuilding, "CAMPAIGN_GOAL_LOCKED")]      // day 2 goal on day 1
    [InlineData(FirstWeekCampaignCatalog.FirstSale, "CAMPAIGN_GOAL_NOT_COMPLETE")]     // unlocked but the milestone is missing
    public async Task Claim_RejectsUnknownLockedAndIncompleteGoals_WithoutPaying(string goalCode, string expectedCode)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client, TimeSpan.FromHours(2));

        var result = await TestHelpers.ExecuteGraphQlAsync(client, ClaimMutation, new { input = new { goalCode } }, p.Token);

        Assert.Equal(expectedCode, result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1_000m, (await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == p.AccountId)).Balance);
        Assert.Empty(await db.PlayerCampaignClaims.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Claim_BeforeOnboardingIsFinished_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client, TimeSpan.Zero, onboarded: false, milestones: [TutorialMilestoneCodes.FirstResourceSold]);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, ClaimMutation, new { input = new { goalCode = FirstWeekCampaignCatalog.FirstSale } }, p.Token);

        Assert.Equal("CAMPAIGN_NOT_STARTED", result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Claim_ForAForeignOrMissingCompany_IsTheSameUniformForbidden_AndNeverPays()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var actor = await SeedPlayerAsync(factory, client, TimeSpan.FromHours(2), milestones: [TutorialMilestoneCodes.FirstResourceSold]);
        var other = await SeedPlayerAsync(factory, client, TimeSpan.FromHours(2));

        var foreign = await TestHelpers.ExecuteGraphQlAsync(
            client, ClaimMutation, new { input = new { goalCode = FirstWeekCampaignCatalog.FirstSale, companyId = other.CompanyId } }, actor.Token);
        var missing = await TestHelpers.ExecuteGraphQlAsync(
            client, ClaimMutation, new { input = new { goalCode = FirstWeekCampaignCatalog.FirstSale, companyId = Guid.NewGuid() } }, actor.Token);

        var f = foreign.GetProperty("errors")[0];
        var m = missing.GetProperty("errors")[0];
        Assert.Equal("FORBIDDEN", f.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(f.GetProperty("extensions").GetRawText(), m.GetProperty("extensions").GetRawText());
        Assert.Equal(f.GetProperty("message").GetString(), m.GetProperty("message").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1_000m, (await db.BankAccounts.AsNoTracking().SingleAsync(a => a.Id == other.AccountId)).Balance);
    }

    [Fact]
    public async Task Claim_CreatesTheUsdAccountWhenTheCompanyHasNone()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var p = await SeedPlayerAsync(factory, client, TimeSpan.FromHours(2), milestones: [TutorialMilestoneCodes.FirstResourceSold]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.BankAccounts.SingleAsync(a => a.Id == p.AccountId)).CurrencyCode = "EUR";
            await db.SaveChangesAsync();
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(client, ClaimMutation, new { input = new { goalCode = FirstWeekCampaignCatalog.FirstSale } }, p.Token);

        Assert.True(result.GetProperty("data").GetProperty("claimCampaignReward").GetProperty("claimed").GetBoolean());
        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var usd = await verifyDb.BankAccounts.AsNoTracking().SingleAsync(a => a.CompanyId == p.CompanyId && a.CurrencyCode == "USD");
        Assert.Equal(2_000m, usd.Balance);
    }
}
