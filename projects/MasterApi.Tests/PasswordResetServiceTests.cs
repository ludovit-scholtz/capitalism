using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Tests;

public sealed class PasswordResetServiceTests
{
    private static MasterDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase($"masterapi-password-reset-service-{Guid.NewGuid():N}")
            .Options;
        return new MasterDbContext(options);
    }

    private static PasswordResetService CreateService(MasterDbContext db, AuthOptions? options = null)
    {
        return new PasswordResetService(
            db,
            Options.Create(options ?? new AuthOptions
            {
                PasswordResetTokenLifetimeMinutes = 60,
            }),
            new PasswordHasher<PlayerAccount>());
    }

    [Fact]
    public async Task IssueResetTokenAsync_WhenAccountExists_CreatesHashedToken()
    {
        await using var db = CreateDbContext();
        var account = new PlayerAccount
        {
            Id = Guid.NewGuid(),
            Email = "player@example.com",
            DisplayName = "Player",
            PasswordHash = "hash",
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.PlayerAccounts.Add(account);
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var result = await service.IssueResetTokenAsync(account.Email, CancellationToken.None);

        Assert.True(result.AccountExists);
        Assert.NotNull(result.RawToken);

        var tokenRow = await db.PasswordResetTokens.SingleAsync();
        Assert.NotEqual(result.RawToken, tokenRow.TokenHash);
        Assert.Equal(PasswordResetService.ComputeTokenHash(result.RawToken!), tokenRow.TokenHash);
    }

    [Fact]
    public async Task IssueResetTokenAsync_WhenAccountMissing_ReturnsNeutralNoAccountResult()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.IssueResetTokenAsync("missing@example.com", CancellationToken.None);

        Assert.False(result.AccountExists);
        Assert.Null(result.RawToken);
        Assert.Equal(0, await db.PasswordResetTokens.CountAsync());
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_ThrowsInvalidOrExpired()
    {
        await using var db = CreateDbContext();
        var account = new PlayerAccount
        {
            Id = Guid.NewGuid(),
            Email = "expired@example.com",
            DisplayName = "Expired",
            PasswordHash = string.Empty,
            CreatedAtUtc = DateTime.UtcNow,
        };
        account.PasswordHash = new PasswordHasher<PlayerAccount>().HashPassword(account, "OldPass123!");
        db.PlayerAccounts.Add(account);

        var rawToken = "token-expired";
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = account.Id,
            TokenHash = PasswordResetService.ComputeTokenHash(rawToken),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var exception = await Assert.ThrowsAsync<PasswordResetException>(() =>
            service.ResetPasswordAsync(rawToken, "NewPass123!", CancellationToken.None));

        Assert.Equal("RESET_TOKEN_INVALID_OR_EXPIRED", exception.Code);
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_IsSingleUse_AndInvalidatesOtherActiveTokens()
    {
        await using var db = CreateDbContext();
        var hasher = new PasswordHasher<PlayerAccount>();
        var account = new PlayerAccount
        {
            Id = Guid.NewGuid(),
            Email = "single-use@example.com",
            DisplayName = "Single Use",
            PasswordHash = string.Empty,
            CreatedAtUtc = DateTime.UtcNow,
        };
        account.PasswordHash = hasher.HashPassword(account, "OldPass123!");
        db.PlayerAccounts.Add(account);

        var primaryRawToken = "token-primary";
        var secondaryRawToken = "token-secondary";
        db.PasswordResetTokens.AddRange(
            new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                PlayerAccountId = account.Id,
                TokenHash = PasswordResetService.ComputeTokenHash(primaryRawToken),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            },
            new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                PlayerAccountId = account.Id,
                TokenHash = PasswordResetService.ComputeTokenHash(secondaryRawToken),
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.ResetPasswordAsync(primaryRawToken, "NewPass123!", CancellationToken.None);

        var updatedAccount = await db.PlayerAccounts.SingleAsync();
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(updatedAccount, updatedAccount.PasswordHash, "NewPass123!"));

        var tokens = await db.PasswordResetTokens.ToListAsync();
        Assert.All(tokens, token => Assert.NotNull(token.UsedAtUtc));

        await Assert.ThrowsAsync<PasswordResetException>(() =>
            service.ResetPasswordAsync(primaryRawToken, "AnotherPass1!", CancellationToken.None));
    }
}
