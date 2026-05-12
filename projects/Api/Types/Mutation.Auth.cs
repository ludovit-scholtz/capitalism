using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Types;

public sealed partial class Mutation
{
    private const int ReferralCodeLength = 8;
    private const int MaxReferralCodeGenerationAttempts = 20;
    private const string ReferralCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>Compiled regex for validating alphanumeric referral codes.</summary>
    private static readonly Regex ReferralCodePattern = new(@"^[A-Z0-9]+$", RegexOptions.Compiled);
    /// <summary>Registers a new player account and returns an auth token.</summary>
    public async Task<AuthPayload> Register(
        RegisterInput input,
        [Service] AppDbContext db,
        [Service] IOptions<JwtOptions> jwtOptions,
        [Service] IOptions<AuthOptions> authOptions,
        [Service] IMasterRankingTelemetryService rankingTelemetry,
        [Service] IOptions<MasterServerRegistrationOptions> masterOptions,
        [Service] ILoginThrottleService throttle)
    {
        if (!authOptions.Value.PasswordAuthEnabled)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Password-based registration is disabled on this server. Please use the OIDC sign-in flow.")
                    .SetCode("AUTH_PASSWORD_DISABLED")
                    .Build());
        }

        var normalizedEmail = input.Email.Trim().ToLowerInvariant();

        if (await db.Players.AnyAsync(p => p.Email.ToLower() == normalizedEmail))
        {
            // Return a neutral message so callers cannot enumerate registered email addresses.
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("An account with that email may already exist. Please check your inbox or try signing in.")
                    .SetCode("DUPLICATE_EMAIL")
                    .Build());
        }

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = input.DisplayName.Trim(),
            Role = PlayerRole.Player,
            ActiveAccountType = AccountContextType.Person,
            CreatedAtUtc = DateTime.UtcNow,
            AppliedReferralCode = NormalizeReferralCode(input.ReferralCode)
        };

        var hasher = new PasswordHasher<Player>();
        player.PasswordHash = hasher.HashPassword(player, input.Password);

        db.Players.Add(player);
        await PersonalBankAccountService.EnsureTrackedAccountAsync(db, player.Id, "USD", 200_000m);
        await db.SaveChangesAsync();
        FireLoginTelemetry(rankingTelemetry, masterOptions.Value.ServerKey, player.Email);

        var session = GenerateToken(player, jwtOptions.Value);
        return new AuthPayload
        {
            Token = session.Token,
            ExpiresAtUtc = session.ExpiresAtUtc,
            Player = player
        };
    }

    /// <summary>Authenticates a player and returns an auth token.</summary>
    public async Task<AuthPayload> Login(
        LoginInput input,
        [Service] AppDbContext db,
        [Service] IOptions<JwtOptions> jwtOptions,
        [Service] IOptions<AuthOptions> authOptions,
        [Service] IMasterRankingTelemetryService rankingTelemetry,
        [Service] IOptions<MasterServerRegistrationOptions> masterOptions,
        [Service] ILoginThrottleService throttle)
    {
        if (!authOptions.Value.PasswordAuthEnabled)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Password-based login is disabled on this server. Please use the OIDC sign-in flow.")
                    .SetCode("AUTH_PASSWORD_DISABLED")
                    .Build());
        }

        var normalizedEmail = input.Email.Trim().ToLowerInvariant();

        // Check throttle BEFORE hitting the database to prevent oracle-style timing attacks.
        if (throttle.IsThrottled(normalizedEmail))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Too many failed login attempts. Please wait before trying again.")
                    .SetCode("LOGIN_THROTTLED")
                    .Build());
        }

        var player = await db.Players.FirstOrDefaultAsync(p => p.Email.ToLower() == normalizedEmail);
        if (player is null)
        {
            // Record failure even for unknown emails to prevent timing-based enumeration.
            throttle.RecordFailure(normalizedEmail);
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid email or password.")
                    .SetCode("INVALID_CREDENTIALS")
                    .Build());
        }

        var hasher = new PasswordHasher<Player>();
        var result = hasher.VerifyHashedPassword(player, player.PasswordHash, input.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            throttle.RecordFailure(normalizedEmail);
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid email or password.")
                    .SetCode("INVALID_CREDENTIALS")
                    .Build());
        }

        // Successful login — clear the failure counter.
        throttle.RecordSuccess(normalizedEmail);

        if (!string.Equals(player.Email, normalizedEmail, StringComparison.Ordinal))
        {
            player.Email = normalizedEmail;
        }

        player.LastLoginAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        FireLoginTelemetry(rankingTelemetry, masterOptions.Value.ServerKey, player.Email);

        var session = GenerateToken(player, jwtOptions.Value);
        return new AuthPayload
        {
            Token = session.Token,
            ExpiresAtUtc = session.ExpiresAtUtc,
            Player = player
        };
    }

    /// <summary>
    /// Generates a unique referral code for the authenticated player.
    /// Returns the existing code if one is already assigned.
    /// </summary>
    [Authorize]
    public async Task<string> GenerateReferralCode(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var existing = await db.ReferralCodes
            .AsNoTracking()
            .Where(code => code.CreatorPlayerId == userId)
            .Select(code => code.Code)
            .FirstOrDefaultDeterministicAsync();

        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var generatedCode = await GenerateUniqueReferralCodeAsync(db, ReferralCodeLength, httpContextAccessor.HttpContext.RequestAborted);
        db.ReferralCodes.Add(new ReferralCode
        {
            Id = Guid.NewGuid(),
            Code = generatedCode,
            CreatorPlayerId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            UsageCount = 0
        });
        await db.SaveChangesAsync(httpContextAccessor.HttpContext.RequestAborted);
        return generatedCode;
    }

    [Authorize]
    public async Task<AuthPayload> StartAdminImpersonation(
        StartAdminImpersonationInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IOptions<JwtOptions> jwtOptions,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        var principal = httpContextAccessor.HttpContext!.User;
        var accessContext = await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(db, principal, httpContextAccessor.HttpContext.RequestAborted);
        var targetPlayer = await db.Players
            .AsNoTracking()
            .Include(player => player.Companies)
            .FirstOrDefaultAsync(player => player.Id == input.TargetPlayerId, httpContextAccessor.HttpContext.RequestAborted)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var impersonationContext = ResolveImpersonationAccountContext(targetPlayer, input);
        targetPlayer.ActiveAccountType = impersonationContext.EffectiveAccountType;
        targetPlayer.ActiveCompanyId = impersonationContext.EffectiveCompanyId;

        var session = GenerateToken(accessContext.ActorPlayer, jwtOptions.Value, new AdminImpersonationTokenContext(
            targetPlayer,
            impersonationContext.EffectiveAccountType,
            impersonationContext.EffectiveCompanyId,
            impersonationContext.EffectiveCompanyName));

        return new AuthPayload
        {
            Token = session.Token,
            ExpiresAtUtc = session.ExpiresAtUtc,
            Player = targetPlayer,
        };
    }

    [Authorize]
    public async Task<AuthPayload> StopAdminImpersonation(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IOptions<JwtOptions> jwtOptions)
    {
        var actorUserId = httpContextAccessor.HttpContext!.User.GetAuthenticatedActorUserId();
        var actorPlayer = await db.Players
            .AsNoTracking()
            .Include(player => player.Companies)
            .FirstOrDefaultAsync(player => player.Id == actorUserId, httpContextAccessor.HttpContext.RequestAborted)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var session = GenerateToken(actorPlayer, jwtOptions.Value);
        return new AuthPayload
        {
            Token = session.Token,
            ExpiresAtUtc = session.ExpiresAtUtc,
            Player = actorPlayer,
        };
    }

    private static void FireLoginTelemetry(
        IMasterRankingTelemetryService rankingTelemetry,
        string? serverKey,
        string playerEmail)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var normalizedServerKey = serverKey ?? string.Empty;
        _ = rankingTelemetry.ReportEventAsync(
            MasterRankingBountyCodes.LoginToGame,
            playerEmail,
            uniqueScopeKey: $"{MasterRankingBountyCodes.LoginToGame}:{playerEmail}:{today}:{normalizedServerKey}");
    }

    /// <summary>Normalizes and validates a referral code from user input.</summary>
    private static string? NormalizeReferralCode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = raw.Trim().ToUpperInvariant();
        // Allow alphanumeric characters only, 4-20 chars
        if (normalized.Length < 4 || normalized.Length > 20 || !ReferralCodePattern.IsMatch(normalized))
        {
            return null;
        }

        return normalized;
    }

    private static async Task<string> GenerateUniqueReferralCodeAsync(
        AppDbContext db,
        int length,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        for (var attempt = 0; attempt < MaxReferralCodeGenerationAttempts; attempt++)
        {
            RandomNumberGenerator.Fill(buffer);
            var codeChars = new char[length];
            for (var index = 0; index < length; index++)
            {
                codeChars[index] = ReferralCodeAlphabet[buffer[index] % ReferralCodeAlphabet.Length];
            }

            var candidate = new string(codeChars);
            var exists = await db.ReferralCodes
                .AsNoTracking()
                .AnyAsync(code => code.Code == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new GraphQLException(
            ErrorBuilder.New()
                .SetMessage("Unable to generate a unique referral code. Please retry.")
                .SetCode("REFERRAL_CODE_GENERATION_FAILED")
                .Build());
    }
}
