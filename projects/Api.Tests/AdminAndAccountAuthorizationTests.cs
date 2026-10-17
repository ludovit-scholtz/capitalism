using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Negative coverage for sensitive operations the GraphQL surface inventory flagged (#392): admin-only mutations must reject
/// unauthenticated callers and ordinary players without side effects, and account-targeting mutations must answer missing
/// and foreign objects identically.
/// </summary>
public sealed class AdminAndAccountAuthorizationTests
{
    public static IEnumerable<object[]> AdminMutations() =>
    [
        ["setPlayerInvisibleInChat", "SetPlayerInvisibleInChatInput!", "{ __typename }", "{ \"playerId\": \"{PLAYER}\", \"isInvisibleInChat\": true }", "ADMIN_ACCESS_REQUIRED"],
        ["setLocalGameAdminRole", "SetLocalGameAdminRoleInput!", "{ __typename }", "{ \"playerId\": \"{PLAYER}\", \"isAdmin\": true }", "ROOT_ADMIN_REQUIRED"],
        ["assignGlobalGameAdminRole", "ManageGlobalGameAdminRoleInput!", "{ __typename }", "{ \"email\": \"{EMAIL}\" }", "ROOT_ADMIN_REQUIRED"],
        ["removeGlobalGameAdminRole", "ManageGlobalGameAdminRoleInput!", "", "{ \"email\": \"{EMAIL}\" }", "ROOT_ADMIN_REQUIRED"],
        ["updateRealWorldBillionaire", "UpdateRealWorldBillionaireInput!", "{ __typename }", "{ \"id\": \"{PLAYER}\", \"rank\": 1, \"name\": \"X\", \"wealthUsd\": 1 }", "ADMIN_ACCESS_REQUIRED"],
        ["upsertGameNewsEntry", "UpsertGameNewsEntryInput!", "{ __typename }", "{ \"entryType\": \"NEWS\", \"status\": \"DRAFT\", \"localizations\": [{ \"locale\": \"en\", \"title\": \"T\", \"summary\": \"S\", \"htmlContent\": \"<p>x</p>\" }] }", "ADMIN_ACCESS_REQUIRED"],
        ["startAdminImpersonation", "StartAdminImpersonationInput!", "{ __typename }", "{ \"targetPlayerId\": \"{PLAYER}\", \"accountType\": \"PERSON\" }", "ADMIN_ACCESS_REQUIRED"],
        ["adminSetPlayerGoldBalance", "AdminSetPlayerGoldBalanceInput!", "{ __typename }", "{ \"playerEmail\": \"{EMAIL}\", \"balance\": 1000000, \"note\": \"grant\" }", "ADMIN_REQUIRED"],
    ];

    private static async Task<(string Token, Guid PlayerId, string Email)> RegisterAsync(HttpClient client)
    {
        var email = $"authz-{Guid.NewGuid():N}@example.com";
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Authz", password = "TestPass123!" } });
        var token = result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var me = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return (token, Guid.Parse(me.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!), email);
    }

    private static Task<JsonElement> Run(HttpClient client, string mutation, string inputType, string selection, string inputJson, Guid playerId, string email, string? token)
    {
        var json = inputJson.Replace("{PLAYER}", playerId.ToString()).Replace("{EMAIL}", email);
        var query = $"mutation($input: {inputType}) {{ {mutation}(input: $input) {selection} }}";
        return TestHelpers.ExecuteGraphQlAsync(client, query, new { input = JsonSerializer.Deserialize<JsonElement>(json) }, token);
    }

    [Theory]
    [MemberData(nameof(AdminMutations))]
    public async Task AdminMutation_Unauthenticated_IsRejected(string mutation, string inputType, string selection, string inputJson, string playerErrorCode)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (_, playerId, email) = await RegisterAsync(client);

        var result = await Run(client, mutation, inputType, selection, inputJson, playerId, email, token: null);

        // The specific authentication code proves the guard ran (a schema/validation error would carry a different code).
        Assert.True(result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0, $"{mutation} accepted an anonymous caller");
        Assert.Equal("AUTH_NOT_AUTHENTICATED", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Theory]
    [MemberData(nameof(AdminMutations))]
    public async Task AdminMutation_OrdinaryPlayer_IsRejectedWithoutSideEffects(string mutation, string inputType, string selection, string inputJson, string playerErrorCode)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, playerId, email) = await RegisterAsync(client);

        var result = await Run(client, mutation, inputType, selection, inputJson, playerId, email, token);

        Assert.True(result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0, $"{mutation} accepted an ordinary player");
        Assert.Equal(playerErrorCode, errors[0].GetProperty("extensions").GetProperty("code").GetString());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.AsNoTracking().FirstAsync(p => p.Id == playerId);
        Assert.Equal(PlayerRole.Player, player.Role);
        Assert.False(player.IsInvisibleInChat);
    }

    [Fact]
    public async Task StopAdminImpersonation_Unauthenticated_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(client, "mutation { stopAdminImpersonation { __typename } }");

        Assert.True(result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task CreatePersonalBankAccount_Unauthenticated_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: CreatePersonalBankAccountInput!) { createPersonalBankAccount(input: $input) { __typename } }",
            new { input = new { currencyCode = "EUR" } });

        Assert.True(result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0);
    }

    private const string TopUpQuery = "mutation($input: TopUpDepositInput!) { topUpDeposit(input: $input) { __typename } }";

    [Fact]
    public async Task TopUpDeposit_Unauthenticated_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(client, TopUpQuery, new { input = new { depositId = Guid.NewGuid(), amount = 10m } });

        Assert.Equal("AUTH_NOT_AUTHENTICATED", result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TopUpDeposit_IsADisabledStub_ForAuthenticatedPlayers()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, _, _) = await RegisterAsync(client);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, TopUpQuery, new { input = new { depositId = Guid.NewGuid(), amount = 10m } }, token);

        Assert.Equal("USE_FOREX_TRANSFER", result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SetBankAccountAlertThreshold_MissingAndForeignAccount_ReturnIdenticalForbidden()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, _, _) = await RegisterAsync(client);
        var (_, ownerId, _) = await RegisterAsync(client);
        Guid foreignAccountId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var foreign = new BankAccount
            {
                Id = Guid.NewGuid(), PlayerId = ownerId, CurrencyCode = "EUR", Balance = 10m,
                AccountNumber = Guid.NewGuid().ToString("N")[..16], CreatedAtUtc = DateTime.UtcNow,
            };
            db.BankAccounts.Add(foreign);
            await db.SaveChangesAsync();
            foreignAccountId = foreign.Id;
        }

        const string query = "mutation($input: SetBankAccountAlertThresholdInput!) { setBankAccountAlertThreshold(input: $input) { bankAccountId } }";
        var foreignResult = await TestHelpers.ExecuteGraphQlAsync(client, query, new { input = new { bankAccountId = foreignAccountId, minBalanceThreshold = 5m } }, token);
        var missingResult = await TestHelpers.ExecuteGraphQlAsync(client, query, new { input = new { bankAccountId = Guid.NewGuid(), minBalanceThreshold = 5m } }, token);

        var foreignError = foreignResult.GetProperty("errors")[0];
        var missingError = missingResult.GetProperty("errors")[0];
        Assert.Equal("FORBIDDEN", foreignError.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(foreignError.GetProperty("extensions").GetProperty("code").GetString(), missingError.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(foreignError.GetProperty("message").GetString(), missingError.GetProperty("message").GetString());
        await using var verify = factory.Services.CreateAsyncScope();
        var untouched = await verify.ServiceProvider.GetRequiredService<AppDbContext>().BankAccounts.AsNoTracking().FirstAsync(a => a.Id == foreignAccountId);
        Assert.Null(untouched.AlertMinBalanceThreshold);
    }

    [Fact]
    public async Task SetBankAccountAlertThreshold_OwnerCanSetTheirOwnThreshold()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, playerId, _) = await RegisterAsync(client);
        Guid accountId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var own = new BankAccount
            {
                Id = Guid.NewGuid(), PlayerId = playerId, CurrencyCode = "EUR", Balance = 10m,
                AccountNumber = Guid.NewGuid().ToString("N")[..16], CreatedAtUtc = DateTime.UtcNow,
            };
            db.BankAccounts.Add(own);
            await db.SaveChangesAsync();
            accountId = own.Id;
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: SetBankAccountAlertThresholdInput!) { setBankAccountAlertThreshold(input: $input) { bankAccountId alertMinBalanceThreshold } }",
            new { input = new { bankAccountId = accountId, minBalanceThreshold = 5m } },
            token);

        Assert.False(result.TryGetProperty("errors", out var errors), $"unexpected errors: {errors}");
        Assert.Equal(5m, result.GetProperty("data").GetProperty("setBankAccountAlertThreshold").GetProperty("alertMinBalanceThreshold").GetDecimal());
    }
}
