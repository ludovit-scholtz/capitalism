using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class StockLimitOrderIntegrationTests
{
    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string displayName)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName, password = "Password1!" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetCurrentPlayerIdAsync(HttpClient client, string token)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static async Task<Guid> SeedPublicCompanyAsync(ApiWebApplicationFactory factory, Guid ownerPlayerId, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gameTick = await db.GameStates.Select(state => state.CurrentTick).FirstOrDefaultDeterministicAsync();
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = ownerPlayerId,
            Name = name,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
            FoundedAtTick = gameTick,
            FoundedAtUtc = DateTime.UtcNow,
        };
        db.Companies.Add(company);
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            AccountNumber = (Math.Abs(Guid.NewGuid().GetHashCode()) % 100_000_000L).ToString("D16"),
            CurrencyCode = "EUR",
            Balance = 500_000m,
            IsGovernmentAccount = false,
        });
        db.Shareholdings.Add(new Shareholding
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            OwnerPlayerId = ownerPlayerId,
            ShareCount = 5_000m,
        });
        await db.SaveChangesAsync();
        return company.Id;
    }

    private static async Task<Guid> EnsureUsdSettlementAccountAsync(ApiWebApplicationFactory factory, Guid playerId, decimal balance)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = await db.BankAccounts.FirstOrDefaultAsync(candidate =>
            candidate.PlayerId == playerId
            && candidate.CurrencyCode == "USD"
            && candidate.ClosedAtUtc == null);
        if (account is not null)
        {
            account.Balance = balance;
            await db.SaveChangesAsync();
            return account.Id;
        }

        account = new BankAccount
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            AccountNumber = (Math.Abs(Guid.NewGuid().GetHashCode()) % 100_000_000L).ToString("D16"),
            CurrencyCode = "USD",
            Balance = balance,
            IsGovernmentAccount = false,
        };
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }

    [Fact]
    public async Task PlaceLimitOrder_BuyOrder_AppearsInMyOpenOrders()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAndGetTokenAsync(client, $"limit-owner-{Guid.NewGuid():N}@test.com", "Owner");
        var ownerId = await GetCurrentPlayerIdAsync(client, ownerToken);
        var companyId = await SeedPublicCompanyAsync(factory, ownerId, "Limit Corp");

        var investorToken = await RegisterAndGetTokenAsync(client, $"limit-investor-{Guid.NewGuid():N}@test.com", "Investor");
        var investorId = await GetCurrentPlayerIdAsync(client, investorToken);
        await EnsureUsdSettlementAccountAsync(factory, investorId, 50_000m);

        var symbol = $"CMP-{companyId:N}".ToUpperInvariant();
        var place = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation Place($input: PlaceLimitOrderInput!) {
              placeLimitOrder(input: $input) {
                stockSymbol
                side
                status
                quantity
                filledQuantity
              }
            }
            """,
            new { input = new { stockSymbol = symbol, side = "BUY", limitPrice = 12.5m, quantity = 20 } },
            investorToken);

        var placed = place.GetProperty("data").GetProperty("placeLimitOrder");
        Assert.Equal(symbol, placed.GetProperty("stockSymbol").GetString());
        Assert.Equal("BUY", placed.GetProperty("side").GetString());
        Assert.Equal("OPEN", placed.GetProperty("status").GetString());
        Assert.Equal(20, placed.GetProperty("quantity").GetInt32());
        Assert.Equal(0, placed.GetProperty("filledQuantity").GetInt32());

        var open = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "{ myOpenOrders { stockSymbol side quantity filledQuantity status } }",
            token: investorToken);
        var openOrders = open.GetProperty("data").GetProperty("myOpenOrders").EnumerateArray().ToList();
        Assert.Contains(openOrders, item =>
            item.GetProperty("stockSymbol").GetString() == symbol
            && item.GetProperty("side").GetString() == "BUY"
            && item.GetProperty("status").GetString() == "OPEN");
    }

    [Fact]
    public async Task TickProcessor_MatchesOpposingOrders_WithPriceTimePriority()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAndGetTokenAsync(client, $"limit-owner2-{Guid.NewGuid():N}@test.com", "Owner");
        var ownerId = await GetCurrentPlayerIdAsync(client, ownerToken);
        var companyId = await SeedPublicCompanyAsync(factory, ownerId, "Match Corp");
        var symbol = $"CMP-{companyId:N}".ToUpperInvariant();

        var buyerToken = await RegisterAndGetTokenAsync(client, $"limit-buyer-{Guid.NewGuid():N}@test.com", "Buyer");
        var buyerId = await GetCurrentPlayerIdAsync(client, buyerToken);
        var sellerToken = await RegisterAndGetTokenAsync(client, $"limit-seller-{Guid.NewGuid():N}@test.com", "Seller");
        var sellerId = await GetCurrentPlayerIdAsync(client, sellerToken);
        var buyerUsd = await EnsureUsdSettlementAccountAsync(factory, buyerId, 100_000m);
        var sellerUsd = await EnsureUsdSettlementAccountAsync(factory, sellerId, 5_000m);

        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Shareholdings.Add(new Shareholding
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                OwnerPlayerId = sellerId,
                ShareCount = 100m,
            });
            db.LimitOrders.Add(new LimitOrder
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                StockSymbol = symbol,
                Side = LimitOrderSide.Buy,
                LimitPrice = 12m,
                Quantity = 30,
                FilledQuantity = 0,
                Status = LimitOrderStatus.Open,
                OwnerPlayerId = buyerId,
                SettlementBankAccountId = buyerUsd,
                ReservedCashRemaining = 360m,
                CreatedAtTick = 1,
                UpdatedAtTick = 1,
            });
            db.LimitOrders.Add(new LimitOrder
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                StockSymbol = symbol,
                Side = LimitOrderSide.Sell,
                LimitPrice = 11m,
                Quantity = 30,
                FilledQuantity = 0,
                Status = LimitOrderStatus.Open,
                OwnerPlayerId = sellerId,
                SettlementBankAccountId = sellerUsd,
                ReservedCashRemaining = 0m,
                CreatedAtTick = 1,
                UpdatedAtTick = 1,
            });
            await db.SaveChangesAsync();
        }

        await using (var tickScope = factory.Services.CreateAsyncScope())
        {
            var db = tickScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = tickScope.ServiceProvider.GetServices<ITickPhase>();
            var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());
            await processor.ProcessTickAsync();
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = await assertDb.LimitOrders.AsNoTracking().Where(order => order.CompanyId == companyId).ToListAsync();
        Assert.All(orders, order => Assert.Equal(LimitOrderStatus.Filled, order.Status));
        Assert.All(orders, order => Assert.Equal(30, order.FilledQuantity));

        var buyerHolding = await assertDb.Shareholdings
            .AsNoTracking()
            .FirstOrDefaultAsync(holding => holding.CompanyId == companyId && holding.OwnerPlayerId == buyerId);
        Assert.NotNull(buyerHolding);
        Assert.Equal(30m, buyerHolding!.ShareCount);

        var sellerHolding = await assertDb.Shareholdings
            .AsNoTracking()
            .FirstOrDefaultAsync(holding => holding.CompanyId == companyId && holding.OwnerPlayerId == sellerId);
        Assert.NotNull(sellerHolding);
        Assert.Equal(70m, sellerHolding!.ShareCount);

        var executions = await assertDb.LimitOrderExecutions
            .AsNoTracking()
            .Where(execution => execution.CompanyId == companyId)
            .ToListAsync();
        Assert.Single(executions);
        Assert.Equal(11m, executions[0].Price);
        Assert.Equal(30, executions[0].Quantity);
    }

    [Fact]
    public async Task CancelLimitOrder_ReleasesReservedBuyCash()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAndGetTokenAsync(client, $"limit-owner3-{Guid.NewGuid():N}@test.com", "Owner");
        var ownerId = await GetCurrentPlayerIdAsync(client, ownerToken);
        var companyId = await SeedPublicCompanyAsync(factory, ownerId, "Cancel Corp");

        var investorToken = await RegisterAndGetTokenAsync(client, $"limit-cancel-{Guid.NewGuid():N}@test.com", "Cancel Investor");
        var investorId = await GetCurrentPlayerIdAsync(client, investorToken);
        var settlementId = await EnsureUsdSettlementAccountAsync(factory, investorId, 10_000m);

        var symbol = $"CMP-{companyId:N}".ToUpperInvariant();
        var place = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation Place($input: PlaceLimitOrderInput!) {
              placeLimitOrder(input: $input) { id }
            }
            """,
            new { input = new { stockSymbol = symbol, side = "BUY", limitPrice = 20m, quantity = 100 } },
            investorToken);
        var orderId = Guid.Parse(place.GetProperty("data").GetProperty("placeLimitOrder").GetProperty("id").GetString()!);

        await using (var beforeCancelScope = factory.Services.CreateAsyncScope())
        {
            var db = beforeCancelScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.BankAccounts.AsNoTracking().FirstAsync(candidate => candidate.Id == settlementId);
            Assert.Equal(8_000m, account.Balance);
        }

        var cancel = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation Cancel($orderId: UUID!) {
              cancelLimitOrder(orderId: $orderId) { status }
            }
            """,
            new { orderId },
            investorToken);
        Assert.Equal("CANCELLED", cancel.GetProperty("data").GetProperty("cancelLimitOrder").GetProperty("status").GetString());

        await using var afterCancelScope = factory.Services.CreateAsyncScope();
        var assertDb = afterCancelScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restoredAccount = await assertDb.BankAccounts.AsNoTracking().FirstAsync(candidate => candidate.Id == settlementId);
        Assert.Equal(10_000m, restoredAccount.Balance);
        var order = await assertDb.LimitOrders.AsNoTracking().FirstAsync(candidate => candidate.Id == orderId);
        Assert.Equal(LimitOrderStatus.Cancelled, order.Status);
        Assert.Equal(0m, order.ReservedCashRemaining);
    }
}
