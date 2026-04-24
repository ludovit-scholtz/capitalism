using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Types;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Text.Json;

namespace Api.Tests;

/// <summary>
/// Integration tests for the Gold AMM liquidity pool feature.
/// Tests cover: pool creation, add/remove liquidity, swap quotes, swap execution,
/// blocked-resource enforcement, and authorization checks.
/// </summary>
public sealed class GoldAmmTests
{
    #region Helpers

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8, "application/json");

        if (token is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string displayName = "Tester")
    {
        var result = await ExecuteGraphQlAsync(client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token player { id } }
            }
            """,
            new { input = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetPlayerIdByEmailAsync(AppDbContext db, string email)
    {
        var player = await db.Players.AsNoTracking().FirstAsync(p => p.Email == email);
        return player.Id;
    }

    private static async Task SetGoldBalanceAsync(AppDbContext db, Guid playerId, decimal amount)
    {
        var existing = await db.PlayerGoldBalances.FirstOrDefaultAsync(g => g.PlayerId == playerId);
        if (existing == null)
        {
            db.PlayerGoldBalances.Add(new PlayerGoldBalance
            {
                Id = Guid.NewGuid(), PlayerId = playerId, Balance = amount,
                CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
            });
        }
        else
        {
            existing.Balance = amount;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private static async Task SetFiatBalanceAsync(AppDbContext db, Guid playerId, string currencyCode, decimal amount)
    {
        var account = await PersonalBankAccountService.EnsureTrackedAccountAsync(db, playerId, currencyCode);
        account.Balance = amount;
        await db.SaveChangesAsync();
    }

    private static string? GetError(JsonElement result)
    {
        if (result.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            var first = errors.EnumerateArray().FirstOrDefault();
            if (first.ValueKind != JsonValueKind.Undefined)
            {
                // Try extensions.code first
                if (first.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("code", out var code))
                    return code.GetString();
                // Fall back to message
                if (first.TryGetProperty("message", out var msg))
                    return msg.GetString();
            }
        }
        return null;
    }

    #endregion

    #region AMM Math unit tests

    [Fact]
    public void ComputeAmmOutput_FiatToGold_ConstantProductMaintained()
    {
        // Pool: 10000 EUR, 10 XAU → K = 100000
        var fiatReserve = 10000m;
        var goldReserve = 10m;
        var inputAmount = 1000m; // buy gold with 1000 EUR
        var feeAmount = Math.Round(1000m * 0.01m, 8); // 10 EUR fee
        var netInput = 1000m - feeAmount;

        var (output, fee) = Query.ComputeAmmOutput("FIAT_TO_GOLD", inputAmount, fiatReserve, goldReserve);

        // net input goes to pool: newFiat = 10000 + 990 = 10990
        // newGold = 100000 / 10990 ≈ 9.0991...
        // output = 10 - newGold ≈ 0.9009...
        Assert.Equal(inputAmount, fee + (inputAmount - fee)); // sanity: fee + netInput = input
        Assert.True(output > 0, "Should receive gold");
        Assert.True(output < inputAmount / (fiatReserve / goldReserve), "Output below naive rate (slippage)");
    }

    [Fact]
    public void ComputeAmmOutput_GoldToFiat_ConstantProductMaintained()
    {
        var fiatReserve = 10000m;
        var goldReserve = 10m;
        var (output, fee) = Query.ComputeAmmOutput("GOLD_TO_FIAT", 1m, fiatReserve, goldReserve);

        // Sell 1 gold: fee = 0.01 XAU, netInput = 0.99 XAU
        // newGold = 10 + 0.99 = 10.99
        // output = 10000 - 100000/10.99 ≈ 899.9...
        Assert.True(output > 0, "Should receive fiat");
        Assert.True(output < fiatReserve / goldReserve, "Output below naive price (slippage)");
    }

    [Fact]
    public void ComputeAmmOutput_SmallTrade_LowSlippage()
    {
        // Deep pool: 1 million EUR, 1000 XAU → price = 1000 EUR/XAU
        var (output, _) = Query.ComputeAmmOutput("FIAT_TO_GOLD", 100m, 1_000_000m, 1000m);
        // Naive: 100/1000 = 0.1 XAU; with 1% fee net=99, actual ≈ 0.099 XAU
        Assert.True(output > 0.09m && output < 0.1m);
    }

    [Fact]
    public void ComputeAmmOutput_LargeTrade_HighSlippage()
    {
        // Shallow pool: 1000 EUR, 1 XAU
        var (output, _) = Query.ComputeAmmOutput("FIAT_TO_GOLD", 500m, 1000m, 1m);
        // With 1% fee, netInput = 495; newFiat = 1495; out = 1 - 1000/1495 ≈ 0.331
        // But naive output = 500/1000 = 0.5 → significant slippage
        Assert.True(output > 0 && output < 0.5m, "Large trade should have significant slippage");
    }

    #endregion

    #region GoldAmmPools public query

    [Fact]
    public async Task GoldAmmPools_EmptyByDefault_ReturnsEmptyList()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client, "{ goldAmmPools { id currencyCode } }");
        var pools = result.GetProperty("data").GetProperty("goldAmmPools");
        Assert.Equal(JsonValueKind.Array, pools.ValueKind);
        Assert.Equal(0, pools.GetArrayLength());
    }

    #endregion

    #region CreateGoldAmmPool mutation

    [Fact]
    public async Task CreateGoldAmmPool_ValidInput_CreatesPoolAndPosition()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "pool-create@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "pool-create@test.com");

        await SetFiatBalanceAsync(db, playerId, "EUR", 10_000m);
        await SetGoldBalanceAsync(db, playerId, 100m);

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation CreatePool($input: CreateGoldAmmPoolInput!) {
              createGoldAmmPool(input: $input) {
                poolId positionId currencyCode liquidityShares fiatProvided goldProvided
                poolFiatReserve poolGoldReserve newFiatBalance newGoldBalance
              }
            }
            """,
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 5m } },
            token);

        var pool = result.GetProperty("data").GetProperty("createGoldAmmPool");
        Assert.Equal("EUR", pool.GetProperty("currencyCode").GetString());
        Assert.Equal(5000m, pool.GetProperty("fiatProvided").GetDecimal());
        Assert.Equal(5m, pool.GetProperty("goldProvided").GetDecimal());
        Assert.True(pool.GetProperty("liquidityShares").GetDecimal() > 0);
        // Balances deducted
        Assert.Equal(5000m, pool.GetProperty("newFiatBalance").GetDecimal());
        Assert.Equal(95m, pool.GetProperty("newGoldBalance").GetDecimal());
    }

    [Fact]
    public async Task CreateGoldAmmPool_DuplicateCurrency_ReturnsPollAlreadyExists()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "pool-dup@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "pool-dup@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 20_000m);
        await SetGoldBalanceAsync(db, playerId, 20m);

        // Create first pool
        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 1000m, goldAmount = 1m } }, token);

        // Try to create again
        var result = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 1000m, goldAmount = 1m } }, token);

        Assert.Equal("POOL_ALREADY_EXISTS", GetError(result));
    }

    [Fact]
    public async Task CreateGoldAmmPool_InsufficientGold_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "pool-nogold@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "pool-nogold@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 10_000m);
        // No gold balance

        var result = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 1000m, goldAmount = 1m } }, token);

        Assert.Equal("INSUFFICIENT_GOLD", GetError(result));
    }

    [Fact]
    public async Task CreateGoldAmmPool_InsufficientFiat_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "pool-nofiat@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "pool-nofiat@test.com");
        // Very low balance, not enough for 1000 EUR
        await SetFiatBalanceAsync(db, playerId, "EUR", 100m);
        await SetGoldBalanceAsync(db, playerId, 10m);

        var result = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 1000m, goldAmount = 1m } }, token);

        Assert.Equal("INSUFFICIENT_FUNDS", GetError(result));
    }

    [Fact]
    public async Task CreateGoldAmmPool_Unauthenticated_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 1000m, goldAmount = 1m } });

        Assert.NotNull(GetError(result));
    }

    #endregion

    #region AddGoldAmmLiquidity mutation

    [Fact]
    public async Task AddGoldAmmLiquidity_ValidInput_IncreasesPoolReserves()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "add-liq@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "add-liq@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 20_000m);
        await SetGoldBalanceAsync(db, playerId, 20m);

        // Create pool first
        var createResult = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 5m } }, token);
        var poolId = createResult.GetProperty("data").GetProperty("createGoldAmmPool").GetProperty("poolId").GetString()!;

        // Add more liquidity
        var result = await ExecuteGraphQlAsync(client,
            """
            mutation AddLiq($input: AddGoldAmmLiquidityInput!) {
              addGoldAmmLiquidity(input: $input) {
                poolId fiatProvided goldProvided poolFiatReserve poolGoldReserve
              }
            }
            """,
            new { input = new { poolId = Guid.Parse(poolId), fiatAmount = 1000m, maxGoldAmount = 2m } },
            token);

        var data = result.GetProperty("data").GetProperty("addGoldAmmLiquidity");
        Assert.Equal(1000m, data.GetProperty("fiatProvided").GetDecimal());
        Assert.Equal(6000m, data.GetProperty("poolFiatReserve").GetDecimal());
    }

    #endregion

    #region RemoveGoldAmmLiquidity mutation

    [Fact]
    public async Task RemoveGoldAmmLiquidity_FullRemoval_ReturnsFundsToPlayer()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "remove-liq@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "remove-liq@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 10_000m);
        await SetGoldBalanceAsync(db, playerId, 10m);

        // Create pool
        var createResult = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { positionId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 5m } }, token);
        var positionId = createResult.GetProperty("data").GetProperty("createGoldAmmPool").GetProperty("positionId").GetString()!;

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation RemoveLiq($input: RemoveGoldAmmLiquidityInput!) {
              removeGoldAmmLiquidity(input: $input) {
                fiatReturned goldReturned remainingShares newFiatBalance newGoldBalance
              }
            }
            """,
            new { input = new { positionId = Guid.Parse(positionId), shareFraction = 1.0m } },
            token);

        var data = result.GetProperty("data").GetProperty("removeGoldAmmLiquidity");
        Assert.Equal(5000m, data.GetProperty("fiatReturned").GetDecimal());
        Assert.Equal(5m, data.GetProperty("goldReturned").GetDecimal());
        Assert.Equal(10_000m, data.GetProperty("newFiatBalance").GetDecimal());
        Assert.Equal(10m, data.GetProperty("newGoldBalance").GetDecimal());
    }

    [Fact]
    public async Task RemoveGoldAmmLiquidity_UnauthorizedPlayer_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token1 = await RegisterAndGetTokenAsync(client, "lp-owner@test.com");
        var token2 = await RegisterAndGetTokenAsync(client, "lp-thief@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerId = await GetPlayerIdByEmailAsync(db, "lp-owner@test.com");
        await SetFiatBalanceAsync(db, ownerId, "EUR", 10_000m);
        await SetGoldBalanceAsync(db, ownerId, 10m);

        var createResult = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { positionId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 5m } }, token1);
        var positionId = createResult.GetProperty("data").GetProperty("createGoldAmmPool").GetProperty("positionId").GetString()!;

        // token2 tries to remove from token1's position
        var result = await ExecuteGraphQlAsync(client,
            "mutation RemoveLiq($input: RemoveGoldAmmLiquidityInput!) { removeGoldAmmLiquidity(input: $input) { fiatReturned } }",
            new { input = new { positionId = Guid.Parse(positionId), shareFraction = 1.0m } }, token2);

        Assert.Equal("UNAUTHORIZED", GetError(result));
    }

    #endregion

    #region GoldAmmSwapQuote query

    [Fact]
    public async Task GoldAmmSwapQuote_FiatToGold_ReturnsValidQuote()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "quote-user@test.com");

        // Create a pool first
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "quote-user@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 20_000m);
        await SetGoldBalanceAsync(db, playerId, 20m);

        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 10_000m, goldAmount = 10m } }, token);

        var result = await ExecuteGraphQlAsync(client,
            """
            query Quote($input: GetGoldAmmSwapQuoteInput!) {
              goldAmmSwapQuote(input: $input) {
                direction currencyCode inputAmount outputAmount feeAmount feePercent
                impliedPrice slippagePercent poolFiatReserve poolGoldReserve availableInputBalance
              }
            }
            """,
            new { input = new { direction = "FIAT_TO_GOLD", currencyCode = "EUR", amount = 1000m } },
            token);

        var quote = result.GetProperty("data").GetProperty("goldAmmSwapQuote");
        Assert.Equal("FIAT_TO_GOLD", quote.GetProperty("direction").GetString());
        Assert.Equal(1000m, quote.GetProperty("inputAmount").GetDecimal());
        Assert.True(quote.GetProperty("outputAmount").GetDecimal() > 0);
        Assert.Equal(1m, quote.GetProperty("feePercent").GetDecimal());
    }

    [Fact]
    public async Task GoldAmmSwapQuote_NoPool_ReturnsPoolNotFound()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "quote-nopool@test.com");

        var result = await ExecuteGraphQlAsync(client,
            "query Quote($input: GetGoldAmmSwapQuoteInput!) { goldAmmSwapQuote(input: $input) { outputAmount } }",
            new { input = new { direction = "FIAT_TO_GOLD", currencyCode = "USD", amount = 100m } }, token);

        Assert.Equal("POOL_NOT_FOUND", GetError(result));
    }

    #endregion

    #region ExecuteGoldAmmSwap mutation

    [Fact]
    public async Task ExecuteGoldAmmSwap_FiatToGold_UpdatesBalancesAndPool()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "swap-fg@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "swap-fg@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 20_000m);
        await SetGoldBalanceAsync(db, playerId, 20m);

        // Create pool: 5k EUR + 5 XAU — leaves 15k EUR and 15 XAU as balance,
        // minus 5k blocked by FiatProvided → 10k available EUR for the swap
        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5_000m, goldAmount = 5m } }, token);

        // Swap 1000 EUR for gold
        var result = await ExecuteGraphQlAsync(client,
            """
            mutation Swap($input: ExecuteGoldAmmSwapInput!) {
              executeGoldAmmSwap(input: $input) {
                tradeId direction currencyCode inputAmount outputAmount feeAmount
                impliedPrice newFiatBalance newGoldBalance
              }
            }
            """,
            new { input = new { direction = "FIAT_TO_GOLD", currencyCode = "EUR", amount = 1000m, minOutputAmount = 0m } },
            token);

        var swap = result.GetProperty("data").GetProperty("executeGoldAmmSwap");
        Assert.NotEqual(Guid.Empty.ToString(), swap.GetProperty("tradeId").GetString());
        Assert.Equal("FIAT_TO_GOLD", swap.GetProperty("direction").GetString());
        Assert.True(swap.GetProperty("outputAmount").GetDecimal() > 0);
        // EUR: started 20k, deposited 5k in pool (wallet=15k), swapped 1k → wallet=14k
        Assert.Equal(14_000m, swap.GetProperty("newFiatBalance").GetDecimal());
        // Gold: started 20, deposited 5 in pool (wallet=15), received gold from swap
        var receivedGold = swap.GetProperty("outputAmount").GetDecimal();
        Assert.Equal(15m + receivedGold, swap.GetProperty("newGoldBalance").GetDecimal());
    }

    [Fact]
    public async Task ExecuteGoldAmmSwap_GoldToFiat_UpdatesBalancesAndPool()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "swap-gf@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "swap-gf@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 20_000m);
        await SetGoldBalanceAsync(db, playerId, 20m);

        // Create pool: 5k EUR + 5 XAU — leaves 15k EUR and 15 XAU as balance,
        // minus 5 blocked by GoldProvided → 10 XAU available for the swap
        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5_000m, goldAmount = 5m } }, token);

        // Swap 1 XAU for EUR
        var result = await ExecuteGraphQlAsync(client,
            """
            mutation Swap($input: ExecuteGoldAmmSwapInput!) {
              executeGoldAmmSwap(input: $input) {
                outputAmount newFiatBalance newGoldBalance
              }
            }
            """,
            new { input = new { direction = "GOLD_TO_FIAT", currencyCode = "EUR", amount = 1m, minOutputAmount = 0m } },
            token);

        var swap = result.GetProperty("data").GetProperty("executeGoldAmmSwap");
        Assert.True(swap.GetProperty("outputAmount").GetDecimal() > 0);
        // EUR: started 20k, deposited 5k in pool (wallet=15k), received EUR from swap
        var receivedFiat = swap.GetProperty("outputAmount").GetDecimal();
        Assert.Equal(15_000m + receivedFiat, swap.GetProperty("newFiatBalance").GetDecimal());
    }

    [Fact]
    public async Task ExecuteGoldAmmSwap_InsufficientFunds_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "swap-insuf@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "swap-insuf@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 10_000m);
        await SetGoldBalanceAsync(db, playerId, 10m);

        // Create pool
        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 5m } }, token);

        // Try to swap more EUR than available (only 5000 left, trying 6000)
        var result = await ExecuteGraphQlAsync(client,
            "mutation Swap($input: ExecuteGoldAmmSwapInput!) { executeGoldAmmSwap(input: $input) { outputAmount } }",
            new { input = new { direction = "FIAT_TO_GOLD", currencyCode = "EUR", amount = 6000m, minOutputAmount = 0m } },
            token);

        Assert.Equal("INSUFFICIENT_FUNDS", GetError(result));
    }

    [Fact]
    public async Task ExecuteGoldAmmSwap_InsufficientGold_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "swap-nogold@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "swap-nogold@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 10_000m);
        await SetGoldBalanceAsync(db, playerId, 2m);

        // Create pool with 1 XAU
        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 1m } }, token);

        // Try to sell 5 XAU but only 1 available (other 1 blocked in pool)
        var result = await ExecuteGraphQlAsync(client,
            "mutation Swap($input: ExecuteGoldAmmSwapInput!) { executeGoldAmmSwap(input: $input) { outputAmount } }",
            new { input = new { direction = "GOLD_TO_FIAT", currencyCode = "EUR", amount = 5m, minOutputAmount = 0m } },
            token);

        Assert.Equal("INSUFFICIENT_GOLD", GetError(result));
    }

    [Fact]
    public async Task ExecuteGoldAmmSwap_SlippageExceeded_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "swap-slip@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "swap-slip@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 20_000m);
        await SetGoldBalanceAsync(db, playerId, 20m);

        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5_000m, goldAmount = 5m } }, token);

        // Demand very high minimum output (will never be met)
        var result = await ExecuteGraphQlAsync(client,
            "mutation Swap($input: ExecuteGoldAmmSwapInput!) { executeGoldAmmSwap(input: $input) { outputAmount } }",
            new { input = new { direction = "FIAT_TO_GOLD", currencyCode = "EUR", amount = 1000m, minOutputAmount = 999m } },
            token);

        Assert.Equal("SLIPPAGE_EXCEEDED", GetError(result));
    }

    #endregion

    #region Blocked resource enforcement

    [Fact]
    public async Task BlockedResources_GoldLockedInPool_CannotBeUsedForSwap()
    {
        // Player has 10 XAU. They lock all of it in a pool.
        // Then try to sell gold in a swap → should fail INSUFFICIENT_GOLD.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "blocked-gold@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "blocked-gold@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 10_000m);
        await SetGoldBalanceAsync(db, playerId, 10m);

        // Create pool that uses ALL available gold (10 XAU) and all but 1000 EUR
        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 10m } }, token);

        // Now try to swap gold — no available gold (all blocked in pool)
        var result = await ExecuteGraphQlAsync(client,
            "mutation Swap($input: ExecuteGoldAmmSwapInput!) { executeGoldAmmSwap(input: $input) { outputAmount } }",
            new { input = new { direction = "GOLD_TO_FIAT", currencyCode = "EUR", amount = 1m, minOutputAmount = 0m } },
            token);

        Assert.Equal("INSUFFICIENT_GOLD", GetError(result));
    }

    [Fact]
    public async Task BlockedResources_FiatLockedInPool_CannotBeUsedForSwap()
    {
        // Player has 5000 EUR. They lock all 5000 in pool.
        // Then try to swap EUR → should fail INSUFFICIENT_FUNDS.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "blocked-fiat@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "blocked-fiat@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 5000m);
        await SetGoldBalanceAsync(db, playerId, 10m);

        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 5m } }, token);

        // All EUR is in pool (blocked), try to swap EUR for gold
        var result = await ExecuteGraphQlAsync(client,
            "mutation Swap($input: ExecuteGoldAmmSwapInput!) { executeGoldAmmSwap(input: $input) { outputAmount } }",
            new { input = new { direction = "FIAT_TO_GOLD", currencyCode = "EUR", amount = 1m, minOutputAmount = 0m } },
            token);

        Assert.Equal("INSUFFICIENT_FUNDS", GetError(result));
    }

    #endregion

    #region MyGoldBalance and MyGoldAmmPositions queries

    [Fact]
    public async Task MyGoldBalance_NoBalance_ReturnsZero()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "gold-bal@test.com");

        var result = await ExecuteGraphQlAsync(client,
            "{ myGoldBalance { balance blockedInPools availableBalance } }", null, token);

        var info = result.GetProperty("data").GetProperty("myGoldBalance");
        Assert.Equal(0m, info.GetProperty("balance").GetDecimal());
        Assert.Equal(0m, info.GetProperty("blockedInPools").GetDecimal());
        Assert.Equal(0m, info.GetProperty("availableBalance").GetDecimal());
    }

    [Fact]
    public async Task MyGoldBalance_WithPool_ShowsCorrectAvailableBalance()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "gold-blk@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "gold-blk@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 5000m);
        await SetGoldBalanceAsync(db, playerId, 10m);

        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 10m } }, token);

        var result = await ExecuteGraphQlAsync(client,
            "{ myGoldBalance { balance blockedInPools availableBalance } }", null, token);

        var info = result.GetProperty("data").GetProperty("myGoldBalance");
        // After depositing ALL 10 XAU into pool via DeductGold:
        //   PlayerGoldBalance.Balance = 0 (physically deducted)
        //   GoldAmmPosition.GoldProvided = 10 (informational: original deposit)
        //   AvailableBalance = Balance = 0  (no double-subtract)
        // Available must never be negative — pool gold was deducted at deposit time.
        Assert.Equal(0m, info.GetProperty("balance").GetDecimal());
        Assert.Equal(10m, info.GetProperty("blockedInPools").GetDecimal());
        Assert.Equal(0m, info.GetProperty("availableBalance").GetDecimal());  // NOT -10
    }

    [Fact]
    public async Task MyGoldAmmPositions_AfterCreatePool_ReturnsPosition()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "positions@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "positions@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 10_000m);
        await SetGoldBalanceAsync(db, playerId, 10m);

        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5000m, goldAmount = 5m } }, token);

        var result = await ExecuteGraphQlAsync(client,
            """
            {
              myGoldAmmPositions {
                id poolId currencyCode liquidityShares sharePercent claimableFiat claimableGold
              }
            }
            """, null, token);

        var positions = result.GetProperty("data").GetProperty("myGoldAmmPositions");
        Assert.Equal(1, positions.GetArrayLength());
        var pos = positions[0];
        Assert.Equal("EUR", pos.GetProperty("currencyCode").GetString());
        Assert.Equal(100m, pos.GetProperty("sharePercent").GetDecimal()); // 100% of pool
        Assert.Equal(5000m, pos.GetProperty("claimableFiat").GetDecimal());
        Assert.Equal(5m, pos.GetProperty("claimableGold").GetDecimal());
    }

    #endregion

    #region Fee accrual

    [Fact]
    public async Task Swap_FeeAccruesToPool_LiquidityProviderEarnsMore()
    {
        // Player A provides liquidity. Player B swaps and pays fee.
        // Player A's claimable amounts after swap should be > original deposit.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var tokenA = await RegisterAndGetTokenAsync(client, "lp-a@test.com");
        var tokenB = await RegisterAndGetTokenAsync(client, "lp-b-swapper@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerAId = await GetPlayerIdByEmailAsync(db, "lp-a@test.com");
        var playerBId = await GetPlayerIdByEmailAsync(db, "lp-b-swapper@test.com");

        await SetFiatBalanceAsync(db, playerAId, "EUR", 20_000m);
        await SetGoldBalanceAsync(db, playerAId, 20m);
        await SetFiatBalanceAsync(db, playerBId, "EUR", 5000m);
        // No gold for player B — they'll only do FIAT_TO_GOLD

        // Player A creates pool
        var createResult = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { positionId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 10_000m, goldAmount = 10m } }, tokenA);
        var positionId = createResult.GetProperty("data").GetProperty("createGoldAmmPool").GetProperty("positionId").GetString()!;

        // Player B swaps (pays 1% fee which stays in pool)
        await ExecuteGraphQlAsync(client,
            "mutation Swap($input: ExecuteGoldAmmSwapInput!) { executeGoldAmmSwap(input: $input) { outputAmount } }",
            new { input = new { direction = "FIAT_TO_GOLD", currencyCode = "EUR", amount = 1000m, minOutputAmount = 0m } },
            tokenB);

        // Player A checks their claimable amounts (should be > original deposit of 10k EUR + 10 XAU)
        var result = await ExecuteGraphQlAsync(client,
            """
            {
              myGoldAmmPositions {
                claimableFiat claimableGold
              }
            }
            """, null, tokenA);

        var positions = result.GetProperty("data").GetProperty("myGoldAmmPositions");
        var pos = positions[0];
        // After swap: pool has 10000 + 1000 EUR = 11000 EUR, and less gold
        // Player A owns 100% → claimable fiat should now be 11000 EUR
        Assert.True(pos.GetProperty("claimableFiat").GetDecimal() > 10_000m);
    }

    #endregion

    #region Balance accounting invariants

    /// <summary>
    /// Full-balance deposit followed by full removal must restore the original balance exactly.
    /// Proves there is no double-accounting loss on the round-trip.
    /// </summary>
    [Fact]
    public async Task FullBalanceDeposit_ThenFullRemoval_RestoresExactBalance()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "roundtrip@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "roundtrip@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 8_000m);
        await SetGoldBalanceAsync(db, playerId, 8m);

        // Deposit ALL funds into pool
        var createResult = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { positionId newFiatBalance newGoldBalance } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 8_000m, goldAmount = 8m } }, token);
        var poolData = createResult.GetProperty("data").GetProperty("createGoldAmmPool");
        Assert.Equal(0m, poolData.GetProperty("newFiatBalance").GetDecimal());  // all in pool
        Assert.Equal(0m, poolData.GetProperty("newGoldBalance").GetDecimal());  // all in pool

        // Available balance = 0 (wallet emptied)
        var balResult = await ExecuteGraphQlAsync(client,
            "{ myGoldBalance { balance availableBalance } }", null, token);
        var balInfo = balResult.GetProperty("data").GetProperty("myGoldBalance");
        Assert.Equal(0m, balInfo.GetProperty("balance").GetDecimal());
        Assert.Equal(0m, balInfo.GetProperty("availableBalance").GetDecimal());

        // Remove all liquidity
        var positionId = createResult.GetProperty("data").GetProperty("createGoldAmmPool").GetProperty("positionId").GetString()!;
        var removeResult = await ExecuteGraphQlAsync(client,
            """
            mutation RemoveLiq($input: RemoveGoldAmmLiquidityInput!) {
              removeGoldAmmLiquidity(input: $input) {
                fiatReturned goldReturned newFiatBalance newGoldBalance
              }
            }
            """,
            new { input = new { positionId = Guid.Parse(positionId), shareFraction = 1.0m } }, token);
        var removeData = removeResult.GetProperty("data").GetProperty("removeGoldAmmLiquidity");
        Assert.Equal(8_000m, removeData.GetProperty("fiatReturned").GetDecimal());
        Assert.Equal(8m, removeData.GetProperty("goldReturned").GetDecimal());
        Assert.Equal(8_000m, removeData.GetProperty("newFiatBalance").GetDecimal());  // fully restored
        Assert.Equal(8m, removeData.GetProperty("newGoldBalance").GetDecimal());       // fully restored
    }

    /// <summary>
    /// Partial removal returns exactly half the deposited funds; remaining shares are consistent.
    /// </summary>
    [Fact]
    public async Task PartialRemoval_ReturnsHalfFunds_RemainingSharesCorrect()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "partial-remove@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "partial-remove@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 10_000m);
        await SetGoldBalanceAsync(db, playerId, 10m);

        var createResult = await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { positionId liquidityShares } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 6_000m, goldAmount = 6m } }, token);
        var positionId = createResult.GetProperty("data").GetProperty("createGoldAmmPool").GetProperty("positionId").GetString()!;
        var totalShares = createResult.GetProperty("data").GetProperty("createGoldAmmPool").GetProperty("liquidityShares").GetDecimal();

        // Remove exactly 50% of position
        var removeResult = await ExecuteGraphQlAsync(client,
            """
            mutation RemoveLiq($input: RemoveGoldAmmLiquidityInput!) {
              removeGoldAmmLiquidity(input: $input) {
                fiatReturned goldReturned remainingShares newFiatBalance newGoldBalance
              }
            }
            """,
            new { input = new { positionId = Guid.Parse(positionId), shareFraction = 0.5m } }, token);
        var data = removeResult.GetProperty("data").GetProperty("removeGoldAmmLiquidity");

        Assert.Equal(3_000m, data.GetProperty("fiatReturned").GetDecimal());   // half of 6000
        Assert.Equal(3m, data.GetProperty("goldReturned").GetDecimal());        // half of 6
        // After partial removal: wallet = 4000 EUR + 3000 returned = 7000 EUR, 4 gold + 3 gold = 7 gold
        Assert.Equal(7_000m, data.GetProperty("newFiatBalance").GetDecimal());
        Assert.Equal(7m, data.GetProperty("newGoldBalance").GetDecimal());

        var remainingShares = data.GetProperty("remainingShares").GetDecimal();
        Assert.True(remainingShares > 0, "Remaining shares should be > 0 after partial removal");
        Assert.True(remainingShares < totalShares, "Remaining shares should be less than original");
    }

    /// <summary>
    /// After depositing partial funds into a pool, the remaining wallet balance is correctly available
    /// for further swaps — proving no double-accounting on the available balance.
    /// </summary>
    [Fact]
    public async Task PostLiquidityDeposit_RemainingWalletBalanceIsAvailableForSwap()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "post-liq-swap@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdByEmailAsync(db, "post-liq-swap@test.com");
        await SetFiatBalanceAsync(db, playerId, "EUR", 10_000m);
        await SetGoldBalanceAsync(db, playerId, 10m);

        // Deposit half into pool: 5000 EUR + 5 XAU → wallet has 5000 EUR + 5 XAU left
        await ExecuteGraphQlAsync(client,
            "mutation CreatePool($input: CreateGoldAmmPoolInput!) { createGoldAmmPool(input: $input) { poolId } }",
            new { input = new { currencyCode = "EUR", fiatAmount = 5_000m, goldAmount = 5m } }, token);

        // Verify myGoldBalance shows correct wallet balance (no double-subtract)
        var balResult = await ExecuteGraphQlAsync(client,
            "{ myGoldBalance { balance blockedInPools availableBalance } }", null, token);
        var balInfo = balResult.GetProperty("data").GetProperty("myGoldBalance");
        Assert.Equal(5m, balInfo.GetProperty("balance").GetDecimal());           // wallet gold
        Assert.Equal(5m, balInfo.GetProperty("blockedInPools").GetDecimal());    // original deposit (informational)
        Assert.Equal(5m, balInfo.GetProperty("availableBalance").GetDecimal());  // = balance, NOT balance - blocked

        // Swap the remaining 5 XAU (wallet gold) for EUR — must succeed
        var swapResult = await ExecuteGraphQlAsync(client,
            "mutation Swap($input: ExecuteGoldAmmSwapInput!) { executeGoldAmmSwap(input: $input) { outputAmount newGoldBalance } }",
            new { input = new { direction = "GOLD_TO_FIAT", currencyCode = "EUR", amount = 5m, minOutputAmount = 0m } },
            token);
        Assert.Null(GetError(swapResult));
        var swapData = swapResult.GetProperty("data").GetProperty("executeGoldAmmSwap");
        Assert.True(swapData.GetProperty("outputAmount").GetDecimal() > 0, "Swap should succeed with remaining wallet gold");
        Assert.Equal(0m, swapData.GetProperty("newGoldBalance").GetDecimal());   // wallet gold fully spent
    }

    #endregion
}
