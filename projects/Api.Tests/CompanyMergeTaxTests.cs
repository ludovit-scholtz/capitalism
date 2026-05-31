using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Regression tests for merge-time tax settlement in <c>mergeCompany</c>.
/// Verifies that a cash-poor target company cannot escape tax and that the
/// settled tax is clamped to the target's available balance and recorded as paid.
/// </summary>
public sealed class CompanyMergeTaxTests
{
    private const decimal TaxRatePercent = 15m;

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName = "Merge Tax Test", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static BankAccount NewBankAccount(Guid companyId, decimal balance)
        => new()
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            AccountNumber = (Math.Abs(Guid.NewGuid().GetHashCode()) % 100_000_000L).ToString("D16"),
            CurrencyCode = "EUR",
            Balance = balance,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };

    /// <summary>
    /// Seeds a player-controlled target (100% owned) with the given balance and taxable
    /// revenue plus an empty destination company, then executes the merge. Returns the
    /// reported cash transferred to the destination and its post-merge bank balance.
    /// </summary>
    private static async Task<(decimal CashTransferred, decimal DestinationBalance)> RunMergeAsync(
        decimal targetBalance, decimal taxableRevenue)
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var email = $"merge-tax-{Guid.NewGuid():N}@example.com";
        var token = await RegisterAndGetTokenAsync(client, email);

        Guid targetCompanyId;
        Guid destinationCompanyId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var playerId = await db.Players.Where(p => p.Email == email).Select(p => p.Id).FirstAsync();

            var gameState = await db.GameStates.FirstOrDefaultAsync();
            if (gameState is null)
            {
                db.GameStates.Add(new GameState { CurrentTick = 0L, TaxRate = TaxRatePercent });
            }
            else
            {
                gameState.TaxRate = TaxRatePercent;
            }

            var destination = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Name = "Destination Co",
                Cash = 0m,
                TotalSharesIssued = 100m,
                FoundedAtUtc = DateTime.UtcNow,
                FoundedAtTick = 0L,
            };
            var target = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Name = "Target Co",
                Cash = 0m,
                TotalSharesIssued = 100m,
                FoundedAtUtc = DateTime.UtcNow,
                FoundedAtTick = 0L,
            };
            db.Companies.AddRange(destination, target);
            db.BankAccounts.Add(NewBankAccount(destination.Id, 0m));
            db.BankAccounts.Add(NewBankAccount(target.Id, targetBalance));

            // Player directly owns 100% of the target so the 90% merge threshold is met.
            db.Shareholdings.Add(new Shareholding
            {
                Id = Guid.NewGuid(),
                CompanyId = target.Id,
                OwnerPlayerId = playerId,
                ShareCount = 100m,
            });

            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = target.Id,
                Category = LedgerCategory.Revenue,
                Description = "Taxable revenue",
                Amount = taxableRevenue,
                RecordedAtTick = 0L,
                RecordedAtUtc = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();
            targetCompanyId = target.Id;
            destinationCompanyId = destination.Id;
        }

        var mergeResult = await ExecuteGraphQlAsync(
            client,
            """
            mutation MergeCompany($input: MergeCompanyInput!) {
              mergeCompany(input: $input) {
                destinationCompanyId
                cashTransferred
              }
            }
            """,
            new { input = new { targetCompanyId, destinationCompanyId } },
            token);

        Assert.False(mergeResult.TryGetProperty("errors", out _), mergeResult.ToString());
        var cashTransferred = mergeResult.GetProperty("data").GetProperty("mergeCompany").GetProperty("cashTransferred").GetDecimal();

        await using (var verifyScope = factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.Companies.AnyAsync(c => c.Id == targetCompanyId));
            // No bank account is left with a negative balance after settlement.
            Assert.False(await db.BankAccounts.AnyAsync(a => a.Balance < 0m));
            var destinationBalance = await db.BankAccounts
                .Where(a => a.CompanyId == destinationCompanyId)
                .SumAsync(a => a.Balance);
            return (cashTransferred, destinationBalance);
        }
    }

    [Fact]
    public async Task MergeCompany_CashPoorTarget_ClampsTaxToAvailableBalanceSoCashCannotEscape()
    {
        // Taxable revenue of 10_000 at 15% owes 1_500 in tax — far more than the $50 balance.
        // The whole balance must be consumed as tax; nothing may carry over to the destination.
        var (cashTransferred, destinationBalance) = await RunMergeAsync(targetBalance: 50m, taxableRevenue: 10_000m);

        Assert.Equal(0m, cashTransferred);
        Assert.Equal(0m, destinationBalance);
    }

    [Fact]
    public async Task MergeCompany_CashRichTarget_SettlesExactTaxAndTransfersRemainder()
    {
        // Taxable revenue of 10_000 at 15% owes exactly 1_500 in tax. With a $5_000 balance the
        // target pays the full 1_500 and the remaining 3_500 transfers to the destination.
        var (cashTransferred, destinationBalance) = await RunMergeAsync(targetBalance: 5_000m, taxableRevenue: 10_000m);

        Assert.Equal(3_500m, cashTransferred);
        Assert.Equal(3_500m, destinationBalance);
    }
}
