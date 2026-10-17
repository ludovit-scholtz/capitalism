using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// #392: closeBankAccount, setMediaHouseContentBudget, upgradeMediaHouse and cityUnlockStatuses must answer a missing
/// object and an object owned by somebody else with the identical FORBIDDEN response (no id enumeration), while the
/// owner still passes the ownership check.
/// </summary>
public sealed class RemainingObjectDenialParityTests
{
    public static IEnumerable<object[]> Operations() =>
    [
        ["closeBankAccount"],
        ["setMediaHouseContentBudget"],
        ["upgradeMediaHouse"],
        ["cityUnlockStatuses"],
    ];

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task MissingAndForeignObject_ReturnIdenticalForbidden(string operation)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var actorToken = await RegisterAsync(client, $"rem-actor-{Guid.NewGuid():N}@example.com");
        var ownerToken = await RegisterAsync(client, $"rem-owner-{Guid.NewGuid():N}@example.com");
        var world = await SeedAsync(factory, await GetMeIdAsync(client, ownerToken), await GetMeIdAsync(client, actorToken));

        var foreign = await ExecuteAsync(client, operation, world.ForeignDepositId, world.ForeignMediaHouseId, world.ForeignCompanyId, actorToken);
        var missing = await ExecuteAsync(client, operation, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), actorToken);

        var foreignError = foreign.GetProperty("errors")[0];
        var missingError = missing.GetProperty("errors")[0];
        Assert.Equal("FORBIDDEN", foreignError.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(
            foreignError.GetProperty("extensions").GetProperty("code").GetString(),
            missingError.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(foreignError.GetProperty("message").GetString(), missingError.GetProperty("message").GetString());
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Unauthenticated_IsRejected(string operation)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await ExecuteAsync(client, operation, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), token: null);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
        Assert.NotEqual("FORBIDDEN", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Owner_PassesTheOwnershipCheckOnTheirOwnObjects()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var actorToken = await RegisterAsync(client, $"rem-ok-actor-{Guid.NewGuid():N}@example.com");
        var ownerToken = await RegisterAsync(client, $"rem-ok-owner-{Guid.NewGuid():N}@example.com");
        var world = await SeedAsync(factory, await GetMeIdAsync(client, ownerToken), await GetMeIdAsync(client, actorToken));

        var budget = await ExecuteAsync(client, "setMediaHouseContentBudget", Guid.NewGuid(), world.ActorMediaHouseId, Guid.NewGuid(), actorToken);
        var unlocks = await ExecuteAsync(client, "cityUnlockStatuses", Guid.NewGuid(), Guid.NewGuid(), world.ActorCompanyId, actorToken);
        var close = await ExecuteAsync(client, "closeBankAccount", world.ActorDepositId, Guid.NewGuid(), Guid.NewGuid(), actorToken);

        Assert.False(IsForbidden(budget), $"owner was denied setMediaHouseContentBudget: {budget}");
        Assert.False(IsForbidden(unlocks), $"owner was denied cityUnlockStatuses: {unlocks}");
        Assert.False(IsForbidden(close), $"owner was denied closeBankAccount: {close}");
    }

    private static bool IsForbidden(JsonElement result)
        => result.TryGetProperty("errors", out var errors)
           && errors.GetArrayLength() > 0
           && errors[0].TryGetProperty("extensions", out var ext)
           && ext.TryGetProperty("code", out var code)
           && code.GetString() == "FORBIDDEN";

    private static Task<JsonElement> ExecuteAsync(HttpClient client, string operation, Guid depositId, Guid buildingId, Guid companyId, string? token)
        => operation switch
        {
            "closeBankAccount" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: CloseBankAccountInput!) { closeBankAccount(input: $input) { __typename } }",
                new { input = new { depositId, amount = 0m } },
                token),
            "setMediaHouseContentBudget" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: SetMediaHouseContentBudgetInput!) { setMediaHouseContentBudget(input: $input) { id } }",
                new { input = new { buildingId, contentBudgetPerTick = 1m } },
                token),
            "upgradeMediaHouse" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: UpgradeMediaHouseInput!) { upgradeMediaHouse(input: $input) { id } }",
                new { input = new { buildingId } },
                token),
            "cityUnlockStatuses" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "query($companyId: UUID) { cityUnlockStatuses(companyId: $companyId) { __typename } }",
                new { companyId },
                token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Parity", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetMeIdAsync(HttpClient client, string token)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static async Task<World> SeedAsync(ApiWebApplicationFactory factory, Guid ownerId, Guid actorId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var foreignCompany = new Company { Id = Guid.NewGuid(), PlayerId = ownerId, Name = "Foreign Co", FoundedAtUtc = DateTime.UtcNow };
        var actorCompany = new Company { Id = Guid.NewGuid(), PlayerId = actorId, Name = "Actor Co", FoundedAtUtc = DateTime.UtcNow };
        db.Companies.AddRange(foreignCompany, actorCompany);

        Building NewBuilding(Company company, string type, string name) => new()
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = type, Name = name,
            Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow,
        };

        var foreignMedia = NewBuilding(foreignCompany, BuildingType.MediaHouse, "Foreign Media");
        var actorMedia = NewBuilding(actorCompany, BuildingType.MediaHouse, "My Media");
        var bank = NewBuilding(foreignCompany, BuildingType.Bank, "Bank");
        db.Buildings.AddRange(foreignMedia, actorMedia, bank);

        BankAccount Deposit(Company depositor, string number) => new()
        {
            Id = Guid.NewGuid(), CompanyId = depositor.Id, BankBuildingId = bank.Id, CurrencyCode = city.CurrencyCode,
            Balance = 0m, AccountNumber = number, CreatedAtUtc = DateTime.UtcNow,
        };

        var foreignDeposit = Deposit(foreignCompany, "2000000000000001");
        var actorDeposit = Deposit(actorCompany, "2000000000000002");
        db.BankAccounts.AddRange(foreignDeposit, actorDeposit);
        await db.SaveChangesAsync();
        return new World(foreignCompany.Id, actorCompany.Id, foreignMedia.Id, actorMedia.Id, foreignDeposit.Id, actorDeposit.Id);
    }

    private sealed record World(
        Guid ForeignCompanyId,
        Guid ActorCompanyId,
        Guid ForeignMediaHouseId,
        Guid ActorMediaHouseId,
        Guid ForeignDepositId,
        Guid ActorDepositId);
}
