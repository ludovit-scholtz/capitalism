using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// #392 / #605: resolvers that take a company id must answer a missing company and a company owned by
/// somebody else with the identical FORBIDDEN response, so company ids cannot be enumerated.
/// </summary>
public sealed class CompanyOwnershipDenialParityTests
{
    public static IEnumerable<object[]> Operations() =>
    [
        ["createCompanyBankAccount"],
        ["openBankAccount"],
        ["acceptLoan"],
        ["makeOfferOnBuilding"],
        ["bankStatement"],
        ["assignBuildingBankAccount"],
    ];

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task MissingAndForeignCompany_ReturnIdenticalForbidden(string operation)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var actorToken = await RegisterAsync(client, $"own-actor-{Guid.NewGuid():N}@example.com");
        var ownerToken = await RegisterAsync(client, $"own-owner-{Guid.NewGuid():N}@example.com");
        var ownerId = await GetMeIdAsync(client, ownerToken);
        var actorId = await GetMeIdAsync(client, actorToken);
        var world = await SeedAsync(factory, ownerId, actorId);

        var foreign = await Execute(client, operation, world, foreignCompany: true, actorToken);
        var missing = await Execute(client, operation, world, foreignCompany: false, actorToken);

        var foreignError = foreign.GetProperty("errors")[0];
        var missingError = missing.GetProperty("errors")[0];
        Assert.Equal("FORBIDDEN", foreignError.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(
            foreignError.GetProperty("extensions").GetProperty("code").GetString(),
            missingError.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(foreignError.GetProperty("message").GetString(), missingError.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Owner_CanStillCreateAccountAndReadStatementForOwnCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var actorToken = await RegisterAsync(client, $"own-ok-actor-{Guid.NewGuid():N}@example.com");
        var ownerToken = await RegisterAsync(client, $"own-ok-owner-{Guid.NewGuid():N}@example.com");
        var world = await SeedAsync(factory, await GetMeIdAsync(client, ownerToken), await GetMeIdAsync(client, actorToken));

        var created = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: CreateCompanyBankAccountInput!) { createCompanyBankAccount(input: $input) { __typename } }",
            new { input = new { companyId = world.ActorCompanyId, currencyCode = "EUR" } },
            actorToken);
        var statement = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "query($companyId: UUID) { bankStatement(companyId: $companyId) { __typename } }",
            new { companyId = world.ActorCompanyId },
            actorToken);

        Assert.False(created.TryGetProperty("errors", out var createErrors), $"createCompanyBankAccount failed: {createErrors}");
        Assert.False(statement.TryGetProperty("errors", out var statementErrors), $"bankStatement failed: {statementErrors}");
    }

    [Fact]
    public async Task Unauthenticated_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: CreateCompanyBankAccountInput!) { createCompanyBankAccount(input: $input) { __typename } }",
            new { input = new { companyId = Guid.NewGuid(), currencyCode = "EUR" } });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    private static Task<JsonElement> Execute(HttpClient client, string operation, World w, bool foreignCompany, string token)
    {
        var companyId = foreignCompany ? w.ForeignCompanyId : Guid.NewGuid();
        return operation switch
        {
            "createCompanyBankAccount" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: CreateCompanyBankAccountInput!) { createCompanyBankAccount(input: $input) { __typename } }",
                new { input = new { companyId, currencyCode = "EUR" } },
                token),
            "openBankAccount" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: OpenBankAccountInput!) { openBankAccount(input: $input) { __typename } }",
                new { input = new { bankBuildingId = Guid.NewGuid(), depositorCompanyId = companyId, amount = 0m } },
                token),
            "acceptLoan" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: AcceptLoanInput!) { acceptLoan(input: $input) { __typename } }",
                new { input = new { loanOfferId = Guid.NewGuid(), borrowerCompanyId = companyId, principalAmount = 1000m } },
                token),
            "makeOfferOnBuilding" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { __typename } }",
                new { input = new { buildingId = w.ListedBuildingId, buyerCompanyId = companyId, offeredPrice = 1000m } },
                token),
            "bankStatement" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "query($companyId: UUID) { bankStatement(companyId: $companyId) { __typename } }",
                new { companyId },
                token),
            "assignBuildingBankAccount" => TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: AssignBuildingBankAccountInput!) { assignBuildingBankAccount(input: $input) { __typename } }",
                new { input = new { buildingId = w.ActorBuildingId, bankAccountId = foreignCompany ? w.ForeignAccountId : Guid.NewGuid() } },
                token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null),
        };
    }

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
        var foreignAccount = new BankAccount
        {
            Id = Guid.NewGuid(), CompanyId = foreignCompany.Id, CurrencyCode = city.CurrencyCode, Balance = 1000m,
            AccountNumber = "1000000000000001", CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(foreignAccount);
        var listed = new Building
        {
            Id = Guid.NewGuid(), CompanyId = foreignCompany.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Listed",
            Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow, IsForSale = true, AskingPrice = 5000m,
        };
        var actorBuilding = new Building
        {
            Id = Guid.NewGuid(), CompanyId = actorCompany.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Mine",
            Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.AddRange(listed, actorBuilding);
        await db.SaveChangesAsync();
        return new World(foreignCompany.Id, foreignAccount.Id, listed.Id, actorBuilding.Id, actorCompany.Id);
    }

    private sealed record World(Guid ForeignCompanyId, Guid ForeignAccountId, Guid ListedBuildingId, Guid ActorBuildingId, Guid ActorCompanyId);
}
