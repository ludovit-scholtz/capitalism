using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// #659: lot purchase, onboarding finish and API-key company binding used to answer a foreign or missing company with their own
/// error and no audit event. They now behave like every other owner-checked lookup: one uniform error for both cases and a recorded
/// denial carrying the real reason.
/// </summary>
public sealed class CompanyLookupDenialAuditTests
{
    private const string PurchaseLotMutation = """
        mutation($input: PurchaseLotInput!) { purchaseLot(input: $input) { lot { id } } }
        """;

    private const string GenerateApiKeyMutation = """
        mutation($input: GenerateApiKeyInput!) { generateApiKey(input: $input) { plaintextKey } }
        """;

    private const string FinishOnboardingMutation = """
        mutation($input: FinishOnboardingInput!) { finishOnboarding(input: $input) { __typename } }
        """;

    [Fact]
    public async Task PurchaseLot_ForeignAndMissingCompany_AreUniformAndAudited()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client);
        var foreignCompanyId = await SeedForeignCompanyAsync(factory, client);

        var foreign = await TestHelpers.ExecuteGraphQlAsync(client, PurchaseLotMutation, PurchaseInput(foreignCompanyId), token);
        var missing = await TestHelpers.ExecuteGraphQlAsync(client, PurchaseLotMutation, PurchaseInput(Guid.NewGuid()), token);

        AssertUniformDenial(foreign, missing);
        AssertDenials(factory, playerId, ObjectAuthorizationService.NotOwnedReason, ObjectAuthorizationService.NotFoundReason);
    }

    [Fact]
    public async Task GenerateApiKey_ForeignAndMissingCompany_AreUniformAndAudited()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client);
        var foreignCompanyId = await SeedForeignCompanyAsync(factory, client);

        var foreign = await TestHelpers.ExecuteGraphQlAsync(client, GenerateApiKeyMutation, ApiKeyInput(foreignCompanyId), token);
        var missing = await TestHelpers.ExecuteGraphQlAsync(client, GenerateApiKeyMutation, ApiKeyInput(Guid.NewGuid()), token);

        AssertUniformDenial(foreign, missing);
        AssertDenials(factory, playerId, ObjectAuthorizationService.NotOwnedReason, ObjectAuthorizationService.NotFoundReason);
    }

    [Fact]
    public async Task GenerateApiKey_MixedOwnedAndForeignCompany_AuditsEachBadId()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client);
        var ownCompanyId = await SeedCompanyAsync(factory, playerId);
        var foreignCompanyId = await SeedForeignCompanyAsync(factory, client);
        var missingCompanyId = Guid.NewGuid();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            GenerateApiKeyMutation,
            new { input = new { name = "bot", scopes = new[] { "company-bound" }, companyIds = new[] { ownCompanyId, foreignCompanyId, missingCompanyId } } },
            token);

        Assert.Equal(ObjectAuthorizationService.NotFoundOrNotOwnedCode, ErrorCode(result));
        var denials = factory.Services.GetRequiredService<ObjectDenialRecorder>().Recent(10).Where(d => d.ActorUserId == playerId).ToList();
        Assert.Equal(2, denials.Count);
        Assert.Contains(denials, d => d.ObjectId == foreignCompanyId && d.Reason == ObjectAuthorizationService.NotOwnedReason);
        Assert.Contains(denials, d => d.ObjectId == missingCompanyId && d.Reason == ObjectAuthorizationService.NotFoundReason);
    }

    [Fact]
    public async Task GenerateApiKey_OwnCompany_StillSucceeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client);
        var ownCompanyId = await SeedCompanyAsync(factory, playerId);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, GenerateApiKeyMutation, ApiKeyInput(ownCompanyId), token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        Assert.Empty(factory.Services.GetRequiredService<ObjectDenialRecorder>().Recent(10).Where(d => d.ActorUserId == playerId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FinishOnboarding_ForeignOrMissingOnboardingCompany_IsUniformAndAudited(bool foreignCompany)
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client);
        var companyId = foreignCompany ? await SeedForeignCompanyAsync(factory, client) : Guid.NewGuid();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(p => p.Id == playerId);
            player.OnboardingCurrentStep = OnboardingProgressStep.ShopSelection;
            player.OnboardingIndustry = "FOOD";
            player.OnboardingCityId = Guid.NewGuid();
            player.OnboardingFactoryLotId = Guid.NewGuid();
            player.OnboardingCompanyId = companyId;
            await db.SaveChangesAsync();
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client, FinishOnboardingMutation, new { input = new { productTypeId = Guid.NewGuid(), shopLotId = Guid.NewGuid() } }, token);

        Assert.Equal(ObjectAuthorizationService.NotFoundOrNotOwnedCode, ErrorCode(result));
        var denial = Assert.Single(factory.Services.GetRequiredService<ObjectDenialRecorder>().Recent(10).Where(d => d.ActorUserId == playerId));
        Assert.Equal("company", denial.ObjectType);
        Assert.Equal(companyId, denial.ObjectId);
        Assert.Equal(foreignCompany ? ObjectAuthorizationService.NotOwnedReason : ObjectAuthorizationService.NotFoundReason, denial.Reason);
    }

    private static object PurchaseInput(Guid companyId) =>
        new { input = new { companyId, lotId = Guid.NewGuid(), buildingType = "FACTORY" } };

    private static object ApiKeyInput(Guid companyId) =>
        new { input = new { name = "bot", scopes = new[] { "company-bound" }, companyIds = new[] { companyId } } };

    private static string? ErrorCode(System.Text.Json.JsonElement result) =>
        result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString();

    private static void AssertUniformDenial(System.Text.Json.JsonElement foreign, System.Text.Json.JsonElement missing)
    {
        Assert.Equal(ObjectAuthorizationService.NotFoundOrNotOwnedCode, ErrorCode(foreign));
        Assert.Equal(ErrorCode(foreign), ErrorCode(missing));
        Assert.Equal(
            foreign.GetProperty("errors")[0].GetProperty("message").GetString(),
            missing.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    private static void AssertDenials(ApiWebApplicationFactory factory, Guid playerId, params string[] expectedReasons)
    {
        var denials = factory.Services.GetRequiredService<ObjectDenialRecorder>().Recent(10).Where(d => d.ActorUserId == playerId).ToList();
        Assert.Equal(expectedReasons.Length, denials.Count);
        Assert.All(denials, denial => Assert.Equal("company", denial.ObjectType));
        Assert.Equal(expectedReasons.OrderBy(r => r), denials.Select(d => d.Reason).OrderBy(r => r));
    }

    private static async Task<(string Token, Guid PlayerId)> RegisterAsync(HttpClient client)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token player { id } } }",
            new { input = new { email = $"denial-{Guid.NewGuid():N}@test.com", displayName = "Denial Audit", password = "TestPass123!" } });
        var register = result.GetProperty("data").GetProperty("register");
        return (register.GetProperty("token").GetString()!, Guid.Parse(register.GetProperty("player").GetProperty("id").GetString()!));
    }

    private static async Task<Guid> SeedForeignCompanyAsync(ApiWebApplicationFactory factory, HttpClient client)
    {
        var (_, otherPlayerId) = await RegisterAsync(client);
        return await SeedCompanyAsync(factory, otherPlayerId);
    }

    private static async Task<Guid> SeedCompanyAsync(ApiWebApplicationFactory factory, Guid playerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = playerId, Name = $"Co {Guid.NewGuid():N}", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }
}
