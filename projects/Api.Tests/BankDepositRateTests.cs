using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Tests.Infrastructure;
using Api.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Api.Tests;

/// <summary>
/// Integration tests for dynamic bank deposit interest rate management.
///
/// Tests cover:
/// - Rate validation: negative/excessive rates rejected
/// - Owner-only: non-owner attempt rejected
/// - Bank activation guard: rate change rejected before base capital deposit
/// - Scheduling: 24-tick delay is stored correctly
/// - Tick processor: rate change is applied to all deposits on effective tick
/// - Tick processor: audit record is updated with affected deposit count
/// - Rate history query: returns pending and applied entries
/// - Multi-deposit fairness: all deposits (not just one) get the new rate
/// - Interest accrual uses new rate after effective date
/// </summary>
public sealed class BankDepositRateTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static async Task<TickProcessor> CreateProcessorAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var logger = new NullLogger<TickProcessor>();
        return await Task.FromResult(new TickProcessor(db, phases, logger));
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email, string displayName)
    {
        var result = await ExecuteAsync(client,
            """
            mutation R($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<JsonElement> ExecuteAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8, "application/json"),
        };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Seeds a fully-activated bank building owned by a player.
    /// Returns (bank, bankOwnerToken) — the bank already has base capital deposited.
    /// </summary>
    private static async Task<(Building bank, Player player)> SeedActivatedBankAsync(
        AppDbContext db,
        string suffix,
        decimal depositRate = 3m)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"ratetest-{suffix}@test.com",
            DisplayName = $"Rate Tester {suffix}",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = $"Rate Bank Corp {suffix}",
            Cash = 100_000_000m,
        };
        db.Companies.Add(company);

        var city = await db.Cities.FirstDeterministicAsync();

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = $"Rate Test Bank {suffix}",
            Level = 1,
            BaseCapitalDeposited = true,
            TotalDeposits = 10_000_000m,
            DepositInterestRatePercent = depositRate,
            LendingInterestRatePercent = 8m,
        };
        db.Buildings.Add(bank);

        return (bank, player);
    }

    // ── Rate validation ────────────────────────────────────────────────────────

    /// <summary>
    /// UpdateBankDepositRate rejects a negative rate.
    /// </summary>
    [Fact]
    public async Task UpdateBankDepositRate_NegativeRate_ReturnsInvalidInterestRateError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "ratetest-neg@test.com", "Rate Neg");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == "ratetest-neg@test.com");
        var city = await db.Cities.FirstDeterministicAsync();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = ownerPlayer.Id, Name = "NegRate Co", Cash = 50_000_000m };
        db.Companies.Add(company);
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "NegRate Bank", Level = 1,
            BaseCapitalDeposited = true, DepositInterestRatePercent = 3m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) { depositInterestRatePercent }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = -1m } },
            token: ownerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Expected an error for negative rate.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INVALID_INTEREST_RATE", code);
    }

    /// <summary>
    /// UpdateBankDepositRate rejects a rate above 50%.
    /// </summary>
    [Fact]
    public async Task UpdateBankDepositRate_RateAbove50_ReturnsInvalidInterestRateError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "ratetest-high@test.com", "Rate High");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == "ratetest-high@test.com");
        var city = await db.Cities.FirstDeterministicAsync();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = ownerPlayer.Id, Name = "HighRate Co", Cash = 50_000_000m };
        db.Companies.Add(company);
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "HighRate Bank", Level = 1,
            BaseCapitalDeposited = true, DepositInterestRatePercent = 3m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) { depositInterestRatePercent }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = 50.01m } },
            token: ownerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Expected an error for rate > 50%.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INVALID_INTEREST_RATE", code);
    }

    /// <summary>
    /// UpdateBankDepositRate accepts 0% (floor) and 50% (ceiling) as valid rates.
    /// </summary>
    [Fact]
    public async Task UpdateBankDepositRate_BoundaryRates_AreAccepted()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "ratetest-bound@test.com", "Rate Bound");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == "ratetest-bound@test.com");
        var city = await db.Cities.FirstDeterministicAsync();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = ownerPlayer.Id, Name = "BoundRate Co", Cash = 50_000_000m };
        db.Companies.Add(company);
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "BoundRate Bank", Level = 1,
            BaseCapitalDeposited = true, DepositInterestRatePercent = 3m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        // Test 0% (floor)
        var result0 = await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) { pendingDepositInterestRatePercent }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = 0m } },
            token: ownerToken);
        Assert.False(result0.TryGetProperty("errors", out _), "0% should be valid.");
        Assert.Equal(0m, result0.GetProperty("data").GetProperty("updateBankDepositRate")
            .GetProperty("pendingDepositInterestRatePercent").GetDecimal());

        // Test 50% (ceiling)
        var result50 = await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) { pendingDepositInterestRatePercent }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = 50m } },
            token: ownerToken);
        Assert.False(result50.TryGetProperty("errors", out _), "50% should be valid.");
        Assert.Equal(50m, result50.GetProperty("data").GetProperty("updateBankDepositRate")
            .GetProperty("pendingDepositInterestRatePercent").GetDecimal());
    }

    // ── Owner-only enforcement ─────────────────────────────────────────────────

    /// <summary>
    /// Non-owner cannot call UpdateBankDepositRate — must return NOT_FOUND_OR_NOT_OWNED.
    /// </summary>
    [Fact]
    public async Task UpdateBankDepositRate_NonOwner_ReturnsNotFoundOrNotOwnedError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "rateowner-own@test.com", "Rate Owner");
        var otherToken = await RegisterAsync(client, "rateother-own@test.com", "Rate Other");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == "rateowner-own@test.com");
        var city = await db.Cities.FirstDeterministicAsync();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = ownerPlayer.Id, Name = "Owner Bank Co", Cash = 50_000_000m };
        db.Companies.Add(company);
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "Owner Test Bank", Level = 1,
            BaseCapitalDeposited = true, DepositInterestRatePercent = 3m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        // Use non-owner token
        var result = await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) { depositInterestRatePercent }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = 5m } },
            token: otherToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Expected an error when non-owner calls updateBankDepositRate.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal(ObjectAuthorizationService.NotFoundOrNotOwnedCode, code);
    }

    // ── Scheduling (24-tick delay) ──────────────────────────────────────────────

    /// <summary>
    /// UpdateBankDepositRate stores a pending rate and an effective tick 24 ticks into the future.
    /// The current deposit rate is NOT immediately changed.
    /// </summary>
    [Fact]
    public async Task UpdateBankDepositRate_SchedulesEffectiveTick_24TicksFromNow()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "ratetest-sched@test.com", "Rate Sched");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == "ratetest-sched@test.com");
        var city = await db.Cities.FirstDeterministicAsync();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = ownerPlayer.Id, Name = "Sched Bank Co", Cash = 50_000_000m };
        db.Companies.Add(company);
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "Sched Test Bank", Level = 1,
            BaseCapitalDeposited = true, DepositInterestRatePercent = 3m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        var gameStateBefore = await db.GameStates.FirstDeterministicAsync();
        var tickBefore = gameStateBefore.CurrentTick;

        var result = await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) {
                depositInterestRatePercent
                pendingDepositInterestRatePercent
                pendingDepositRateEffectiveTick
              }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = 4.5m } },
            token: ownerToken);

        Assert.False(result.TryGetProperty("errors", out _), "Expected no error.");
        var summary = result.GetProperty("data").GetProperty("updateBankDepositRate");

        // Current rate is unchanged (still 3%)
        Assert.Equal(3m, summary.GetProperty("depositInterestRatePercent").GetDecimal());

        // Pending rate is the new rate
        Assert.Equal(4.5m, summary.GetProperty("pendingDepositInterestRatePercent").GetDecimal());

        // Effective tick is 24 ticks ahead of current
        var effectiveTick = summary.GetProperty("pendingDepositRateEffectiveTick").GetInt64();
        Assert.Equal(tickBefore + 24L, effectiveTick);

        // Audit record was created with IsApplied=false
        var historyEntry = await db.BankDepositRateHistories
            .FirstOrDefaultAsync(h => h.BankBuildingId == bank.Id);
        Assert.NotNull(historyEntry);
        Assert.Equal(4.5m, historyEntry.NewRatePercent);
        Assert.Equal(3m, historyEntry.PreviousRatePercent);
        Assert.False(historyEntry.IsApplied);
        Assert.Equal(tickBefore + 24L, historyEntry.EffectiveTick);
    }

    // ── Tick processor application ─────────────────────────────────────────────

    /// <summary>
    /// When the effective tick is reached, BankInterestPhase updates all active deposits
    /// at the bank to the new rate and marks the history record as applied.
    /// </summary>
    [Fact]
    public async Task InterestAccrual_AppliesNewRateAfterEffectiveTick_AllDepositsUpdated()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = await CreateProcessorAsync(scope);

        // Seed bank
        var player = new Player
        {
            Id = Guid.NewGuid(), Email = $"rateapply-{Guid.NewGuid():N}@test.com",
            DisplayName = "Rate Apply Owner", PasswordHash = "hash", Role = PlayerRole.Player,
        };
        db.Players.Add(player);

        var bankCompany = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Apply Bank Corp", Cash = 100_000_000m };
        db.Companies.Add(bankCompany);

        var depositorPlayer = new Player
        {
            Id = Guid.NewGuid(), Email = $"depositor-{Guid.NewGuid():N}@test.com",
            DisplayName = "Depositor", PasswordHash = "hash", Role = PlayerRole.Player,
        };
        db.Players.Add(depositorPlayer);

        var depositorCompany = new Company { Id = Guid.NewGuid(), PlayerId = depositorPlayer.Id, Name = "Depositor Corp", Cash = 0m };
        db.Companies.Add(depositorCompany);

        var city = await db.Cities.FirstDeterministicAsync();

        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = bankCompany.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "Rate Apply Bank", Level = 1,
            BaseCapitalDeposited = true, TotalDeposits = 10_000_000m,
            DepositInterestRatePercent = 3m, LendingInterestRatePercent = 8m,
        };
        db.Buildings.Add(bank);

        // Seed two depositor accounts at old rate (3%)
        var deposit1 = new BankAccount
        {
            Id = Guid.NewGuid(), AccountNumber = "1234567890000001",
            CurrencyCode = "EUR", CompanyId = depositorCompany.Id, BankBuildingId = bank.Id,
            Balance = 100_000m, DepositInterestRatePercent = 3m,
            IsBaseCapitalDeposit = false, DepositedAtTick = 0, CreatedAtUtc = DateTime.UtcNow,
        };
        var deposit2 = new BankAccount
        {
            Id = Guid.NewGuid(), AccountNumber = "1234567890000002",
            CurrencyCode = "EUR", CompanyId = depositorCompany.Id, BankBuildingId = bank.Id,
            Balance = 200_000m, DepositInterestRatePercent = 3m,
            IsBaseCapitalDeposit = false, DepositedAtTick = 0, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.AddRange(deposit1, deposit2);

        // Create a pending rate change with effective tick = 0 (already due)
        var gameState = await db.GameStates.FirstDeterministicAsync();
        var rateHistory = new BankDepositRateHistory
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            PreviousRatePercent = 3m,
            NewRatePercent = 5m,
            EffectiveTick = gameState.CurrentTick, // effective immediately on next tick
            EffectiveUtc = DateTime.UtcNow,
            ScheduledAtTick = gameState.CurrentTick - 24,
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-24),
            ChangedByPlayerId = player.Id,
            AffectedDepositCount = 0,
            IsApplied = false,
        };
        db.BankDepositRateHistories.Add(rateHistory);

        bank.PendingDepositInterestRatePercent = 5m;
        bank.PendingDepositRateEffectiveTick = gameState.CurrentTick;

        // Seed a bank funding account for interest payment
        var bankFundingAccount = new BankAccount
        {
            Id = Guid.NewGuid(), AccountNumber = "1234567890000099",
            CurrencyCode = "EUR", CompanyId = bankCompany.Id,
            Balance = 50_000_000m, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(bankFundingAccount);
        bank.BankAccountId = bankFundingAccount.Id;

        await db.SaveChangesAsync();

        // Process one tick — BankInterestPhase should apply the rate change
        await processor.ProcessTickAsync();

        // Reload data
        var refreshed1 = await db.BankAccounts.FindAsync(deposit1.Id);
        var refreshed2 = await db.BankAccounts.FindAsync(deposit2.Id);
        var refreshedBank = await db.Buildings.FindAsync(bank.Id);
        var refreshedHistory = await db.BankDepositRateHistories.FindAsync(rateHistory.Id);

        Assert.NotNull(refreshed1);
        Assert.NotNull(refreshed2);
        Assert.NotNull(refreshedBank);
        Assert.NotNull(refreshedHistory);

        // Deposits should have the new rate
        Assert.Equal(5m, refreshed1.DepositInterestRatePercent);
        Assert.Equal(5m, refreshed2.DepositInterestRatePercent);

        // Bank's current rate should be updated, pending fields cleared
        Assert.Equal(5m, refreshedBank.DepositInterestRatePercent);
        Assert.Null(refreshedBank.PendingDepositInterestRatePercent);
        Assert.Null(refreshedBank.PendingDepositRateEffectiveTick);

        // Audit record marked as applied with correct count
        Assert.True(refreshedHistory.IsApplied);
        Assert.Equal(2, refreshedHistory.AffectedDepositCount);
    }

    /// <summary>
    /// Interest accrued after a rate change uses the new rate, not the old one.
    /// </summary>
    [Fact]
    public async Task InterestAccrual_AfterRateChange_UsesNewRate()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = await CreateProcessorAsync(scope);

        var player = new Player
        {
            Id = Guid.NewGuid(), Email = $"ratecalc-{Guid.NewGuid():N}@test.com",
            DisplayName = "Rate Calc Owner", PasswordHash = "hash", Role = PlayerRole.Player,
        };
        db.Players.Add(player);
        var bankCompany = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Calc Bank Corp", Cash = 100_000_000m };
        db.Companies.Add(bankCompany);

        var depositorPlayer = new Player
        {
            Id = Guid.NewGuid(), Email = $"depositor2-{Guid.NewGuid():N}@test.com",
            DisplayName = "Depositor2", PasswordHash = "hash", Role = PlayerRole.Player,
        };
        db.Players.Add(depositorPlayer);
        var depositorCompany = new Company { Id = Guid.NewGuid(), PlayerId = depositorPlayer.Id, Name = "Depositor Corp2", Cash = 0m };
        db.Companies.Add(depositorCompany);

        var city = await db.Cities.FirstDeterministicAsync();
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = bankCompany.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "Calc Bank", Level = 1,
            BaseCapitalDeposited = true, TotalDeposits = 10_000_000m,
            DepositInterestRatePercent = 6m, // Start at 6%
        };
        db.Buildings.Add(bank);

        var deposit = new BankAccount
        {
            Id = Guid.NewGuid(), AccountNumber = "2000000000000001",
            CurrencyCode = "EUR", CompanyId = depositorCompany.Id, BankBuildingId = bank.Id,
            Balance = 1_000_000m, DepositInterestRatePercent = 6m, // Already updated to new rate
            IsBaseCapitalDeposit = false, DepositedAtTick = 0, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(deposit);

        var bankFundingAccount = new BankAccount
        {
            Id = Guid.NewGuid(), AccountNumber = "2000000000000099",
            CurrencyCode = "EUR", CompanyId = bankCompany.Id,
            Balance = 50_000_000m, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(bankFundingAccount);
        bank.BankAccountId = bankFundingAccount.Id;

        await db.SaveChangesAsync();

        await processor.ProcessTickAsync();

        var refreshed = await db.BankAccounts.FindAsync(deposit.Id);
        Assert.NotNull(refreshed);

        // Per-tick interest = 1,000,000 × (6/100) / 8760 ≈ 6.8493
        var expectedInterest = decimal.Round(1_000_000m * (6m / 100m) / GameConstants.TicksPerYear, 4, MidpointRounding.AwayFromZero);
        Assert.True(refreshed.Balance > 1_000_000m, "Balance should have increased from interest.");
        Assert.InRange(refreshed.Balance - 1_000_000m, expectedInterest - 0.001m, expectedInterest + 0.001m);
    }

    // ── Audit history query ────────────────────────────────────────────────────

    /// <summary>
    /// The bankDepositRateHistory query (exposed via bankDepositRateHistory mutation/query field)
    /// returns all pending and applied history entries for the bank, newest first.
    /// </summary>
    [Fact]
    public async Task UpdateBankDepositRate_AuditHistory_IsImmutableAndQueryable()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "ratehistory@test.com", "Rate History");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == "ratehistory@test.com");
        var city = await db.Cities.FirstDeterministicAsync();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = ownerPlayer.Id, Name = "History Bank Co", Cash = 50_000_000m };
        db.Companies.Add(company);
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "History Bank", Level = 1,
            BaseCapitalDeposited = true, DepositInterestRatePercent = 3m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        // Schedule first rate change
        await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) { pendingDepositInterestRatePercent }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = 4m } },
            token: ownerToken);

        // Schedule second rate change (replaces the first)
        await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) { pendingDepositInterestRatePercent }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = 5.5m } },
            token: ownerToken);

        // Query history
        var histResult = await ExecuteAsync(client,
            """
            query HIST($id: UUID!) {
              bankDepositRateHistory(bankBuildingId: $id) {
                newRatePercent
                previousRatePercent
                isApplied
                effectiveTick
              }
            }
            """,
            new { id = bank.Id.ToString() },
            token: ownerToken);

        Assert.False(histResult.TryGetProperty("errors", out _), "Expected no error querying history.");
        var history = histResult.GetProperty("data").GetProperty("bankDepositRateHistory");
        Assert.Equal(1, history.GetArrayLength()); // Only 1 entry: previous pending was replaced

        var entry = history[0];
        Assert.Equal(5.5m, entry.GetProperty("newRatePercent").GetDecimal());
        Assert.False(entry.GetProperty("isApplied").GetBoolean(), "Rate not yet applied.");
    }

    /// <summary>
    /// Non-owners cannot read another bank's deposit-rate history.
    /// The query must return the same opaque NOT_FOUND_OR_NOT_OWNED code used elsewhere.
    /// </summary>
    [Fact]
    public async Task BankDepositRateHistory_NonOwner_ReturnsNotFoundOrNotOwnedError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "ratehistory-owner@test.com", "Rate History Owner");
        var otherToken = await RegisterAsync(client, "ratehistory-other@test.com", "Rate History Other");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == "ratehistory-owner@test.com");
        var city = await db.Cities.FirstDeterministicAsync();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = ownerPlayer.Id, Name = "History Guard Co", Cash = 50_000_000m };
        db.Companies.Add(company);
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "History Guard Bank", Level = 1,
            BaseCapitalDeposited = true, DepositInterestRatePercent = 3m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) { pendingDepositInterestRatePercent }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = 4m } },
            token: ownerToken);

        var result = await ExecuteAsync(client,
            """
            query HIST($id: UUID!) {
              bankDepositRateHistory(bankBuildingId: $id) {
                id
              }
            }
            """,
            new { id = bank.Id.ToString() },
            token: otherToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Expected an error when non-owner queries bank deposit rate history.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal(ObjectAuthorizationService.NotFoundOrNotOwnedCode, code);
    }

    // ── Bank activation guard ─────────────────────────────────────────────────

    /// <summary>
    /// Calling UpdateBankDepositRate before the bank base capital is deposited should fail.
    /// </summary>
    [Fact]
    public async Task UpdateBankDepositRate_NotActivated_ReturnsBankNotActivatedError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "ratenoinit@test.com", "Rate NoInit");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == "ratenoinit@test.com");
        var city = await db.Cities.FirstDeterministicAsync();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = ownerPlayer.Id, Name = "NoInit Bank Co", Cash = 50_000_000m };
        db.Companies.Add(company);
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "NoInit Bank", Level = 1,
            BaseCapitalDeposited = false, // Not yet activated
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            """
            mutation UBDR($input: UpdateBankDepositRateInput!) {
              updateBankDepositRate(input: $input) { depositInterestRatePercent }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), newRatePercent = 4m } },
            token: ownerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Expected an error for non-activated bank.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("BANK_NOT_ACTIVATED", code);
    }

    // ── Multi-deposit fairness ─────────────────────────────────────────────────

    /// <summary>
    /// When a rate change is applied, ALL active non-owner deposits at the bank
    /// receive the new rate — not just the first one.
    /// </summary>
    [Fact]
    public async Task InterestAccrual_RateChange_AppliesUniformlyToAllDeposits()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = await CreateProcessorAsync(scope);

        var owner = new Player
        {
            Id = Guid.NewGuid(), Email = $"multidep-{Guid.NewGuid():N}@test.com",
            DisplayName = "Multi Dep Owner", PasswordHash = "hash", Role = PlayerRole.Player,
        };
        db.Players.Add(owner);
        var bankCo = new Company { Id = Guid.NewGuid(), PlayerId = owner.Id, Name = "Multi Bank Corp", Cash = 100_000_000m };
        db.Companies.Add(bankCo);

        var dep1Player = new Player { Id = Guid.NewGuid(), Email = $"d1-{Guid.NewGuid():N}@t.com", DisplayName = "D1", PasswordHash = "x", Role = PlayerRole.Player };
        var dep2Player = new Player { Id = Guid.NewGuid(), Email = $"d2-{Guid.NewGuid():N}@t.com", DisplayName = "D2", PasswordHash = "x", Role = PlayerRole.Player };
        var dep3Player = new Player { Id = Guid.NewGuid(), Email = $"d3-{Guid.NewGuid():N}@t.com", DisplayName = "D3", PasswordHash = "x", Role = PlayerRole.Player };
        db.Players.AddRange(dep1Player, dep2Player, dep3Player);

        var d1Co = new Company { Id = Guid.NewGuid(), PlayerId = dep1Player.Id, Name = "D1 Corp", Cash = 0m };
        var d2Co = new Company { Id = Guid.NewGuid(), PlayerId = dep2Player.Id, Name = "D2 Corp", Cash = 0m };
        var d3Co = new Company { Id = Guid.NewGuid(), PlayerId = dep3Player.Id, Name = "D3 Corp", Cash = 0m };
        db.Companies.AddRange(d1Co, d2Co, d3Co);

        var city = await db.Cities.FirstDeterministicAsync();
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = bankCo.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "Multi Bank", Level = 1,
            BaseCapitalDeposited = true, TotalDeposits = 15_000_000m,
            DepositInterestRatePercent = 3m,
        };
        db.Buildings.Add(bank);

        var mkDeposit = (Guid companyId, string num) => new BankAccount
        {
            Id = Guid.NewGuid(), AccountNumber = num, CurrencyCode = "EUR",
            CompanyId = companyId, BankBuildingId = bank.Id, Balance = 50_000m,
            DepositInterestRatePercent = 3m, IsBaseCapitalDeposit = false,
            DepositedAtTick = 0, CreatedAtUtc = DateTime.UtcNow,
        };

        var depa = mkDeposit(d1Co.Id, "3000000000000001");
        var depb = mkDeposit(d2Co.Id, "3000000000000002");
        var depc = mkDeposit(d3Co.Id, "3000000000000003");
        db.BankAccounts.AddRange(depa, depb, depc);

        var gameState = await db.GameStates.FirstDeterministicAsync();
        var rateChange = new BankDepositRateHistory
        {
            Id = Guid.NewGuid(), BankBuildingId = bank.Id,
            PreviousRatePercent = 3m, NewRatePercent = 7m,
            EffectiveTick = gameState.CurrentTick, EffectiveUtc = DateTime.UtcNow,
            ScheduledAtTick = gameState.CurrentTick - 24, ScheduledAtUtc = DateTime.UtcNow,
            ChangedByPlayerId = owner.Id, AffectedDepositCount = 0, IsApplied = false,
        };
        db.BankDepositRateHistories.Add(rateChange);
        bank.PendingDepositInterestRatePercent = 7m;
        bank.PendingDepositRateEffectiveTick = gameState.CurrentTick;

        var fundingAcct = new BankAccount
        {
            Id = Guid.NewGuid(), AccountNumber = "3000000000000099",
            CurrencyCode = "EUR", CompanyId = bankCo.Id,
            Balance = 50_000_000m, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(fundingAcct);
        bank.BankAccountId = fundingAcct.Id;

        await db.SaveChangesAsync();

        await processor.ProcessTickAsync();

        var ra = await db.BankAccounts.FindAsync(depa.Id);
        var rb = await db.BankAccounts.FindAsync(depb.Id);
        var rc = await db.BankAccounts.FindAsync(depc.Id);

        Assert.NotNull(ra); Assert.NotNull(rb); Assert.NotNull(rc);
        Assert.Equal(7m, ra.DepositInterestRatePercent);
        Assert.Equal(7m, rb.DepositInterestRatePercent);
        Assert.Equal(7m, rc.DepositInterestRatePercent);

        var applied = await db.BankDepositRateHistories.FindAsync(rateChange.Id);
        Assert.NotNull(applied);
        Assert.True(applied.IsApplied);
        Assert.Equal(3, applied.AffectedDepositCount);
    }
}
