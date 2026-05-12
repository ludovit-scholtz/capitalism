namespace Api.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public static class CorsPolicyHelper
{
    public const string MisconfiguredWarningMessage =
        "CORS_MISCONFIGURED: No allowed origins configured; all cross-origin requests will be rejected";

    public static string[] ResolveAllowedOrigins(IConfiguration configuration)
    {
        return configuration.GetSection("Cors:AllowedOrigins")
            .Get<string[]>()?
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Select(origin => origin.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
    }

    public static bool IsDevelopmentOpenPolicy(IHostEnvironment environment)
    {
        return environment.IsDevelopment();
    }

    public static bool IsNonDevelopmentMisconfigured(IHostEnvironment environment, IReadOnlyCollection<string> allowedOrigins)
    {
        return !environment.IsDevelopment() && allowedOrigins.Count == 0;
    }

    public static bool IsOriginAllowed(IHostEnvironment environment, IReadOnlyCollection<string> allowedOrigins, string origin)
    {
        return environment.IsDevelopment() || allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }
}
