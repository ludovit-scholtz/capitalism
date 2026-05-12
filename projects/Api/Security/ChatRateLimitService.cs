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
    /// roughly how many seconds until the window resets.
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

    /// <summary>Duration of the sliding rate-limit window.</summary>
    public static readonly TimeSpan WindowSize = TimeSpan.FromSeconds(60);

    private static string CounterKey(Guid playerId) => $"chat_rate:{playerId:N}";

    /// <inheritdoc/>
    public (bool IsAllowed, int RetryAfterSeconds) TryRecord(Guid playerId)
    {
        var key = CounterKey(playerId);

        // Store a long[] wrapper so Interlocked.Increment can operate atomically on the
        // in-cache reference, avoiding TOCTOU races from concurrent requests.
        var counter = cache.GetOrCreate(key, entry =>
        {
            entry.SetAbsoluteExpiration(WindowSize);
            return new long[] { 0 };
        });

        var count = Interlocked.Increment(ref counter![0]);

        if (count > MaxMessagesPerWindow)
        {
            // Estimate remaining seconds from the window duration (conservative upper bound).
            var retryAfter = (int)Math.Ceiling(WindowSize.TotalSeconds);
            logger.LogWarning(
                "Chat rate limit exceeded for player {PlayerId}: {Count} messages in the last {WindowSeconds}s (limit {Limit}).",
                playerId, count, (int)WindowSize.TotalSeconds, MaxMessagesPerWindow);

            return (false, retryAfter);
        }

        return (true, 0);
    }
}
