using MasterApi.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MasterApi.Security;

public interface IPasswordResetThrottleService
{
    bool IsRateLimited(string normalizedEmail);

    void RecordRequest(string normalizedEmail);
}

public sealed class PasswordResetThrottleService(
    IMemoryCache cache,
    IOptions<AuthOptions> options) : IPasswordResetThrottleService
{
    private static string RequestCountKey(string email) => $"password_reset_requests:{email}";

    public bool IsRateLimited(string normalizedEmail)
    {
        var key = RequestCountKey(normalizedEmail);
        if (!cache.TryGetValue<long[]>(key, out var counter))
        {
            return false;
        }

        return counter is not null && Interlocked.Read(ref counter[0]) >= options.Value.ForgotPasswordMaxRequests;
    }

    public void RecordRequest(string normalizedEmail)
    {
        var key = RequestCountKey(normalizedEmail);
        var window = TimeSpan.FromMinutes(options.Value.ForgotPasswordWindowMinutes);
        var counter = cache.GetOrCreate(key, entry =>
        {
            entry.SetAbsoluteExpiration(window);
            return new long[] { 0 };
        });

        Interlocked.Increment(ref counter![0]);
    }
}
