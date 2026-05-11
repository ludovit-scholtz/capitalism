using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public sealed class BotOwnershipGuardTests
{
    [Fact]
    public async Task EnsureCompanyOwnedAsync_OwnedCompany_Succeeds()
    {
        await using var db = CreateDb(nameof(EnsureCompanyOwnedAsync_OwnedCompany_Succeeds));
        var playerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        db.Players.Add(new Player
        {
            Id = playerId,
            Email = "owner@test.com",
            DisplayName = "Owner",
            PasswordHash = "hash",
        });
        db.Companies.Add(new Company
        {
            Id = companyId,
            PlayerId = playerId,
            Name = "Owned Co",
            FoundedAtTick = 0,
        });
        await db.SaveChangesAsync();

        var guard = new BotOwnershipGuard(db);
        await guard.EnsureCompanyOwnedAsync(playerId, companyId, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureCompanyOwnedAsync_ForeignCompany_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureCompanyOwnedAsync_ForeignCompany_ThrowsNotOwnedOrNotFound));
        var ownerPlayerId = Guid.NewGuid();
        var foreignPlayerId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        db.Players.AddRange(
            new Player
            {
                Id = ownerPlayerId,
                Email = "owner@test.com",
                DisplayName = "Owner",
                PasswordHash = "hash",
            },
            new Player
            {
                Id = foreignPlayerId,
                Email = "foreign@test.com",
                DisplayName = "Foreign",
                PasswordHash = "hash",
            });
        db.Companies.Add(new Company
        {
            Id = companyId,
            PlayerId = foreignPlayerId,
            Name = "Foreign Co",
            FoundedAtTick = 0,
        });
        await db.SaveChangesAsync();

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureCompanyOwnedAsync(ownerPlayerId, companyId, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);
    }

    [Fact]
    public async Task EnsureCompanyOwnedAsync_MissingCompany_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureCompanyOwnedAsync_MissingCompany_ThrowsNotOwnedOrNotFound));
        var playerId = Guid.NewGuid();
        db.Players.Add(new Player
        {
            Id = playerId,
            Email = "owner@test.com",
            DisplayName = "Owner",
            PasswordHash = "hash",
        });
        await db.SaveChangesAsync();

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureCompanyOwnedAsync(playerId, null, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);
    }

    [Fact]
    public async Task EnsureMutationOwnershipAsync_ForeignForexAccount_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureMutationOwnershipAsync_ForeignForexAccount_ThrowsNotOwnedOrNotFound));
        var ownerPlayerId = Guid.NewGuid();
        var foreignPlayerId = Guid.NewGuid();
        var foreignAccountId = Guid.NewGuid();

        db.Players.AddRange(
            new Player
            {
                Id = ownerPlayerId,
                Email = "owner@test.com",
                DisplayName = "Owner",
                PasswordHash = "hash",
            },
            new Player
            {
                Id = foreignPlayerId,
                Email = "foreign@test.com",
                DisplayName = "Foreign",
                PasswordHash = "hash",
            });
        db.BankAccounts.Add(new BankAccount
        {
            Id = foreignAccountId,
            PlayerId = foreignPlayerId,
            AccountNumber = "1234567890123456",
            CurrencyCode = "EUR",
            Balance = 100m,
        });
        await db.SaveChangesAsync();

        using var document = JsonDocument.Parse($$"""
        {
          "input": {
            "fromBankAccountId": "{{foreignAccountId}}"
          }
        }
        """);

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureMutationOwnershipAsync("executeForexSwap", document.RootElement, ownerPlayerId, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);
    }

    [Fact]
    public async Task EnsureMutationOwnershipAsync_AddGoldAmmLiquidity_ForeignPool_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureMutationOwnershipAsync_AddGoldAmmLiquidity_ForeignPool_ThrowsNotOwnedOrNotFound));
        var ownerPlayerId = Guid.NewGuid();
        var foreignPlayerId = Guid.NewGuid();
        var poolId = Guid.NewGuid();

        db.Players.AddRange(
            new Player { Id = ownerPlayerId, Email = "owner@test.com", DisplayName = "Owner", PasswordHash = "hash" },
            new Player { Id = foreignPlayerId, Email = "foreign@test.com", DisplayName = "Foreign", PasswordHash = "hash" });
        db.GoldAmmPools.Add(new GoldAmmPool
        {
            Id = poolId,
            CurrencyCode = "EUR",
            FiatReserve = 1_000m,
            GoldReserve = 5m,
            TotalLiquidityShares = 100m,
        });
        db.GoldAmmPositions.Add(new GoldAmmPosition
        {
            Id = Guid.NewGuid(),
            PoolId = poolId,
            PlayerId = foreignPlayerId,
            LiquidityShares = 100m,
            FiatProvided = 1_000m,
            GoldProvided = 5m,
        });
        await db.SaveChangesAsync();

        using var document = JsonDocument.Parse($$"""
        {
          "input": {
            "poolId": "{{poolId}}"
          }
        }
        """);

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureMutationOwnershipAsync("addGoldAmmLiquidity", document.RootElement, ownerPlayerId, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);
    }

    [Fact]
    public async Task EnsureMutationOwnershipAsync_RemoveGoldAmmLiquidity_ForeignPosition_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureMutationOwnershipAsync_RemoveGoldAmmLiquidity_ForeignPosition_ThrowsNotOwnedOrNotFound));
        var ownerPlayerId = Guid.NewGuid();
        var foreignPlayerId = Guid.NewGuid();
        var poolId = Guid.NewGuid();
        var foreignPositionId = Guid.NewGuid();

        db.Players.AddRange(
            new Player { Id = ownerPlayerId, Email = "owner@test.com", DisplayName = "Owner", PasswordHash = "hash" },
            new Player { Id = foreignPlayerId, Email = "foreign@test.com", DisplayName = "Foreign", PasswordHash = "hash" });
        db.GoldAmmPools.Add(new GoldAmmPool
        {
            Id = poolId,
            CurrencyCode = "EUR",
            FiatReserve = 1_000m,
            GoldReserve = 5m,
            TotalLiquidityShares = 100m,
        });
        db.GoldAmmPositions.Add(new GoldAmmPosition
        {
            Id = foreignPositionId,
            PoolId = poolId,
            PlayerId = foreignPlayerId,
            LiquidityShares = 100m,
            FiatProvided = 1_000m,
            GoldProvided = 5m,
        });
        await db.SaveChangesAsync();

        using var document = JsonDocument.Parse($$"""
        {
          "input": {
            "positionId": "{{foreignPositionId}}"
          }
        }
        """);

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureMutationOwnershipAsync("removeGoldAmmLiquidity", document.RootElement, ownerPlayerId, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);
    }

    [Fact]
    public async Task EnsureMutationOwnershipAsync_CancelLimitOrder_ForeignOrder_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureMutationOwnershipAsync_CancelLimitOrder_ForeignOrder_ThrowsNotOwnedOrNotFound));
        var ownerPlayerId = Guid.NewGuid();
        var foreignPlayerId = Guid.NewGuid();
        var foreignCompanyId = Guid.NewGuid();
        var foreignOrderId = Guid.NewGuid();
        var foreignSettlementAccountId = Guid.NewGuid();

        db.Players.AddRange(
            new Player { Id = ownerPlayerId, Email = "owner@test.com", DisplayName = "Owner", PasswordHash = "hash" },
            new Player { Id = foreignPlayerId, Email = "foreign@test.com", DisplayName = "Foreign", PasswordHash = "hash" });
        db.Companies.Add(new Company { Id = foreignCompanyId, PlayerId = foreignPlayerId, Name = "Foreign Co", FoundedAtTick = 0 });
        db.BankAccounts.Add(new BankAccount
        {
            Id = foreignSettlementAccountId,
            CompanyId = foreignCompanyId,
            AccountNumber = "1234567890123456",
            CurrencyCode = "USD",
            Balance = 5_000m,
        });
        db.LimitOrders.Add(new LimitOrder
        {
            Id = foreignOrderId,
            CompanyId = foreignCompanyId,
            StockSymbol = StockSymbolCodec.FromCompanyId(foreignCompanyId),
            Side = LimitOrderSide.Buy,
            LimitPrice = 10m,
            Quantity = 100,
            Status = LimitOrderStatus.Open,
            OwnerCompanyId = foreignCompanyId,
            SettlementBankAccountId = foreignSettlementAccountId,
            CreatedAtTick = 0,
            UpdatedAtTick = 0,
        });
        await db.SaveChangesAsync();

        using var document = JsonDocument.Parse($$"""
        {
          "orderId": "{{foreignOrderId}}"
        }
        """);

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureMutationOwnershipAsync("cancelLimitOrder", document.RootElement, ownerPlayerId, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);
    }

    [Fact]
    public async Task EnsureMutationOwnershipAsync_ProposeDividend_ForeignCompanyWithoutShares_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureMutationOwnershipAsync_ProposeDividend_ForeignCompanyWithoutShares_ThrowsNotOwnedOrNotFound));
        var ownerPlayerId = Guid.NewGuid();
        var foreignPlayerId = Guid.NewGuid();
        var foreignCompanyId = Guid.NewGuid();
        var foreignStockSymbol = StockSymbolCodec.FromCompanyId(foreignCompanyId);

        db.Players.AddRange(
            new Player { Id = ownerPlayerId, Email = "owner@test.com", DisplayName = "Owner", PasswordHash = "hash" },
            new Player { Id = foreignPlayerId, Email = "foreign@test.com", DisplayName = "Foreign", PasswordHash = "hash" });
        db.Companies.Add(new Company { Id = foreignCompanyId, PlayerId = foreignPlayerId, Name = "Foreign Co", FoundedAtTick = 0 });
        await db.SaveChangesAsync();

        using var document = JsonDocument.Parse($$"""
        {
          "input": {
            "stockSymbol": "{{foreignStockSymbol}}"
          }
        }
        """);

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureMutationOwnershipAsync("proposeDividend", document.RootElement, ownerPlayerId, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);
    }

    [Fact]
    public async Task EnsureMutationOwnershipAsync_VoteDividendProposal_ForeignProposalWithoutShares_ThrowsNotOwnedOrNotFound()
    {
        await using var db = CreateDb(nameof(EnsureMutationOwnershipAsync_VoteDividendProposal_ForeignProposalWithoutShares_ThrowsNotOwnedOrNotFound));
        var ownerPlayerId = Guid.NewGuid();
        var foreignPlayerId = Guid.NewGuid();
        var foreignCompanyId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        db.Players.AddRange(
            new Player { Id = ownerPlayerId, Email = "owner@test.com", DisplayName = "Owner", PasswordHash = "hash" },
            new Player { Id = foreignPlayerId, Email = "foreign@test.com", DisplayName = "Foreign", PasswordHash = "hash" });
        db.Companies.Add(new Company
        {
            Id = foreignCompanyId,
            PlayerId = foreignPlayerId,
            Name = "Foreign Co",
            FoundedAtTick = 0,
            TotalSharesIssued = 1_000m,
        });
        db.DividendProposals.Add(new DividendProposal
        {
            Id = proposalId,
            CompanyId = foreignCompanyId,
            StockSymbol = StockSymbolCodec.FromCompanyId(foreignCompanyId),
            ProposedByAccountId = foreignCompanyId,
            ProposedByAccountType = AccountContextType.Company,
            DividendPerShare = 1m,
            TotalPayout = 1_000m,
            Status = DividendProposalStatus.Voting,
            ProposedAtTick = 0,
            VotingOpenTick = 0,
            VotingCloseTick = 10,
        });
        await db.SaveChangesAsync();

        using var document = JsonDocument.Parse($$"""
        {
          "input": {
            "proposalId": "{{proposalId}}"
          }
        }
        """);

        var guard = new BotOwnershipGuard(db);
        var ex = await Assert.ThrowsAsync<GraphQLException>(() => guard.EnsureMutationOwnershipAsync("voteDividendProposal", document.RootElement, ownerPlayerId, CancellationToken.None));
        Assert.Equal(BotOwnershipGuard.NotFoundOrNotOwnedCode, ex.Errors.Single().Code);
    }

    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"bot-ownership-guard-tests-{name}")
            .Options;

        return new AppDbContext(options);
    }
}
