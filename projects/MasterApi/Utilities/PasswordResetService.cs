using System.Security.Cryptography;
using System.Text;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

public sealed class PasswordResetService(
    MasterDbContext db,
    IOptions<AuthOptions> authOptions,
    IPasswordHasher<PlayerAccount> passwordHasher)
{
    public async Task<PasswordResetIssueResult> IssueResetTokenAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var player = await db.PlayerAccounts
            .FirstOrDefaultAsync(account => account.Email == normalizedEmail, cancellationToken);

        if (player is null)
        {
            return PasswordResetIssueResult.NoAccount;
        }

        var now = DateTime.UtcNow;
        var rawToken = GenerateRawToken();
        var tokenEntity = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            TokenHash = ComputeTokenHash(rawToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(authOptions.Value.PasswordResetTokenLifetimeMinutes),
        };

        db.PasswordResetTokens.Add(tokenEntity);
        await db.SaveChangesAsync(cancellationToken);

        return new PasswordResetIssueResult(
            true,
            player.Email,
            player.DisplayName,
            rawToken);
    }

    public async Task ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Trim().Length < 8)
        {
            throw PasswordResetException.PasswordTooShort();
        }

        var tokenHash = ComputeTokenHash(token);
        var now = DateTime.UtcNow;

        var resetToken = await db.PasswordResetTokens
            .Include(entry => entry.PlayerAccount)
            .FirstOrDefaultAsync(entry => entry.TokenHash == tokenHash, cancellationToken);

        if (resetToken is null
            || resetToken.PlayerAccount is null
            || resetToken.UsedAtUtc is not null
            || resetToken.ExpiresAtUtc <= now)
        {
            throw PasswordResetException.InvalidOrExpiredToken();
        }

        resetToken.PlayerAccount.PasswordHash = passwordHasher.HashPassword(resetToken.PlayerAccount, newPassword.Trim());
        resetToken.UsedAtUtc = now;

        var otherActiveTokens = await db.PasswordResetTokens
            .Where(entry =>
                entry.PlayerAccountId == resetToken.PlayerAccountId
                && entry.Id != resetToken.Id
                && entry.UsedAtUtc == null
                && entry.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in otherActiveTokens)
        {
            activeToken.UsedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static string ComputeTokenHash(string token)
    {
        var input = token?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    internal static string BuildResetLink(string frontendUrl, string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(frontendUrl) ? "http://localhost:5173" : frontendUrl.Trim();
        var uriBuilder = new UriBuilder(baseUrl.TrimEnd('/'))
        {
            Path = "/reset-password",
            Query = $"token={Uri.EscapeDataString(token)}",
        };
        return uriBuilder.Uri.ToString();
    }

    private static string GenerateRawToken()
    {
        // Generate a URL-safe random token suitable for query-string transport in reset links.
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .TrimEnd('=');
    }
}

public sealed record PasswordResetIssueResult(
    bool AccountExists,
    string? RecipientEmail,
    string? RecipientDisplayName,
    string? RawToken)
{
    public static PasswordResetIssueResult NoAccount { get; } = new(false, null, null, null);
}

public sealed class PasswordResetException : Exception
{
    private PasswordResetException(string code, string message, int statusCode) : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }

    public string Code { get; }

    public int StatusCode { get; }

    public static PasswordResetException InvalidOrExpiredToken() =>
        new("RESET_TOKEN_INVALID_OR_EXPIRED", "This reset link is invalid or expired. Please request a new one.", StatusCodes.Status400BadRequest);

    public static PasswordResetException PasswordTooShort() =>
        new("PASSWORD_TOO_SHORT", "Password must be at least 8 characters.", StatusCodes.Status400BadRequest);
}
