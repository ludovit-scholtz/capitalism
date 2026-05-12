using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// In-game chat mutations.
/// Provides a server-wide shared chat feed authenticated players can post to.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>Maximum allowed length (in characters) for a single chat message after trimming.</summary>
    private const int MaxChatMessageLength = 500;

    /// <summary>
    /// Sends a shared in-game chat message authored by the authenticated player.
    /// </summary>
    /// <remarks>
    /// Players marked as <see cref="Player.IsInvisibleInChat"/> remain hidden from
    /// regular players' chat feeds, but their messages are still stored and visible
    /// to themselves and administrators.
    /// </remarks>
    /// <param name="input">The message payload; content is trimmed before storage.</param>
    /// <param name="db">The game database context.</param>
    /// <param name="httpContextAccessor">Used to identify the sender.</param>
    /// <param name="rateLimitService">Per-user chat rate limiter.</param>
    /// <returns>The persisted chat message including the sender's display name.</returns>
    /// <exception cref="GraphQLException">
    /// Thrown with code <c>PLAYER_NOT_FOUND</c> if the caller's player record does not exist,
    /// <c>CHAT_MESSAGE_EMPTY</c> if the trimmed message is blank,
    /// <c>MESSAGE_TOO_LONG</c> if the message exceeds <see cref="MaxChatMessageLength"/> characters,
    /// or <c>RATE_LIMITED</c> if the player has exceeded the per-minute send limit.
    /// </exception>
    [Authorize]
    public async Task<InGameChatMessage> SendChatMessage(
        SendChatMessageInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] IChatRateLimitService rateLimitService)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Id == userId);
        if (player is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        }

        var message = input.Message.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Chat message cannot be empty.")
                    .SetCode("CHAT_MESSAGE_EMPTY")
                    .Build());
        }

        if (message.Length > MaxChatMessageLength)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Chat message is too long. Maximum length is {MaxChatMessageLength} characters.")
                    .SetCode("MESSAGE_TOO_LONG")
                    .SetExtension("maxLength", MaxChatMessageLength)
                    .Build());
        }

        // Enforce per-user sliding-window rate limit.  Record the attempt first so concurrent
        // requests from the same account are counted atomically.
        var (isAllowed, retryAfterSeconds) = rateLimitService.TryRecord(userId);
        if (!isAllowed)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You are sending messages too fast. Please wait a moment.")
                    .SetCode("RATE_LIMITED")
                    .SetExtension("retryAfter", retryAfterSeconds)
                    .Build());
        }

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Message = message,
            SentAtUtc = DateTime.UtcNow
        };

        db.ChatMessages.Add(chatMessage);
        await db.SaveChangesAsync();

        return new InGameChatMessage
        {
            Id = chatMessage.Id,
            PlayerId = player.Id,
            PlayerDisplayName = PublicPlayerDisplayName.Resolve(player),
            Message = chatMessage.Message,
            SentAtUtc = chatMessage.SentAtUtc,
            IsOwnMessage = true
        };
    }
}
