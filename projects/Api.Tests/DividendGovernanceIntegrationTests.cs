using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class DividendGovernanceIntegrationTests
{
    private static async Task<TickProcessor> CreateProcessorAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        return await Task.FromResult(new TickProcessor(db, phases, NullLogger<TickProcessor>.Instance));
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string displayName)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName, password = "Password1!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetCurrentPlayerIdAsync(HttpClient client, string token)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static BankAccount CreateCompanyBankAccount(Guid companyId, string currencyCode, decimal balance)
    {
        return new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L).ToString("D16"),
            CurrencyCode = currencyCode,
            Balance = balance,
            CompanyId = companyId,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task ProposeVoteSettleDividend_Approved_DistributesPayoutsAndRecordsHistory()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var majorityToken = await RegisterAndGetTokenAsync(client, $"div-majority-{Guid.NewGuid():N}@test.com", "Majority");
        var minorityToken = await RegisterAndGetTokenAsync(client, $"div-minority-{Guid.NewGuid():N}@test.com", "Minority");
        var majorityPlayerId = await GetCurrentPlayerIdAsync(client, majorityToken);
        var minorityPlayerId = await GetCurrentPlayerIdAsync(client, minorityToken);

        Guid listedCompanyId;
        string listedStockSymbol;
        decimal startingCompanyBalance;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await db.Cities.AsNoTracking().FirstAsync(city => city.CurrencyCode == "EUR");
            var currentTick = await db.GameStates.Select(state => (long?)state.CurrentTick).FirstOrDefaultDeterministicAsync() ?? 0L;

            var listedCompany = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = majorityPlayerId,
                Name = "Dividend Listed Co",
                TotalSharesIssued = 1_000m,
                DividendPayoutRatio = 0.2m,
                FoundedAtTick = currentTick,
                FoundedAtUtc = DateTime.UtcNow,
            };
            db.Companies.Add(listedCompany);

            var companyFundingAccount = CreateCompanyBankAccount(listedCompany.Id, city.CurrencyCode, 20_000m);
            db.BankAccounts.Add(companyFundingAccount);
            db.Shareholdings.AddRange(
                new Shareholding
                {
                    Id = Guid.NewGuid(),
                    CompanyId = listedCompany.Id,
                    OwnerPlayerId = majorityPlayerId,
                    ShareCount = 700m,
                },
                new Shareholding
                {
                    Id = Guid.NewGuid(),
                    CompanyId = listedCompany.Id,
                    OwnerPlayerId = minorityPlayerId,
                    ShareCount = 300m,
                });
            await db.SaveChangesAsync();

            listedCompanyId = listedCompany.Id;
            listedStockSymbol = StockSymbolCodec.FromCompanyId(listedCompany.Id);
            startingCompanyBalance = companyFundingAccount.Balance;
        }

        var proposeResult = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation ProposeDividend($input: ProposeDividendInput!) {
              proposeDividend(input: $input) {
                id
                votingCloseTick
              }
            }
            """,
            new { input = new { stockSymbol = listedStockSymbol, dividendPerShare = 2m } },
            majorityToken);
        var proposalId = Guid.Parse(
            proposeResult.GetProperty("data").GetProperty("proposeDividend").GetProperty("id").GetString()!);
        var votingCloseTick = proposeResult.GetProperty("data").GetProperty("proposeDividend").GetProperty("votingCloseTick").GetInt64();

        await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation VoteDividendProposal($input: VoteDividendProposalInput!) {
              voteDividendProposal(input: $input) { id }
            }
            """,
            new { input = new { proposalId, choice = "FOR" } },
            majorityToken);
        await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation VoteDividendProposal($input: VoteDividendProposalInput!) {
              voteDividendProposal(input: $input) { id }
            }
            """,
            new { input = new { proposalId, choice = "AGAINST" } },
            minorityToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var processor = await CreateProcessorAsync(scope);
            var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
            Assert.NotNull(gameState);

            var guard = 0;
            while (gameState!.CurrentTick < votingCloseTick && guard++ < 40)
            {
                await processor.ProcessTickAsync();
                await db.SaveChangesAsync();
                gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
            }
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var proposal = await db.DividendProposals.AsNoTracking().FirstAsync(candidate => candidate.Id == proposalId);
            Assert.Equal(DividendProposalStatus.Settled, proposal.Status);
            Assert.NotNull(proposal.SettledAtTick);

            var dividendPayments = await db.DividendPayments
                .AsNoTracking()
                .Where(payment => payment.CompanyId == listedCompanyId)
                .OrderBy(payment => payment.TotalAmount)
                .ToListAsync();
            Assert.Equal(2, dividendPayments.Count);
            Assert.Equal(600m, dividendPayments[0].TotalAmount);
            Assert.Equal(1400m, dividendPayments[1].TotalAmount);

            var majorityPersonalBalance = await db.BankAccounts
                .Where(account => account.PlayerId == majorityPlayerId && account.CurrencyCode == PersonalBankAccountService.SettlementCurrencyCode)
                .Select(account => account.Balance)
                .FirstOrDefaultDeterministicAsync();
            var minorityPersonalBalance = await db.BankAccounts
                .Where(account => account.PlayerId == minorityPlayerId && account.CurrencyCode == PersonalBankAccountService.SettlementCurrencyCode)
                .Select(account => account.Balance)
                .FirstOrDefaultDeterministicAsync();
            Assert.Equal(201_400m, majorityPersonalBalance);
            Assert.Equal(200_600m, minorityPersonalBalance);

            var updatedCompanyBalance = await db.BankAccounts
                .Where(account => account.CompanyId == listedCompanyId)
                .Select(account => account.Balance)
                .FirstOrDefaultDeterministicAsync();
            Assert.Equal(startingCompanyBalance - 2_000m, updatedCompanyBalance);

            var settlementNotifications = await db.PlayerNotifications
                .AsNoTracking()
                .Where(notification => notification.CompanyId == listedCompanyId && notification.Type == PlayerNotificationType.DividendProposalSettled)
                .ToListAsync();
            Assert.True(settlementNotifications.Count >= 2);

            var priceHistory = await db.SharePriceHistoryEntries
                .AsNoTracking()
                .Where(entry => entry.CompanyId == listedCompanyId)
                .ToListAsync();
            Assert.NotEmpty(priceHistory);
        }
    }

    [Fact]
    public async Task VoteDividendProposal_SameAccountCannotVoteTwice()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAndGetTokenAsync(client, $"div-double-{Guid.NewGuid():N}@test.com", "Vote Once");
        var ownerPlayerId = await GetCurrentPlayerIdAsync(client, ownerToken);
        string stockSymbol;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await db.Cities.AsNoTracking().FirstAsync(city => city.CurrencyCode == "EUR");
            var currentTick = await db.GameStates.Select(state => (long?)state.CurrentTick).FirstOrDefaultDeterministicAsync() ?? 0L;
            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = ownerPlayerId,
                Name = "Double Vote Co",
                TotalSharesIssued = 500m,
                FoundedAtTick = currentTick,
                FoundedAtUtc = DateTime.UtcNow,
            };
            db.Companies.Add(company);
            db.BankAccounts.Add(CreateCompanyBankAccount(company.Id, city.CurrencyCode, 10_000m));
            db.Shareholdings.Add(new Shareholding
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                OwnerPlayerId = ownerPlayerId,
                ShareCount = 500m,
            });
            await db.SaveChangesAsync();
            stockSymbol = StockSymbolCodec.FromCompanyId(company.Id);
        }

        var proposeResult = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation ProposeDividend($input: ProposeDividendInput!) {
              proposeDividend(input: $input) { id }
            }
            """,
            new { input = new { stockSymbol, dividendPerShare = 1m } },
            ownerToken);
        var proposalId = Guid.Parse(proposeResult.GetProperty("data").GetProperty("proposeDividend").GetProperty("id").GetString()!);

        await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation VoteDividendProposal($input: VoteDividendProposalInput!) {
              voteDividendProposal(input: $input) { id }
            }
            """,
            new { input = new { proposalId, choice = "FOR" } },
            ownerToken);

        var secondVoteResult = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation VoteDividendProposal($input: VoteDividendProposalInput!) {
              voteDividendProposal(input: $input) { id }
            }
            """,
            new { input = new { proposalId, choice = "AGAINST" } },
            ownerToken);

        var error = secondVoteResult.GetProperty("errors")[0];
        Assert.Equal("DIVIDEND_ALREADY_VOTED", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task DividendGovernanceSettlement_Rejected_ReleasesReservedCash()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var proposerToken = await RegisterAndGetTokenAsync(client, $"div-reject-proposer-{Guid.NewGuid():N}@test.com", "Reject Proposer");
        var opponentToken = await RegisterAndGetTokenAsync(client, $"div-reject-opponent-{Guid.NewGuid():N}@test.com", "Reject Opponent");
        var proposerPlayerId = await GetCurrentPlayerIdAsync(client, proposerToken);
        var opponentPlayerId = await GetCurrentPlayerIdAsync(client, opponentToken);

        Guid listedCompanyId;
        string listedStockSymbol;
        decimal startingCompanyBalance;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await db.Cities.AsNoTracking().FirstAsync(city => city.CurrencyCode == "EUR");
            var currentTick = await db.GameStates.Select(state => (long?)state.CurrentTick).FirstOrDefaultDeterministicAsync() ?? 0L;
            var listedCompany = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = proposerPlayerId,
                Name = "Rejected Dividend Co",
                TotalSharesIssued = 1_000m,
                FoundedAtTick = currentTick,
                FoundedAtUtc = DateTime.UtcNow,
            };
            db.Companies.Add(listedCompany);
            var companyFundingAccount = CreateCompanyBankAccount(listedCompany.Id, city.CurrencyCode, 12_000m);
            db.BankAccounts.Add(companyFundingAccount);
            db.Shareholdings.AddRange(
                new Shareholding
                {
                    Id = Guid.NewGuid(),
                    CompanyId = listedCompany.Id,
                    OwnerPlayerId = proposerPlayerId,
                    ShareCount = 400m,
                },
                new Shareholding
                {
                    Id = Guid.NewGuid(),
                    CompanyId = listedCompany.Id,
                    OwnerPlayerId = opponentPlayerId,
                    ShareCount = 600m,
                });
            await db.SaveChangesAsync();

            listedCompanyId = listedCompany.Id;
            listedStockSymbol = StockSymbolCodec.FromCompanyId(listedCompany.Id);
            startingCompanyBalance = companyFundingAccount.Balance;
        }

        var proposeResult = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation ProposeDividend($input: ProposeDividendInput!) {
              proposeDividend(input: $input) {
                id
                votingCloseTick
              }
            }
            """,
            new { input = new { stockSymbol = listedStockSymbol, dividendPerShare = 2m } },
            proposerToken);
        var proposalId = Guid.Parse(proposeResult.GetProperty("data").GetProperty("proposeDividend").GetProperty("id").GetString()!);
        var votingCloseTick = proposeResult.GetProperty("data").GetProperty("proposeDividend").GetProperty("votingCloseTick").GetInt64();

        await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation VoteDividendProposal($input: VoteDividendProposalInput!) {
              voteDividendProposal(input: $input) { id }
            }
            """,
            new { input = new { proposalId, choice = "AGAINST" } },
            proposerToken);
        await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation VoteDividendProposal($input: VoteDividendProposalInput!) {
              voteDividendProposal(input: $input) { id }
            }
            """,
            new { input = new { proposalId, choice = "AGAINST" } },
            opponentToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var processor = await CreateProcessorAsync(scope);
            var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
            Assert.NotNull(gameState);

            var guard = 0;
            while (gameState!.CurrentTick < votingCloseTick && guard++ < 40)
            {
                await processor.ProcessTickAsync();
                await db.SaveChangesAsync();
                gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
            }
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var proposal = await db.DividendProposals.AsNoTracking().FirstAsync(candidate => candidate.Id == proposalId);
            Assert.Equal(DividendProposalStatus.Rejected, proposal.Status);
            Assert.NotNull(proposal.SettledAtTick);

            var updatedCompanyBalance = await db.BankAccounts
                .Where(account => account.CompanyId == listedCompanyId)
                .Select(account => account.Balance)
                .FirstOrDefaultDeterministicAsync();
            Assert.Equal(startingCompanyBalance, updatedCompanyBalance);

            var dividendPayments = await db.DividendPayments
                .AsNoTracking()
                .Where(payment => payment.CompanyId == listedCompanyId)
                .ToListAsync();
            Assert.Empty(dividendPayments);
        }
    }

    [Fact]
    public async Task ProposeDividend_PolicyProposal_NonCeoRejectedWithNotCeo()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAndGetTokenAsync(client, $"div-policy-owner-{Guid.NewGuid():N}@test.com", "Policy Owner");
        var otherToken = await RegisterAndGetTokenAsync(client, $"div-policy-other-{Guid.NewGuid():N}@test.com", "Policy Other");
        var ownerId = await GetCurrentPlayerIdAsync(client, ownerToken);
        Guid companyId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var currentTick = await db.GameStates.Select(state => (long?)state.CurrentTick).FirstOrDefaultDeterministicAsync() ?? 0L;
            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = ownerId,
                Name = "Policy Co",
                TotalSharesIssued = 1_000m,
                DividendPayoutRatio = 0.2m,
                FoundedAtTick = currentTick,
                FoundedAtUtc = DateTime.UtcNow,
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation ProposeDividend($input: ProposeDividendInput!) {
              proposeDividend(input: $input) { id }
            }
            """,
            new { input = new { companyId, dividendPercent = 35m } },
            otherToken);
        var error = result.GetProperty("errors")[0];
        Assert.Equal("NOT_CEO", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task VoteDividend_ApproveMajority_UpdatesCompanyDividendPayoutRatio()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var majorityToken = await RegisterAndGetTokenAsync(client, $"div-policy-majority-{Guid.NewGuid():N}@test.com", "Policy Majority");
        var minorityToken = await RegisterAndGetTokenAsync(client, $"div-policy-minority-{Guid.NewGuid():N}@test.com", "Policy Minority");
        var majorityPlayerId = await GetCurrentPlayerIdAsync(client, majorityToken);
        var minorityPlayerId = await GetCurrentPlayerIdAsync(client, minorityToken);
        Guid companyId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var currentTick = await db.GameStates.Select(state => (long?)state.CurrentTick).FirstOrDefaultDeterministicAsync() ?? 0L;
            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = majorityPlayerId,
                Name = "Policy Voting Co",
                TotalSharesIssued = 1_000m,
                DividendPayoutRatio = 0.2m,
                FoundedAtTick = currentTick,
                FoundedAtUtc = DateTime.UtcNow,
            };
            db.Companies.Add(company);
            db.Shareholdings.AddRange(
                new Shareholding
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    OwnerPlayerId = majorityPlayerId,
                    ShareCount = 700m,
                },
                new Shareholding
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    OwnerPlayerId = minorityPlayerId,
                    ShareCount = 300m,
                });
            await db.SaveChangesAsync();
            companyId = company.Id;
        }

        await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation ProposeDividend($input: ProposeDividendInput!) {
              proposeDividend(input: $input) { id }
            }
            """,
            new { input = new { companyId, dividendPercent = 45m } },
            majorityToken);
        await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation VoteDividend($input: VoteDividendInput!) {
              voteDividend(input: $input) { id status }
            }
            """,
            new { input = new { companyId, vote = "APPROVE" } },
            majorityToken);
        await using var assertScope = factory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var proposalStatus = await assertDb.DividendProposals
            .AsNoTracking()
            .Where(proposal => proposal.CompanyId == companyId && proposal.TotalPayout <= 0m)
            .Select(proposal => proposal.Status)
            .FirstOrDefaultDeterministicAsync();
        Assert.Equal(DividendProposalStatus.Settled, proposalStatus);
        var updatedRatio = await assertDb.Companies
            .AsNoTracking()
            .Where(company => company.Id == companyId)
            .Select(company => company.DividendPayoutRatio)
            .FirstOrDefaultDeterministicAsync();
        Assert.Equal(0.45m, updatedRatio);
    }
}
