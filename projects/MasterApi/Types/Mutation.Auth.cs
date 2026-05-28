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
        var url = !string.IsNullOrWhiteSpace(inputUrl)
            ? inputUrl
            : httpContext?.Request.Headers.Referer.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();
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

    [HotChocolate.Authorization.Authorize]
    public async Task<UpdatePersonalAccountNamePayload> UpdatePersonalAccountName(
        UpdatePersonalAccountNameInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] PersonalAccountNamePropagationService propagationService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var ct = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var personalAccountName = input.PersonalAccountName?.Trim();
        if (string.IsNullOrWhiteSpace(personalAccountName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Personal account name is required.")
                    .SetCode("PERSONAL_ACCOUNT_NAME_REQUIRED")
                    .Build());
        }

        if (personalAccountName.Length > 40)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Personal account name must be 40 characters or fewer.")
                    .SetCode("PERSONAL_ACCOUNT_NAME_TOO_LONG")
                    .Build());
        }

        string? normalizedGender = null;
        if (input.Gender is not null)
        {
            var trimmedGender = input.Gender.Trim().ToUpperInvariant();
            if (!PlayerGender.IsValid(trimmedGender))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Gender must be MALE, FEMALE, or UNSPECIFIED.")
                        .SetCode("INVALID_GENDER")
                        .Build());
            }

            normalizedGender = trimmedGender;
        }

        var duplicateExists = await db.PlayerAccounts
            .AsNoTracking()
            .AnyAsync(candidate =>
                candidate.Id != player.Id
                && candidate.DisplayName.ToLower() == personalAccountName.ToLower());
        if (duplicateExists)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This personal account name is already taken.")
                    .SetCode("PERSONAL_ACCOUNT_NAME_NOT_UNIQUE")
                    .Build());
        }

        player.DisplayName = personalAccountName;
        if (normalizedGender is not null)
        {
            player.Gender = normalizedGender;
        }
        await db.SaveChangesAsync(ct);
        await propagationService.PropagateAsync(player.Email, player.DisplayName, player.Gender, ct);

        return new UpdatePersonalAccountNamePayload
        {
            PlayerId = player.Id,
            PersonalAccountName = player.DisplayName,
            Gender = player.Gender,
        };
    }

    /// <summary>Prolongs (or creates) a Pro subscription for the authenticated player.</summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<SubscriptionInfo> ProlongSubscription(
        ProlongSubscriptionInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] MasterRankingService rankingService)
    {
        if (input.Months < 1 || input.Months > 12)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Months must be between 1 and 12.")
                    .SetCode("INVALID_MONTHS")
                    .Build());
        }

        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        var userId = player.Id;
        var now = DateTime.UtcNow;
        var hasReferralDiscount = player.ReferredByEmail is not null;
        var monthlyPrice = ResolveGoldPriceWithReferralDiscount(MonthlyProPriceGold, hasReferralDiscount);
        var totalPrice = decimal.Round(monthlyPrice * input.Months, 8, MidpointRounding.AwayFromZero);
        EnsureSufficientGoldBalance(player, totalPrice, "Not enough tokenized gold for monthly Pro purchase.");
        var balanceBeforeCharge = player.GoldTokenBalance;
        ApplyGoldDebit(player, totalPrice);
        AddSystemGoldTransaction(
            db,
            player,
            -totalPrice,
            balanceBeforeCharge,
            $"PRO_SUBSCRIPTION_PURCHASE:{input.Months}M");

        var latestSub = await GetLatestSubscriptionAsync(db, userId);
        var resultSub = GrantOrCreateProSubscription(db, userId, now, input.Months, latestSub);

        await db.SaveChangesAsync();

        // Award RECOMMEND_GOOD_FRIEND bounty to the referrer when a referred player subscribes.
        if (player.ReferredByEmail is not null)
        {
            try
            {
                await rankingService.IngestEventAsync(
                    MasterRankingBountyCodes.RecommendGoodFriend,
                    player.ReferredByEmail,
                    serverKey: null,
                    externalEventId: null,
                    uniqueScopeKey: $"sub:{player.Id}",
                    idempotencyKey: null,
                    proofReference: null,
                    payloadJson: "{}",
                    occurredAtUtc: now,
                    cancellationToken: CancellationToken.None);
            }
            catch
            {
                // Bounty failure must never block the subscription flow.
            }
        }

        return Query.BuildSubscriptionInfo(resultSub, now);
    }

    /// <summary>Claims the one-time startup pack on the master portal and grants 3 months of Pro access.</summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<SubscriptionInfo> ClaimStartupPack(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] MasterRankingService rankingService)
    {
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        var userId = player.Id;
        var now = DateTime.UtcNow;
        var hasReferralDiscount = player.ReferredByEmail is not null;
        var startupPackPrice = ResolveGoldPriceWithReferralDiscount(StartupPackPriceGold, hasReferralDiscount);

        var latestSub = await GetLatestSubscriptionAsync(db, userId);

        if (player.StartupPackClaimedAtUtc is not null)
        {
            return Query.BuildSubscriptionInfo(latestSub, now);
        }

        EnsureSufficientGoldBalance(player, startupPackPrice, "Not enough tokenized gold for startup pack purchase.");
        var balanceBeforeCharge = player.GoldTokenBalance;
        ApplyGoldDebit(player, startupPackPrice);
        AddSystemGoldTransaction(
            db,
            player,
            -startupPackPrice,
            balanceBeforeCharge,
            "STARTUP_PACK_PURCHASE:3M");

        var resultSub = GrantOrCreateProSubscription(db, userId, now, StartupPackDurationMonths, latestSub);
        player.StartupPackClaimedAtUtc = now;

        await db.SaveChangesAsync();

        // Award RECOMMEND_GOOD_FRIEND bounty to the referrer when a referred player claims the startup pack.
        if (player.ReferredByEmail is not null)
        {
            try
            {
                await rankingService.IngestEventAsync(
                    MasterRankingBountyCodes.RecommendGoodFriend,
                    player.ReferredByEmail,
                    serverKey: null,
                    externalEventId: null,
                    uniqueScopeKey: $"startup:{player.Id}",
                    idempotencyKey: null,
                    proofReference: null,
                    payloadJson: "{}",
                    occurredAtUtc: now,
                    cancellationToken: CancellationToken.None);
            }
            catch
            {
                // Bounty failure must never block the startup pack claim flow.
            }
        }

        return Query.BuildSubscriptionInfo(resultSub, now);
    }
}
