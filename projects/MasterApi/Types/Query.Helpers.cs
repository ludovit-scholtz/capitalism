using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Capitalism.Shared.Security;
using HotChocolate.CostAnalysis.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Query
{

    internal static GameServerSummary ToSummary(Data.Entities.GameServerNode server, DateTime cutoff)
    {
        var keyStatus = !server.IsActive
            ? "REVOKED"
            : server.ExpiresAtUtc <= DateTime.UtcNow
                ? "EXPIRED"
                : server.ExpiresAtUtc <= DateTime.UtcNow.AddMinutes(10)
                    ? "EXPIRING_SOON"
                    : "ACTIVE";

        return new GameServerSummary
        {
            Id = server.Id,
            ServerKey = server.ServerKey,
            DisplayName = server.DisplayName,
            Description = server.Description,
            Region = server.Region,
            Environment = server.Environment,
            BackendUrl = server.BackendUrl,
            GraphqlUrl = server.GraphqlUrl,
            FrontendUrl = server.FrontendUrl,
            Version = server.Version,
            PlayerCount = server.PlayerCount,
            CompanyCount = server.CompanyCount,
            CurrentTick = server.CurrentTick,
            RegisteredAtUtc = server.RegisteredAtUtc,
            LastHeartbeatAtUtc = server.LastHeartbeatAtUtc,
            IsOnline = server.LastHeartbeatAtUtc >= cutoff,
            IsActive = server.IsActive,
            ExpiresAtUtc = server.ExpiresAtUtc,
            KeyStatus = keyStatus,
        };
    }

    internal static async Task<PlayerAccount?> GetCurrentUserAsync(ClaimsPrincipal principal, MasterDbContext db)
    {
        var idClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(idClaim, out var userId))
        {
            var playerById = await db.PlayerAccounts.FirstOrDefaultAsync(candidate => candidate.Id == userId);
            if (playerById is not null)
            {
                return playerById;
            }
        }

        var email = principal.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(email))
        {
            return await db.PlayerAccounts.FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == email);
        }

        throw new GraphQLException(
            ErrorBuilder.New()
                .SetMessage("Authenticated user identity is missing.")
                .SetCode("IDENTITY_MISSING")
                .Build());
    }

    internal static MasterPlayerProfile ToProfile(PlayerAccount player)
    {
        return new MasterPlayerProfile
        {
            Id = player.Id,
            Email = player.Email,
            DisplayName = player.DisplayName,
            PersonalAccountName = player.DisplayName,
            Gender = player.Gender,
            PreferredLocale = player.PreferredLocale,
            CreatedAtUtc = player.CreatedAtUtc,
            StartupPackClaimedAtUtc = player.StartupPackClaimedAtUtc,
            CanClaimStartupPack = player.StartupPackClaimedAtUtc is null,
            HasReferralDiscount = player.ReferredByEmail is not null,
            DeletionRequestedAtUtc = player.DeletionRequestedAtUtc,
            DeletionScheduledAtUtc = player.DeletionScheduledAtUtc,
            IsPendingDeletion = player.DeletionRequestedAtUtc is not null,
            WeeklyReportEmailSubscribed = !player.WeeklyReportEmailUnsubscribed,
        };
    }

    internal static void EnsureServiceAccess(
        MasterServerServiceInput input,
        IOptions<MasterServerOptions> masterServerOptions,
        bool requireRegistrationKey = true,
        bool requireServerKey = true
        )
    {
        if (requireRegistrationKey)
        {
            var expectedKey = masterServerOptions.Value.RegistrationKey.Trim();
            if (string.IsNullOrWhiteSpace(expectedKey)
                || !string.Equals(expectedKey, input.RegistrationKey?.Trim(), StringComparison.Ordinal))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Invalid game server registration key.")
                        .SetCode("INVALID_REGISTRATION_KEY")
                        .Build());
            }
        }
        if (requireServerKey)
        {
            if (string.IsNullOrWhiteSpace(input.ServerKey))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Server key is required.")
                        .SetCode("SERVER_KEY_REQUIRED")
                        .Build());
            }
        }
    }

    internal static async Task<GameAdministrationAccessInfo> BuildGameAdministrationAccessAsync(
        MasterDbContext db,
        GameAdministrationOptions options,
        string normalizedEmail)
    {
        var rootAdminEmails = BuildRootAdministratorEmailSet(options);
        var hasGlobalAdminRole = await db.GlobalGameAdminGrants
            .AsNoTracking()
            .AnyAsync(grant => grant.Email == normalizedEmail);
        var isRootAdministrator = rootAdminEmails.Contains(normalizedEmail);

        return new GameAdministrationAccessInfo
        {
            Email = normalizedEmail,
            IsRootAdministrator = isRootAdministrator,
            HasGlobalAdminRole = hasGlobalAdminRole,
            CanAccessEveryGameDashboard = isRootAdministrator || hasGlobalAdminRole,
        };
    }

    internal static HashSet<string> BuildRootAdministratorEmailSet(GameAdministrationOptions options)
    {
        return options.RootAdministratorEmails
            .Select(email => email?.Trim().ToLowerInvariant())
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    internal sealed record TrustedNewsServerIdentity(string ServerKey, string DisplayName);

    internal sealed record TrustedNewsAdminIdentity(string Email, GameAdministrationAccessInfo Access);

    internal static async Task<TrustedNewsServerIdentity?> TryResolveTrustedNewsServerIdentityAsync(
        MasterDbContext db,
        MasterServerOptions options,
        MasterServerServiceInput input,
        CancellationToken cancellationToken = default)
    {
        var expectedRegistrationKey = options.RegistrationKey.Trim();
        var suppliedRegistrationKey = input.RegistrationKey?.Trim();
        if (string.IsNullOrWhiteSpace(expectedRegistrationKey)
            || !string.Equals(expectedRegistrationKey, suppliedRegistrationKey, StringComparison.Ordinal))
        {
            return null;
        }

        var serverKey = input.ServerKey?.Trim();
        if (string.IsNullOrWhiteSpace(serverKey))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var serverKeyHash = ShardKeyProtector.ComputeHash(serverKey);
        var server = await db.GameServers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => (candidate.ServerKey == serverKey || candidate.ServerKeyHash == serverKeyHash)
                    && candidate.IsActive
                    && candidate.ExpiresAtUtc > now,
                cancellationToken);

        return server is null
            ? null
            : new TrustedNewsServerIdentity(server.ServerKey, server.DisplayName);
    }

    internal static async Task<TrustedNewsAdminIdentity?> TryResolvePrivilegedNewsAdminIdentityAsync(
        MasterDbContext db,
        GameAdministrationOptions options,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        string callerEmail;
        try
        {
            callerEmail = GetEmailFromClaims(principal);
        }
        catch (GraphQLException)
        {
            return null;
        }

        var access = await BuildGameAdministrationAccessAsync(db, options, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            return null;
        }

        return new TrustedNewsAdminIdentity(callerEmail, access);
    }

    internal static GameNewsEntryInfo ToGameNewsEntryInfo(
        GameNewsEntry entry,
        string? normalizedPlayerEmail,
        string serverKey,
        bool includeAuthorEmails = true)
    {
        var isRead = normalizedPlayerEmail is not null
            && entry.ReadReceipts.Any(receipt => receipt.PlayerEmail == normalizedPlayerEmail && receipt.ServerKey == serverKey);

        return new GameNewsEntryInfo
        {
            Id = entry.Id,
            EntryType = entry.EntryType,
            Status = entry.Status,
            TargetServerKey = entry.TargetServerKey,
            // Author email addresses are personal data and must stay private:
            // only trusted servers or administrators may see them.
            CreatedByEmail = includeAuthorEmails ? entry.CreatedByEmail : string.Empty,
            UpdatedByEmail = includeAuthorEmails ? entry.UpdatedByEmail : string.Empty,
            CreatedAtUtc = entry.CreatedAtUtc,
            UpdatedAtUtc = entry.UpdatedAtUtc,
            PublishedAtUtc = entry.PublishedAtUtc,
            IsRead = isRead,
            Localizations = entry.Localizations
                .OrderBy(localization => localization.Locale)
                .Select(localization => new GameNewsLocalizationInfo
                {
                    Locale = localization.Locale,
                    Title = localization.Title,
                    Summary = localization.Summary,
                    HtmlContent = localization.HtmlContent,
                })
                .ToList(),
        };
    }

    internal static SubscriptionInfo BuildSubscriptionInfo(ProSubscription? sub, DateTime now)
    {
        if (sub is null)
        {
            return new SubscriptionInfo
            {
                Tier = "FREE",
                Status = "NONE",
                IsActive = false,
                CanProlong = true,
            };
        }

        var isActive = sub.ExpiresAtUtc > now;
        var daysRemaining = isActive ? (int)Math.Ceiling((sub.ExpiresAtUtc - now).TotalDays) : 0;

        return new SubscriptionInfo
        {
            Tier = sub.Tier.ToString().ToUpperInvariant(),
            Status = isActive ? "ACTIVE" : "EXPIRED",
            StartsAtUtc = sub.StartsAtUtc,
            ExpiresAtUtc = sub.ExpiresAtUtc,
            IsActive = isActive,
            DaysRemaining = isActive ? daysRemaining : null,
            CanProlong = true,
        };
    }

    /// <summary>Extracts the normalized email from JWT claims for admin-only operations.</summary>
    internal static string GetEmailFromClaims(ClaimsPrincipal principal)
    {
        if (!string.Equals(
                principal.FindFirstValue(TokenBoundaryClaims.MasterPrivilegeEligibleClaimType),
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You don't have permission to perform this action.")
                    .SetCode("TOKEN_BOUNDARY_FORBIDDEN")
                    .Build());
        }

        var email = principal.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Authenticated user identity is missing.")
                    .SetCode("IDENTITY_MISSING")
                    .Build());
        }

        return email;
    }

    internal static string NormalizeEmail(string email, string errorCode)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A valid email address is required.")
                    .SetCode(errorCode)
                    .Build());
        }

        try
        {
            var mailAddress = new System.Net.Mail.MailAddress(normalizedEmail);
            if (!string.Equals(mailAddress.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException();
            }
        }
        catch
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A valid email address is required.")
                    .SetCode(errorCode)
                    .Build());
        }

        return normalizedEmail;
    }
}
