using System.Security.Claims;
using System.Diagnostics;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Security;
using MasterApi.Utilities;
using Capitalism.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{

    private const string RegistrationFailedCode = "REGISTRATION_FAILED";
    private const string RegistrationFailedMessage = "Registration failed.";
    private static readonly TimeSpan RegistrationMinimumDuration = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan InvalidCredentialsMinimumDuration = TimeSpan.FromMilliseconds(250);

    /// <summary>Registers a new master-portal account.</summary>
    public async Task<MasterAuthPayload> Register(
        RegisterInput input,
        [Service] MasterDbContext db,
        [Service] IOptions<JwtOptions> jwtOptions,
        [Service] IHostEnvironment hostEnvironment,
        [Service] IOptions<AuthOptions> authOptions,
        [Service] MasterRankingService rankingService,
        [Service] IMasterEmailService emailService,
        [Service] ILoginThrottleService throttle,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var startedAt = Stopwatch.GetTimestamp();
        if (!authOptions.Value.PasswordAuthEnabled)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Password-based registration is disabled. Please use the OIDC sign-in flow.")
                    .SetCode("AUTH_PASSWORD_DISABLED")
                    .Build());
        }

        var email = input.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !IsValidEmail(email))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A valid email address is required.")
                    .SetCode("INVALID_EMAIL")
                    .Build());
        }

        if (string.IsNullOrWhiteSpace(input.DisplayName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Display name is required.")
                    .SetCode("DISPLAY_NAME_REQUIRED")
                    .Build());
        }

        if (input.Password.Length < 8)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Password must be at least 8 characters.")
                    .SetCode("PASSWORD_TOO_SHORT")
                    .Build());
        }

        var existingPlayer = await db.PlayerAccounts.FirstOrDefaultAsync(p => p.Email == email);
        if (existingPlayer is not null
            && !MasterRankingService.IsShadowProvisionedPasswordHash(existingPlayer.PasswordHash))
        {
            ExecuteDummyPasswordWork(input.Password);
            await DelayToMinimumDurationAsync(startedAt, RegistrationMinimumDuration);
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(RegistrationFailedMessage)
                    .SetCode(RegistrationFailedCode)
                    .Build());
        }

        // Validate referral email if provided (referrer must be an existing account).
        string? referredByEmail = null;
        if (!string.IsNullOrWhiteSpace(input.ReferralEmail))
        {
            var normalizedReferral = input.ReferralEmail.Trim().ToLowerInvariant();
            var referrerExists = await db.PlayerAccounts.AnyAsync(p => p.Email == normalizedReferral);
            if (referrerExists)
            {
                referredByEmail = normalizedReferral;
            }
        }

        var now = DateTime.UtcNow;
        var hasher = new PasswordHasher<PlayerAccount>();
        var shouldAwardReferralBounty = false;
        PlayerAccount player;

        if (existingPlayer is not null)
        {
            existingPlayer.DisplayName = input.DisplayName.Trim();
            existingPlayer.PasswordHash = hasher.HashPassword(existingPlayer, input.Password);

            if (existingPlayer.ReferredByEmail is null && referredByEmail is not null)
            {
                existingPlayer.ReferredByEmail = referredByEmail;
                shouldAwardReferralBounty = true;
            }

            player = existingPlayer;
        }
        else
        {
            player = new PlayerAccount
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = input.DisplayName.Trim(),
                CreatedAtUtc = now,
                ReferredByEmail = referredByEmail,
            };

            player.PasswordHash = hasher.HashPassword(player, input.Password);
            db.PlayerAccounts.Add(player);
            shouldAwardReferralBounty = referredByEmail is not null;
        }

        player.PreferredLocale = EmailLocalizations.NormalizeLocale(input.Locale);
        player.PreferredLocaleUpdatedAtUtc = now;
        player.LastAccessedUrl = ResolveAccessedUrl(input.CurrentUrl, httpContextAccessor.HttpContext);

        await db.SaveChangesAsync();

        // Award RECOMMEND_FRIEND bounty to the referrer if applicable.
        if (shouldAwardReferralBounty && referredByEmail is not null)
        {
            try
            {
                await rankingService.IngestEventAsync(
                    MasterRankingBountyCodes.RecommendFriend,
                    referredByEmail,
                    serverKey: null,
                    externalEventId: null,
                    uniqueScopeKey: player.Id.ToString(),
                    idempotencyKey: null,
                    proofReference: null,
                    payloadJson: "{}",
                    occurredAtUtc: now,
                    cancellationToken: CancellationToken.None);
            }
            catch
            {
                // Referral bounty failure must never block registration.
            }
        }

        var session = GenerateToken(player, jwtOptions.Value);
        await TrackIssuedSessionAsync(
            db,
            player.Id,
            session.Token,
            session.ExpiresAtUtc,
            httpContextAccessor.HttpContext,
            httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        AuthSessionCookieService.SetSessionCookies(httpContextAccessor.HttpContext, hostEnvironment, session.Token, session.ExpiresAtUtc);
        await emailService.SendRegistrationEmailIfNeededAsync(
            player,
            ResolveAccessedUrl(input.CurrentUrl, httpContextAccessor.HttpContext),
            input.Locale,
            httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        await DelayToMinimumDurationAsync(startedAt, RegistrationMinimumDuration);

        return new MasterAuthPayload
        {
            Token = session.Token,
            ExpiresAtUtc = session.ExpiresAtUtc,
            Player = Query.ToProfile(player),
        };
    }

    /// <summary>Authenticates an existing master-portal account.</summary>
    public async Task<MasterAuthPayload> Login(
        LoginInput input,
        [Service] MasterDbContext db,
        [Service] IOptions<JwtOptions> jwtOptions,
        [Service] IHostEnvironment hostEnvironment,
        [Service] IOptions<AuthOptions> authOptions,
        [Service] ILoginThrottleService throttle,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var startedAt = Stopwatch.GetTimestamp();
        if (!authOptions.Value.PasswordAuthEnabled)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Password-based login is disabled. Please use the OIDC sign-in flow.")
                    .SetCode("AUTH_PASSWORD_DISABLED")
                    .Build());
        }

        var email = input.Email.Trim().ToLowerInvariant();

        // Check throttle BEFORE hitting the database to prevent oracle-style timing attacks.
        if (throttle.IsThrottled(email))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Too many failed login attempts. Please wait before trying again.")
                    .SetCode("LOGIN_THROTTLED")
                    .Build());
        }

        var player = await db.PlayerAccounts.FirstOrDefaultAsync(p => p.Email == email);
        if (player is null)
        {
            ExecuteDummyPasswordWork(input.Password);
            throttle.RecordFailure(email);
            await DelayToMinimumDurationAsync(startedAt, InvalidCredentialsMinimumDuration);
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid credentials.")
                    .SetCode("INVALID_CREDENTIALS")
                    .Build());
        }

        var hasher = new PasswordHasher<PlayerAccount>();
        var result = hasher.VerifyHashedPassword(player, player.PasswordHash, input.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throttle.RecordFailure(email);
            await DelayToMinimumDurationAsync(startedAt, InvalidCredentialsMinimumDuration);
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid credentials.")
                    .SetCode("INVALID_CREDENTIALS")
                    .Build());
        }

        // Successful login — clear the failure counter.
        throttle.RecordSuccess(email);

        player.LastLoginAtUtc = DateTime.UtcNow;
        player.PreferredLocale = EmailLocalizations.NormalizeLocale(input.Locale);
        player.PreferredLocaleUpdatedAtUtc = DateTime.UtcNow;
        player.LastAccessedUrl = ResolveAccessedUrl(input.CurrentUrl, httpContextAccessor.HttpContext);
        await db.SaveChangesAsync();

        var session = GenerateToken(player, jwtOptions.Value);
        await TrackIssuedSessionAsync(
            db,
            player.Id,
            session.Token,
            session.ExpiresAtUtc,
            httpContextAccessor.HttpContext,
            httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        AuthSessionCookieService.SetSessionCookies(httpContextAccessor.HttpContext, hostEnvironment, session.Token, session.ExpiresAtUtc);

        return new MasterAuthPayload
        {
            Token = session.Token,
            ExpiresAtUtc = session.ExpiresAtUtc,
            Player = Query.ToProfile(player),
        };
    }

    private static string? ResolveAccessedUrl(string? inputUrl, HttpContext? httpContext)
    {
        // This URL is recorded for email context only and must not drive authorization decisions.
        var url = !string.IsNullOrWhiteSpace(inputUrl)
            ? inputUrl
            : httpContext?.Request.Headers.Referer.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private static void ExecuteDummyPasswordWork(string? password)
    {
        var candidate = string.IsNullOrWhiteSpace(password) ? "dummy-password-123!" : password;
        var hasher = new PasswordHasher<PlayerAccount>();
        var dummyPlayer = new PlayerAccount
        {
            Id = Guid.Empty,
            Email = "dummy@example.com",
            DisplayName = "dummy",
            CreatedAtUtc = DateTime.UtcNow,
        };
        var hash = hasher.HashPassword(dummyPlayer, candidate);
        _ = hasher.VerifyHashedPassword(dummyPlayer, hash, candidate);
    }

    private static async Task DelayToMinimumDurationAsync(long startedAtTimestamp, TimeSpan minimumDuration)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAtTimestamp);
        var remaining = minimumDuration - elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }
    }
}
