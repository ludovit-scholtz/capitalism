using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// In-game chat queries.
/// Returns the shared server-wide chat feed, respecting invisible-player visibility rules.
/// </summary>
public sealed partial class Query
{
    /// <summary>
    /// Returns the latest in-game chat messages visible to the authenticated player in a channel.
    /// </summary>
    /// <param name="db">The game database context.</param>
    /// <param name="httpContextAccessor">Used to identify the currently authenticated player.</param>
    /// <param name="cityId">Optional city scope. Null means the global channel.</param>
    /// <param name="lastN">Maximum number of messages to return.</param>
    [Authorize]
    public async Task<List<InGameChatMessage>> GetChatMessages(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        Guid? cityId,
        int? lastN)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var viewer = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(player => player.Id == userId);

        if (viewer is null)
        {
            return [];
        }

        var safeLimit = Math.Clamp(lastN ?? DefaultChatMessageLimit, 1, MaxChatMessageLimit);
        var canSeeInvisiblePlayers = viewer.Role == PlayerRole.Admin;

        var messages = await db.ChatMessages
            .AsNoTracking()
            .Include(message => message.AuthorPlayer)
            .Where(message => message.CityId == cityId)
            .Where(message => message.IsVisible || message.AuthorPlayerId == userId || canSeeInvisiblePlayers)
            .Where(message => !message.AuthorPlayer.IsInvisibleInChat
                              || message.AuthorPlayerId == userId
                              || canSeeInvisiblePlayers)
            .OrderByDescending(message => message.CreatedAtUtc)
            .Take(safeLimit)
            .OrderBy(message => message.CreatedAtUtc)
            .ToListAsync();

        return messages
            .Select(message => new InGameChatMessage
            {
                Id = message.Id,
                AuthorPlayerId = message.AuthorPlayerId,
                AuthorDisplayName = message.AuthorDisplayName,
                CityId = message.CityId,
                Content = message.IsVisible
                    ? message.Content
                    : (message.AuthorPlayerId == userId ? string.Empty : message.Content),
                CreatedAtUtc = message.CreatedAtUtc,
                IsVisible = message.IsVisible,
                IsRemovedForViewer = !message.IsVisible && message.AuthorPlayerId == userId,
                IsOwnMessage = message.AuthorPlayerId == userId
            })
            .ToList();
    }
}
