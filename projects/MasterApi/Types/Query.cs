using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Capitalism.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Query
{
    public async Task<List<GameServerSummary>> GetGameServers(
        [Service] MasterDbContext db,
        [Service] IOptions<MasterServerOptions> options)
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-Math.Max(5, options.Value.ActiveThresholdSeconds));

        var servers = await db.GameServers
            .OrderByDescending(server => server.LastHeartbeatAtUtc)
            .ThenBy(server => server.DisplayName)
            .ToListAsync();

        return servers
            .Select(server => ToSummary(server, cutoff))
            .OrderByDescending(server => server.IsOnline)
            .ThenBy(server => server.DisplayName)
            .ToList();
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<MasterPlayerProfile> GetMe(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        return ToProfile(player);
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<SubscriptionInfo> GetMySubscription(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        var userId = player.Id;
        var now = DateTime.UtcNow;

        // Return the most recent subscription regardless of DB status so that
        // players with an expired Pro plan see "EXPIRED" rather than "FREE/NONE".
        // BuildSubscriptionInfo uses the expiry timestamp to compute the live state.
        var latestSub = await db.ProSubscriptions
            .Where(s => s.PlayerAccountId == userId)
            .OrderByDescending(s => s.ExpiresAtUtc)
            .FirstOrDefaultAsync();

        return BuildSubscriptionInfo(latestSub, now);
    }

    public async Task<GameAdministrationAccessInfo> GetGameAdministrationAccess(
        GetGameAdministrationAccessInput input,
        [Service] MasterDbContext db,
        [Service] IOptions<MasterServerOptions> masterServerOptions,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        EnsureServiceAccess(input, masterServerOptions);
        var email = NormalizeEmail(input.Email, "INVALID_EMAIL");

        return await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, email);
    }

    public async Task<List<GlobalGameAdminGrantInfo>> GetGlobalGameAdminGrants(
        GetGlobalGameAdminGrantsInput input,
        [Service] MasterDbContext db,
        [Service] IOptions<MasterServerOptions> masterServerOptions,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        EnsureServiceAccess(input, masterServerOptions);

        var requesterEmail = NormalizeEmail(input.RequesterEmail, "INVALID_REQUESTER_EMAIL");
        var requesterAccess = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, requesterEmail);
        if (!requesterAccess.IsRootAdministrator)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only root administrators can manage global game administrators.")
                    .SetCode("ROOT_ADMIN_REQUIRED")
                    .Build());
        }

        return await db.GlobalGameAdminGrants
            .AsNoTracking()
            .OrderBy(grant => grant.Email)
            .Select(grant => new GlobalGameAdminGrantInfo
            {
                Id = grant.Id,
                Email = grant.Email,
                GrantedByEmail = grant.GrantedByEmail,
                GrantedAtUtc = grant.GrantedAtUtc,
                UpdatedAtUtc = grant.UpdatedAtUtc,
            })
            .ToListAsync();
    }
    public async Task<GameNewsFeedResult> GetGameNewsFeed(
        [Service] MasterDbContext db,
        [Service] IOptions<MasterServerOptions> masterServerOptions,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        ClaimsPrincipal claimsPrincipal,
        GetGameNewsFeedInput? input = null
        )
    {
        if (input is null)
        {
            input = new GetGameNewsFeedInput
            {
                ServerKey = string.Empty,
                IncludeDrafts = false,
                Limit = 20,
            };
        }
        var includeDrafts = false;
        if (input.IncludeDrafts)
        {
            var trustedServer = await TryResolveTrustedNewsServerIdentityAsync(db, masterServerOptions.Value, input);
            var trustedAdmin = await TryResolvePrivilegedNewsAdminIdentityAsync(db, gameAdministrationOptions.Value, claimsPrincipal);
            includeDrafts = trustedServer is not null || trustedAdmin is not null;
        }

        var playerEmail = string.IsNullOrWhiteSpace(input.PlayerEmail)
            ? null
            : NormalizeEmail(input.PlayerEmail, "INVALID_PLAYER_EMAIL");
        var limit = Math.Clamp(input.Limit, 1, 500);

        var entries = await db.GameNewsEntries
            .AsNoTracking()
            .Include(entry => entry.Localizations)
            .Include(entry => entry.ReadReceipts)
            .Where(entry => entry.TargetServerKey == null || entry.TargetServerKey == input.ServerKey)
            .Where(entry => includeDrafts || entry.Status == GameNewsEntryStatus.Published)
            .OrderByDescending(entry => entry.PublishedAtUtc ?? entry.UpdatedAtUtc)
            .ThenByDescending(entry => entry.CreatedAtUtc)
            .Take(limit)
            .AsSplitQuery()
            .ToListAsync();

        var items = entries
            .Select(entry => ToGameNewsEntryInfo(entry, playerEmail, input.ServerKey))
            .ToList();

        var unreadCount = 0;
        if (playerEmail is not null)
        {
            var publishedEntryIds = await db.GameNewsEntries
                .AsNoTracking()
                .Where(entry => entry.Status == GameNewsEntryStatus.Published)
                .Where(entry => entry.TargetServerKey == null || entry.TargetServerKey == input.ServerKey)
                .Select(entry => entry.Id)
                .ToListAsync();

            if (publishedEntryIds.Count > 0)
            {
                var readEntryIds = await db.GameNewsReadReceipts
                    .AsNoTracking()
                    .Where(receipt => receipt.PlayerEmail == playerEmail && receipt.ServerKey == input.ServerKey)
                    .Where(receipt => publishedEntryIds.Contains(receipt.GameNewsEntryId))
                    .Select(receipt => receipt.GameNewsEntryId)
                    .Distinct()
                    .CountAsync();

                unreadCount = Math.Max(0, publishedEntryIds.Count - readEntryIds);
            }
        }
        
        return new GameNewsFeedResult
        {
            Items = items,
            UnreadCount = unreadCount,
        };
    }

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
            CreatedAtUtc = player.CreatedAtUtc,
            StartupPackClaimedAtUtc = player.StartupPackClaimedAtUtc,
            CanClaimStartupPack = player.StartupPackClaimedAtUtc is null,
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
        string serverKey)
    {
        var isRead = normalizedPlayerEmail is not null
            && entry.ReadReceipts.Any(receipt => receipt.PlayerEmail == normalizedPlayerEmail && receipt.ServerKey == serverKey);

        return new GameNewsEntryInfo
        {
            Id = entry.Id,
            EntryType = entry.EntryType,
            Status = entry.Status,
            TargetServerKey = entry.TargetServerKey,
            CreatedByEmail = entry.CreatedByEmail,
            UpdatedByEmail = entry.UpdatedByEmail,
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

    [HotChocolate.Authorization.Authorize]
    public async Task<List<BuildingLayoutTemplateInfo>> GetMyBuildingLayouts(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        var userId = player.Id;

        return await db.BuildingLayoutTemplates
            .AsNoTracking()
            .Where(l => l.PlayerAccountId == userId)
            .OrderByDescending(l => l.UpdatedAtUtc)
            .Select(l => new BuildingLayoutTemplateInfo
            {
                Id = l.Id,
                Name = l.Name,
                Description = l.Description,
                BuildingType = l.BuildingType,
                UnitsJson = l.UnitsJson,
                CreatedAtUtc = l.CreatedAtUtc,
                UpdatedAtUtc = l.UpdatedAtUtc,
            })
            .ToListAsync();
    }

    /// <summary>Returns the authenticated player's own gold token account details (balance and recent transactions).</summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<PlayerGoldAccountInfo> GetMyGoldAccount(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var recentTx = await db.GoldTokenTransactions
            .AsNoTracking()
            .Where(tx => tx.PlayerAccountId == player.Id)
            .OrderByDescending(tx => tx.CreatedAtUtc)
            .Take(10)
            .Select(tx => new PlayerGoldTransactionInfo
            {
                Id = tx.Id,
                Amount = tx.Amount,
                BalanceBefore = tx.BalanceBefore,
                BalanceAfter = tx.BalanceAfter,
                Note = tx.Note,
                CreatedAtUtc = tx.CreatedAtUtc,
            })
            .ToListAsync();

        return new PlayerGoldAccountInfo
        {
            GoldTokenBalance = player.GoldTokenBalance,
            LastUpdatedAtUtc = recentTx.Count > 0 ? recentTx[0].CreatedAtUtc : null,
            RecentTransactions = recentTx,
        };
    }

    /// <summary>Returns all player accounts with their gold token balances. Requires global admin or root admin.</summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<List<GoldTokenBalanceInfo>> GetGoldTokenBalances(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Gold token administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        return await db.PlayerAccounts
            .AsNoTracking()
            .OrderBy(p => p.Email)
            .Select(p => new GoldTokenBalanceInfo
            {
                PlayerId = p.Id,
                Email = p.Email,
                DisplayName = p.DisplayName,
                GoldTokenBalance = p.GoldTokenBalance,
            })
            .ToListAsync();
    }

    /// <summary>Returns recent gold token transactions (audit log). Requires global admin or root admin.</summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<List<GoldTokenTransactionInfo>> GetGoldTokenTransactions(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        string? targetEmail = null,
        int limit = 50)
    {
        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Gold token administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var clampedLimit = Math.Clamp(limit, 1, 200);
        var query = db.GoldTokenTransactions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(targetEmail))
        {
            var normalized = targetEmail.Trim().ToLowerInvariant();
            query = query.Where(tx => tx.PlayerEmail == normalized);
        }

        return await query
            .OrderByDescending(tx => tx.CreatedAtUtc)
            .Take(clampedLimit)
            .Select(tx => new GoldTokenTransactionInfo
            {
                Id = tx.Id,
                PlayerEmail = tx.PlayerEmail,
                Amount = tx.Amount,
                BalanceBefore = tx.BalanceBefore,
                BalanceAfter = tx.BalanceAfter,
                AdminEmail = tx.AdminEmail,
                Note = tx.Note,
                CreatedAtUtc = tx.CreatedAtUtc,
            })
            .ToListAsync();
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
