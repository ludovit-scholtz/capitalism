using MasterApi.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MasterApi.Security;

/// <summary>
/// Tracks per-account consecutive login failures and temporarily locks out accounts that
/// exceed the configured threshold, preventing brute-force attacks.
/// </summary>
public interface ILoginThrottleService
{
    /// <summary>
    /// Returns <c>true</c> when the account identified by <paramref name="normalizedEmail"/>
    /// is currently locked out due to too many failed attempts.
    /// </summary>
    bool IsThrottled(string normalizedEmail);

    /// <summary>
    /// Records a failed login attempt for <paramref name="normalizedEmail"/>.
    /// Returns <c>true</c> when this failure triggered a new lockout.
    /// </summary>
    bool RecordFailure(string normalizedEmail);

    /// <summary>
    /// Clears the failure counter for <paramref name="normalizedEmail"/> after a successful login.
    /// </summary>
    void RecordSuccess(string normalizedEmail);
}

/// <inheritdoc cref="ILoginThrottleService"/>
public sealed class LoginThrottleService(
    IMemoryCache cache,
    IOptions<AuthOptions> options,
    ILogger<LoginThrottleService> logger) : ILoginThrottleService
{
    private static string FailureCountKey(string email) => $"login_failures:{email}";
    private static string LockoutKey(string email) => $"login_lockout:{email}";

    public bool IsThrottled(string normalizedEmail)
    {
        return cache.TryGetValue(LockoutKey(normalizedEmail), out _);
    }

    public bool RecordFailure(string normalizedEmail)
    {
        var maxAttempts = options.Value.MaxFailedLoginAttempts;
        var lockoutWindow = TimeSpan.FromMinutes(options.Value.LockoutWindowMinutes);
        var countKey = FailureCountKey(normalizedEmail);

        // Store a long[] wrapper so Interlocked.Increment can operate atomically on the
        // in-cache reference, avoiding TOCTOU races from concurrent failed logins.
        var counter = cache.GetOrCreate(countKey, entry =>
        {
            entry.SetSlidingExpiration(lockoutWindow);
            return new long[] { 0 };
        });

        var count = Interlocked.Increment(ref counter![0]);

        if (count >= maxAttempts)
        {
            // Lock out the account for the configured window using an absolute expiry so it auto-clears.
            cache.Set(LockoutKey(normalizedEmail), true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = lockoutWindow
            });

            logger.LogWarning(
                "Account lockout triggered for {Email} after {Count} consecutive failed login attempts. Lockout window: {WindowMinutes} minutes.",
                normalizedEmail, count, options.Value.LockoutWindowMinutes);

            return true;
        }

        return false;
    }

    public void RecordSuccess(string normalizedEmail)
    {
        cache.Remove(FailureCountKey(normalizedEmail));
        cache.Remove(LockoutKey(normalizedEmail));
    }
}
