using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// For every resolver that loads an object by caller-supplied id and then checks ownership, a missing
/// id and an id owned by someone else must be indistinguishable to the caller (issue #392).
/// </summary>
public sealed class ObjectAuthorizationParityTests
{
    private const string FriendlyMessage = "You don't have permission to perform this action.";

    private static async Task<string> RegisterAsync(HttpClient client, string prefix)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email = $"{prefix}-{Guid.NewGuid():N}@test.com", displayName = prefix, password = "Password1!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> PlayerIdAsync(HttpClient client, string token)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static async Task<Guid> CreateCompanyAsync(ApiWebApplicationFactory factory, Guid ownerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tick = await db.GameStates.Select(state => (long?)state.CurrentTick).FirstOrDefaultDeterministicAsync() ?? 0L;
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = ownerId,
            Name = $"Parity-{Guid.NewGuid():N}",
            TotalSharesIssued = 1_000m,
            FoundedAtTick = tick,
            FoundedAtUtc = DateTime.UtcNow,
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private static async Task<Guid> CreateDepositAsync(ApiWebApplicationFactory factory, Guid companyId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L).ToString("D16"),
            CurrencyCode = "EUR",
            Balance = 0m,
            CompanyId = companyId,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    private static void AssertUniformDenial(JsonElement result)
    {
        var error = result.GetProperty("errors")[0];
        Assert.Equal("FORBIDDEN", error.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(FriendlyMessage, error.GetProperty("message").GetString());
    }

    public static TheoryData<string> Operations =>
    [
        "salary",
        "dividend",
        "mergeDestination",
        "mergeTarget",
        "myTradeRoutes",
        "tradeRoute",
        "closeCompanyBankAccount",
    ];

    /// <summary>
    /// Operations whose "foreign" case is a company/account owned by another player. Excluded: mergeTarget
    /// (any company the caller holds shares in is a valid target, judged by shareholding) and tradeRoute
    /// (needs a full building graph; its denial branch shares the code path of myTradeRoutes).
    /// </summary>
    public static TheoryData<string> ForeignOperations =>
    [
        "salary",
        "dividend",
        "mergeDestination",
        "myTradeRoutes",
        "closeCompanyBankAccount",
    ];

    private static (string Document, object Variables) Build(string op, Guid objectId)
    {
        return op switch
        {
            "salary" => (
                "mutation M($i: SetCompanySalaryLevelInput!) { setCompanySalaryLevel(input: $i) { __typename } }",
                new { i = new { companyId = objectId, cityId = Guid.NewGuid(), salaryLevel = "STANDARD" } }),
            "dividend" => (
                "mutation M($i: ProposeDividendInput!) { proposeDividend(input: $i) { id } }",
                new { i = new { companyId = objectId, dividendPercent = 10m } }),
            "mergeDestination" => (
                "mutation M($i: MergeCompanyInput!) { mergeCompany(input: $i) { __typename } }",
                new { i = new { targetCompanyId = Guid.NewGuid(), destinationCompanyId = objectId } }),
            "mergeTarget" => (
                "mutation M($i: MergeCompanyInput!) { mergeCompany(input: $i) { __typename } }",
                new { i = new { targetCompanyId = objectId, destinationCompanyId = Guid.NewGuid() } }),
            "myTradeRoutes" => (
                "query Q($c: UUID) { myTradeRoutes(companyId: $c) { id } }",
                new { c = objectId }),
            "tradeRoute" => (
                "query Q($r: UUID!) { tradeRoute(routeId: $r) { id } }",
                new { r = objectId }),
            "closeCompanyBankAccount" => (
                "mutation M($i: CloseCompanyBankAccountInput!) { closeCompanyBankAccount(input: $i) { id } }",
                new { i = new { bankAccountId = objectId } }),
            _ => throw new ArgumentOutOfRangeException(nameof(op)),
        };
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task UnknownObjectId_IsDeniedWithTheUniformError(string op)
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "probe");

        var (document, variables) = Build(op, Guid.NewGuid());
        var result = await TestHelpers.ExecuteGraphQlAsync(client, document, variables, token);

        AssertUniformDenial(result);
    }

    [Theory]
    [MemberData(nameof(ForeignOperations))]
    public async Task ForeignObjectId_IsDeniedWithTheSameUniformError(string op)
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var ownerToken = await RegisterAsync(client, "owner");
        var attackerToken = await RegisterAsync(client, "attacker");
        var ownerId = await PlayerIdAsync(client, ownerToken);
        var companyId = await CreateCompanyAsync(factory, ownerId);
        var objectId = op == "closeCompanyBankAccount" ? await CreateDepositAsync(factory, companyId) : companyId;

        var (document, variables) = Build(op, objectId);
        var result = await TestHelpers.ExecuteGraphQlAsync(client, document, variables, attackerToken);

        AssertUniformDenial(result);
    }

    [Fact]
    public async Task CreateTradeRoute_UnknownAndForeignCompanies_ReturnTheSamePayloadError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var ownerToken = await RegisterAsync(client, "route-owner");
        var attackerToken = await RegisterAsync(client, "route-attacker");
        var foreignCompanyId = await CreateCompanyAsync(factory, await PlayerIdAsync(client, ownerToken));

        async Task<(string? Code, string? Message)> CreateAsync(Guid companyId)
        {
            var result = await TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation M($i: CreateTradeRouteInput!) { createTradeRoute(input: $i) { isSuccess errorCode errorMessage } }",
                new
                {
                    i = new
                    {
                        companyId,
                        sourceBuildingId = Guid.NewGuid(),
                        sourceBuildingUnitId = Guid.NewGuid(),
                        destinationBuildingId = Guid.NewGuid(),
                        destinationBuildingUnitId = Guid.NewGuid(),
                        quantity = 1m,
                        pricePerUnit = 1m,
                    },
                },
                attackerToken);
            var payload = result.GetProperty("data").GetProperty("createTradeRoute");
            return (payload.GetProperty("errorCode").GetString(), payload.GetProperty("errorMessage").GetString());
        }

        var unknown = await CreateAsync(Guid.NewGuid());
        var foreign = await CreateAsync(foreignCompanyId);

        Assert.Equal("FORBIDDEN", unknown.Code);
        Assert.Equal(FriendlyMessage, unknown.Message);
        Assert.Equal(unknown, foreign);
    }
}
