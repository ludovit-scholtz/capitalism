using Microsoft.Extensions.Caching.Memory;

namespace Api.Security;

/// <summary>
/// Tracks per-player chat message send rates and enforces a sliding-window rate limit
/// to prevent chat spam and database bloat.
/// </summary>
public interface IChatRateLimitService
{
    /// <summary>
    /// Records a chat send attempt for the given player and returns whether it is allowed.
    /// </summary>
    /// <param name="playerId">The authenticated player's ID.</param>
    /// <returns>
    /// <c>IsAllowed = true</c> when the player is within the rate limit;
    /// <c>IsAllowed = false</c> with <c>RetryAfterSeconds</c> indicating
    /// how many seconds until the current window expires.
    /// </returns>
    (bool IsAllowed, int RetryAfterSeconds) TryRecord(Guid playerId);
}

/// <inheritdoc cref="IChatRateLimitService"/>
public sealed class ChatRateLimitService(
    IMemoryCache cache,
    ILogger<ChatRateLimitService> logger) : IChatRateLimitService
{
    /// <summary>Maximum number of chat messages a single player may send per window.</summary>
    public const int MaxMessagesPerWindow = 20;

    /// <summary>Duration of the rate-limit window.</summary>
    public static readonly TimeSpan WindowSize = TimeSpan.FromSeconds(60);

    private static string CounterKey(Guid playerId) => $"chat_rate:{playerId:N}";

    /// <summary>
    /// Per-player state: <c>[0]</c> = message count, <c>[1]</c> = window expiry as UTC ticks.
    /// Stored as a <c>long[]</c> so <see cref="Interlocked.Increment"/> can operate atomically
    /// on the count without external locking.
    /// </summary>
    private sealed record WindowState(long[] Data);

    /// <inheritdoc/>
    public (bool IsAllowed, int RetryAfterSeconds) TryRecord(Guid playerId)
    {
        var key = CounterKey(playerId);

        var state = cache.GetOrCreate(key, entry =>
        {
            var expiresAt = DateTimeOffset.UtcNow.Add(WindowSize);
            entry.SetAbsoluteExpiration(WindowSize);
            // Data[0] = count; Data[1] = expiry ticks (UTC).
            return new WindowState(new long[] { 0, expiresAt.UtcTicks });
        });

        var count = Interlocked.Increment(ref state!.Data[0]);

        if (count > MaxMessagesPerWindow)
        {
            // Calculate the actual seconds remaining until the window expires.
            var expiresAtUtc = new DateTimeOffset(state.Data[1], TimeSpan.Zero);
            var remaining = expiresAtUtc - DateTimeOffset.UtcNow;
            var retryAfter = (int)Math.Ceiling(Math.Max(0, remaining.TotalSeconds));

            logger.LogWarning(
                "Chat rate limit exceeded for player {PlayerId}: {Count} messages in the last {WindowSeconds}s (limit {Limit}).",
                playerId, count, (int)WindowSize.TotalSeconds, MaxMessagesPerWindow);

            return (false, retryAfter);
        }

        return (true, 0);
    }
}
