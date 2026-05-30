using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class AccountPurgeGraphQlIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    public AccountPurgeGraphQlIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query, object? variables = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null)
        => ExecuteGraphQlAsync(_client, query, variables);

    [Fact]
    public async Task PurgePlayerAccountFromMaster_ExistingShardPlayer_RemovesPlayerAndTransfersBanksToGovernment()
    {
        var playerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var bankId = Guid.NewGuid();
        var factoryId = Guid.NewGuid();
        var depositorAccountId = Guid.NewGuid();
        var playerEmail = $"purge-player-{Guid.NewGuid():N}@example.com";
        Guid governmentCompanyId;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            governmentCompanyId = await db.Companies
                .Where(company => company.Player.Email == GovernmentActorConstants.GovernmentEmail)
                .Select(company => company.Id)
                .FirstAsync();

            db.Players.Add(new Player
            {
                Id = playerId,
                Email = playerEmail,
                DisplayName = "Purge Me",
                PasswordHash = "hash",
                Role = PlayerRole.Player,
            });
            db.Companies.Add(new Company { Id = companyId, PlayerId = playerId, Name = "Purge Co", FoundedAtTick = 0 });
            db.Cities.Add(new City { Id = cityId, Name = "Purge City", CurrencyCode = "EUR" });
            db.Buildings.Add(new Building
            {
                Id = bankId,
                CompanyId = companyId,
                CityId = cityId,
                Type = BuildingType.Bank,
                Name = "Purge Bank",
                DepositInterestRatePercent = 8m,
                LendingInterestRatePercent = 5m,
                PendingDepositInterestRatePercent = 9m,
                PendingDepositRateEffectiveTick = 100,
                IsGovernmentOwned = false,
            });
            db.Buildings.Add(new Building
            {
                Id = factoryId,
                CompanyId = companyId,
                CityId = cityId,
                Type = BuildingType.Factory,
                Name = "Purge Factory",
            });
            db.BuildingUnits.Add(new BuildingUnit { Id = Guid.NewGuid(), BuildingId = factoryId, GridX = 0, GridY = 0, UnitType = "MANUFACTURING" });
            db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), BuildingId = factoryId, Quantity = 10m });
            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = "1111111111111111",
                CurrencyCode = "EUR",
                CompanyId = companyId,
                BankBuildingId = bankId,
                Balance = 1000m,
            });
            db.BankAccounts.Add(new BankAccount
            {
                Id = depositorAccountId,
                AccountNumber = "2222222222222222",
                CurrencyCode = "EUR",
                CompanyId = governmentCompanyId,
                BankBuildingId = bankId,
                Balance = 500m,
            });
            await db.SaveChangesAsync();
        }

        const string mutation = """
            mutation PurgePlayerAccountFromMaster($input: PurgePlayerAccountFromMasterInput!) {
                purgePlayerAccountFromMaster(input: $input) {
                    playerFound
                    companiesRemoved
                    buildingsDestroyed
                    banksTransferredToGovernment
                }
            }
            """;

        var result = await ExecuteGraphQlAsync(mutation, new
        {
            input = new
            {
                registrationKey = "local-master-registration-key",
                serverKey = "capitalism-local",
                playerEmail,
            },
        });

        Assert.False(result.TryGetProperty("errors", out _));
        var payload = result.GetProperty("data").GetProperty("purgePlayerAccountFromMaster");
        Assert.True(payload.GetProperty("playerFound").GetBoolean());
        Assert.Equal(1, payload.GetProperty("companiesRemoved").GetInt32());
        Assert.Equal(1, payload.GetProperty("buildingsDestroyed").GetInt32());
        Assert.Equal(1, payload.GetProperty("banksTransferredToGovernment").GetInt32());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Players.AnyAsync(player => player.Id == playerId));
            Assert.False(await db.Companies.AnyAsync(company => company.Id == companyId));
            Assert.False(await db.Buildings.AnyAsync(building => building.Id == factoryId));
            Assert.False(await db.BuildingUnits.AnyAsync(unit => unit.BuildingId == factoryId));
            Assert.False(await db.Inventories.AnyAsync(inventory => inventory.BuildingId == factoryId));

            var bank = await db.Buildings.SingleAsync(building => building.Id == bankId);
            Assert.Equal(governmentCompanyId, bank.CompanyId);
            Assert.True(bank.IsGovernmentOwned);
            Assert.Equal(0m, bank.DepositInterestRatePercent);
            Assert.Equal(20m, bank.LendingInterestRatePercent);
            Assert.Null(bank.PendingDepositInterestRatePercent);
            Assert.Null(bank.PendingDepositRateEffectiveTick);
            Assert.True(await db.BankAccounts.AnyAsync(account => account.Id == depositorAccountId));
        }
    }

    [Fact]
    public async Task PurgePlayerAccountFromMaster_Unauthenticated_WrongRegistrationKey_ReturnsInvalidRegistrationKeyError()
    {
        const string mutation = """
            mutation PurgePlayerAccountFromMaster($input: PurgePlayerAccountFromMasterInput!) {
                purgePlayerAccountFromMaster(input: $input) {
                    playerFound
                }
            }
            """;

        var result = await ExecuteGraphQlAsync(mutation, new
        {
            input = new
            {
                registrationKey = "completely-wrong-registration-key",
                serverKey = "capitalism-local",
                playerEmail = "any@example.com",
            },
        });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.NotEqual(0, errors.GetArrayLength());
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INVALID_REGISTRATION_KEY", code);
    }
}
