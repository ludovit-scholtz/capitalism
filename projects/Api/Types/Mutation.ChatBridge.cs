using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Posts a chat message into the shared in-game chat on behalf of a player who sent it from the
    /// bridged Discord channel. Authenticated by the shared master registration key plus this
    /// shard's server key (the same trust boundary as <c>purgePlayerAccountFromMaster</c>). The
    /// player is resolved by email; the message is hidden from the Discord forwarder so it is not
    /// echoed back to Discord.
    /// </summary>
    public async Task<PostBridgedChatMessagePayload> PostBridgedChatMessage(
        PostBridgedChatMessageInput input,
        [Service] AppDbContext db,
        [Service] IOptions<MasterServerRegistrationOptions> options,
        [Service] BridgedChatMessageTracker bridgedTracker,
        [Service] ITopicEventSender topicEventSender,
        [Service] IChatRateLimitService rateLimitService)
    {
        var expectedRegistrationKey = options.Value.RegistrationKey.Trim();
        if (string.IsNullOrWhiteSpace(expectedRegistrationKey)
            || !KeysMatch(expectedRegistrationKey, input.RegistrationKey))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid game server registration key.")
                    .SetCode("INVALID_REGISTRATION_KEY")
                    .Build());
        }

        var expectedServerKey = options.Value.ServerKey.Trim();
        if (string.IsNullOrWhiteSpace(expectedServerKey)
            || !KeysMatch(expectedServerKey, input.ServerKey))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid server key.")
                    .SetCode("INVALID_SERVER_KEY")
                    .Build());
        }

        var playerEmail = input.PlayerEmail?.Trim();
        if (string.IsNullOrWhiteSpace(playerEmail))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player email is required.")
                    .SetCode("PLAYER_EMAIL_REQUIRED")
                    .Build());
        }

        var content = input.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            return new PostBridgedChatMessagePayload { Delivered = false };
        }

        if (content.Length > MaxChatMessageLength)
        {
            content = content[..MaxChatMessageLength];
        }

        var normalizedEmail = playerEmail.ToLowerInvariant();
        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Email == normalizedEmail);
        if (player is null)
        {
            // The player has no presence on this shard; nothing to post here.
            return new PostBridgedChatMessagePayload { Delivered = false };
        }

        // Same per-player limit as sendChatMessage, so a leaked bridge key cannot flood the chat (#658).
        var (isAllowed, retryAfterSeconds) = rateLimitService.TryRecord(player.Id);
        if (!isAllowed)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This player is sending messages too fast. Please wait a moment.")
                    .SetCode("RATE_LIMITED")
                    .SetExtension("retryAfter", retryAfterSeconds)
                    .Build());
        }

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            AuthorPlayerId = player.Id,
            CityId = null,
            AuthorDisplayName = PublicPlayerDisplayName.Resolve(player),
            Content = content,
            CreatedAtUtc = DateTime.UtcNow,
            IsVisible = true,
        };

        db.ChatMessages.Add(chatMessage);
        await db.SaveChangesAsync();

        // Mark before publishing so the Discord forwarder skips it and avoids a Discord echo loop.
        bridgedTracker.MarkBridged(chatMessage.Id);

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
            IsOwnMessage = false,
        };

        if (!player.IsInvisibleInChat)
        {
            await topicEventSender.SendAsync(nameof(Subscription.ChatMessageSent), payload);
        }

        return new PostBridgedChatMessagePayload { Delivered = true };
    }

    /// <summary>Constant-time comparison of a shared secret so response timing does not reveal how much of a guessed key matched (#658).</summary>
    private static bool KeysMatch(string expected, string? provided)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided?.Trim() ?? string.Empty));
}

public sealed class PostBridgedChatMessageInput
{
    [Required]
    public string RegistrationKey { get; set; } = string.Empty;

    [Required]
    public string ServerKey { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string PlayerEmail { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}

public sealed class PostBridgedChatMessagePayload
{
    public bool Delivered { get; set; }
}
