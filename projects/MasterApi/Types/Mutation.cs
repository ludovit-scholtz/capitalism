using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Security;
using Capitalism.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MasterApi.Types;

public sealed partial class Mutation
{
    private const int StartupPackDurationMonths = 3;
    internal const decimal MonthlyProPriceGold = 0.137m;
    internal const decimal StartupPackPriceGold = 0.274m;
    internal const decimal ReferralDiscountFraction = 0.10m;
    internal const long VoiTokenizedGoldAssetId = 302228;
    internal const long AlgorandTokenizedGoldAssetId = 1241944285;


    private static string NormalizeRequiredUrl(string url, string errorCode)
    {
        var trimmedUrl = url.Trim();
        if (string.IsNullOrWhiteSpace(trimmedUrl)
            || !Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A valid absolute URL is required.")
                    .SetCode(errorCode)
                    .Build());
        }

        return trimmedUrl.TrimEnd('/');
    }

    private static (string Token, DateTime ExpiresAtUtc) GenerateToken(PlayerAccount player, JwtOptions options)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var issuedAtUtc = DateTime.UtcNow;
        var expires = issuedAtUtc.AddMinutes(options.ExpiresMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new Claim(ClaimTypes.Email, player.Email),
            new Claim(ClaimTypes.Name, player.DisplayName),
            new Claim(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeMaster),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(issuedAtUtc).ToString(), ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    private static async Task<ProSubscription?> GetLatestSubscriptionAsync(MasterDbContext db, Guid userId)
    {
        return await db.ProSubscriptions
            .Where(subscription => subscription.PlayerAccountId == userId)
            .OrderByDescending(subscription => subscription.ExpiresAtUtc)
            .FirstOrDefaultAsync();
    }

    private static ProSubscription GrantOrCreateProSubscription(
        MasterDbContext db,
        Guid userId,
        DateTime now,
        int months,
        ProSubscription? latestSub)
    {
        if (latestSub is not null
            && latestSub.Status == SubscriptionStatus.Active
            && latestSub.ExpiresAtUtc > now)
        {
            latestSub.ExpiresAtUtc = latestSub.ExpiresAtUtc.AddMonths(months);
            latestSub.UpdatedAtUtc = now;
            return latestSub;
        }

        if (latestSub is not null && latestSub.Status == SubscriptionStatus.Active)
        {
            latestSub.Status = SubscriptionStatus.Expired;
            latestSub.UpdatedAtUtc = now;
        }

        var newSub = new ProSubscription
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = userId,
            Tier = SubscriptionTier.Pro,
            Status = SubscriptionStatus.Active,
            StartsAtUtc = now,
            ExpiresAtUtc = now.AddMonths(months),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.ProSubscriptions.Add(newSub);
        return newSub;
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    internal static decimal ResolveGoldPriceWithReferralDiscount(decimal basePriceGold, bool hasReferralDiscount)
    {
        if (!hasReferralDiscount)
        {
            return basePriceGold;
        }

        return decimal.Round(basePriceGold * (1m - ReferralDiscountFraction), 8, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeGoldNetwork(string? network)
    {
        var normalized = network?.Trim().ToUpperInvariant();
        if (normalized is "VOI" or "ALGORAND")
        {
            return normalized;
        }

        throw new GraphQLException(
            ErrorBuilder.New()
                .SetMessage("Network must be VOI or ALGORAND.")
                .SetCode("INVALID_NETWORK")
                .Build());
    }

    private static long ResolveAssetIdByNetwork(string network)
    {
        return network == "VOI" ? VoiTokenizedGoldAssetId : AlgorandTokenizedGoldAssetId;
    }

    private static void EnsurePositiveGoldAmount(decimal amount)
    {
        if (amount <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Amount must be greater than zero.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }
    }

    private static void EnsureSufficientGoldBalance(PlayerAccount player, decimal amount, string errorMessage)
    {
        if (player.GoldTokenBalance < amount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(errorMessage)
                    .SetCode("INSUFFICIENT_GOLD_BALANCE")
                    .Build());
        }
    }

    private static void ApplyGoldDebit(PlayerAccount player, decimal amount)
    {
        if (player.GoldTokenBalance - amount < 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Operation would result in a negative gold balance.")
                    .SetCode("INSUFFICIENT_GOLD_BALANCE")
                    .Build());
        }

        player.GoldTokenBalance -= amount;
        player.ConcurrencyToken = Guid.NewGuid();
    }

    private static void AddSystemGoldTransaction(
        MasterDbContext db,
        PlayerAccount player,
        decimal amount,
        decimal balanceBefore,
        string note)
    {
        db.GoldTokenTransactions.Add(new GoldTokenTransaction
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = player.Id,
            PlayerEmail = player.Email,
            Amount = amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = player.GoldTokenBalance,
            AdminEmail = "system@capitalism.master",
            Note = note,
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static string NormalizeLocale(string locale)
    {
        var normalizedLocale = locale.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedLocale))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Localization locale is required.")
                    .SetCode("INVALID_LOCALE")
                    .Build());
        }

        return normalizedLocale;
    }

    private static async Task TrackIssuedSessionAsync(
        MasterDbContext db,
        Guid playerAccountId,
        string token,
        DateTime expiresAtUtc,
        HttpContext? httpContext,
        CancellationToken cancellationToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var jti = jwt.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Jti)?.Value ?? jwt.Id;
        if (string.IsNullOrWhiteSpace(jti))
        {
            return;
        }

        var existing = await db.MasterPlayerSessions.FirstOrDefaultAsync(candidate => candidate.Jti == jti, cancellationToken);
        if (existing is not null)
        {
            existing.LastSeenAtUtc = DateTime.UtcNow;
            existing.ExpiresAtUtc = expiresAtUtc;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        db.MasterPlayerSessions.Add(new MasterPlayerSession
        {
            Jti = jti,
            PlayerAccountId = playerAccountId,
            IssuedAtUtc = jwt.ValidFrom,
            ExpiresAtUtc = expiresAtUtc,
            LastSeenAtUtc = DateTime.UtcNow,
            LastSeenIpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
        });
        await db.SaveChangesAsync(cancellationToken);
    }


}
