using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using HotChocolate.Subscriptions;
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
        [Service] IChatRateLimitService rateLimitService,
        [Service] ITopicEventSender topicEventSender)
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

        var content = input.Content.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Chat message cannot be empty.")
                    .SetCode("CHAT_MESSAGE_EMPTY")
                    .Build());
        }

        if (content.Length > MaxChatMessageLength)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Chat message is too long. Maximum length is {MaxChatMessageLength} characters.")
                    .SetCode("MESSAGE_TOO_LONG")
                    .SetExtension("maxLength", MaxChatMessageLength)
                    .Build());
        }

        if (input.CityId is not null
            && !await db.Cities.AsNoTracking().AnyAsync(city => city.Id == input.CityId.Value))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("City not found.")
                    .SetCode("CITY_NOT_FOUND")
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
            AuthorPlayerId = player.Id,
            CityId = input.CityId,
            AuthorDisplayName = PublicPlayerDisplayName.Resolve(player),
            Content = content,
            CreatedAtUtc = DateTime.UtcNow,
            IsVisible = true
        };

        db.ChatMessages.Add(chatMessage);
        await db.SaveChangesAsync();

        var payload = new InGameChatMessage
        {
            Id = chatMessage.Id,
            AuthorPlayerId = player.Id,
            AuthorDisplayName = chatMessage.AuthorDisplayName,
            CityId = chatMessage.CityId,
            Content = chatMessage.Content,
            CreatedAtUtc = chatMessage.CreatedAtUtc,
            IsVisible = chatMessage.IsVisible,
            IsRemovedForViewer = false,
            IsOwnMessage = true
        };

        // Invisible players should still see their own mutation response but are hidden from others.
        if (!player.IsInvisibleInChat)
        {
            await topicEventSender.SendAsync(nameof(Subscription.ChatMessageSent), payload);
        }

        return payload;
    }

    [Authorize(Policy = Policies.Admin)]
    public async Task<InGameChatMessage> SetChatMessageVisible(
        SetChatMessageVisibleInput input,
        [Service] AppDbContext db,
        [Service] ITopicEventSender topicEventSender)
    {
        var message = await db.ChatMessages
            .AsTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == input.MessageId);

        if (message is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Chat message not found.")
                    .SetCode("CHAT_MESSAGE_NOT_FOUND")
                    .Build());
        }

        message.IsVisible = input.Visible;
        await db.SaveChangesAsync();

        var payload = new InGameChatMessage
        {
            Id = message.Id,
            AuthorPlayerId = message.AuthorPlayerId,
            AuthorDisplayName = message.AuthorDisplayName,
            CityId = message.CityId,
            Content = message.Content,
            CreatedAtUtc = message.CreatedAtUtc,
            IsVisible = message.IsVisible,
            IsRemovedForViewer = false,
            IsOwnMessage = false
        };

        await topicEventSender.SendAsync(nameof(Subscription.ChatMessageSent), payload);
        return payload;
    }
}
