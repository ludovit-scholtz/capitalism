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

    [Cost(20)]
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

    [Cost(25)]
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
    [Cost(40)]
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
        // Resolve viewer privilege on every request so that author email addresses
        // (CreatedByEmail/UpdatedByEmail) are never disclosed to non-administrators,
        // and so draft visibility stays restricted to trusted servers or admins.
        var trustedServer = await TryResolveTrustedNewsServerIdentityAsync(db, masterServerOptions.Value, input);
        var trustedAdmin = await TryResolvePrivilegedNewsAdminIdentityAsync(db, gameAdministrationOptions.Value, claimsPrincipal);
        var isPrivilegedViewer = trustedServer is not null || trustedAdmin is not null;
        var includeDrafts = input.IncludeDrafts && isPrivilegedViewer;

        var playerEmail = string.IsNullOrWhiteSpace(input.PlayerEmail)
            ? null
            : NormalizeEmail(input.PlayerEmail, "INVALID_PLAYER_EMAIL");
        var limit = Math.Clamp(input.Limit, 1, 500);

        var visibleEntries = db.GameNewsEntries
            .AsNoTracking()
            .Where(entry => entry.TargetServerKey == null || entry.TargetServerKey == input.ServerKey)
            .Where(entry => includeDrafts || entry.Status == GameNewsEntryStatus.Published);

        var entries = new List<GameNewsEntry>();
        foreach (var entryType in GameNewsEntryType.All)
        {
            var categoryEntries = await visibleEntries
                .Where(entry => entry.EntryType == entryType)
                .Include(entry => entry.Localizations)
                .Include(entry => entry.ReadReceipts)
                .OrderByDescending(entry => entry.PublishedAtUtc ?? entry.UpdatedAtUtc)
                .ThenByDescending(entry => entry.CreatedAtUtc)
                .Take(limit)
                .AsSplitQuery()
                .ToListAsync();

            entries.AddRange(categoryEntries);
        }

        entries = entries
            .OrderByDescending(entry => entry.PublishedAtUtc ?? entry.UpdatedAtUtc)
            .ThenByDescending(entry => entry.CreatedAtUtc)
            .ToList();

        var items = entries
            .Select(entry => ToGameNewsEntryInfo(entry, playerEmail, input.ServerKey, isPrivilegedViewer))
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
}
