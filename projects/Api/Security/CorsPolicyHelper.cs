namespace Api.Security;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

public static class CorsPolicyHelper
{
    public const string MisconfiguredWarningMessage =
        "CORS_MISCONFIGURED: No allowed origins configured; all cross-origin requests will be rejected";

    private static readonly string[] DevelopmentDefaultOrigins =
    [
        "http://localhost:5173",
        "http://127.0.0.1:5173",
        "http://localhost:5174",
        "http://127.0.0.1:5174",
        "http://localhost:4173",
        "http://127.0.0.1:4173",
        "http://localhost:4174",
        "http://127.0.0.1:4174",
    ];

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

    public static string[] ResolveEffectiveAllowedOrigins(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredOrigins = ResolveAllowedOrigins(configuration);
        if (configuredOrigins.Length > 0)
        {
            return configuredOrigins;
        }

        if (environment.IsDevelopment())
        {
            return DevelopmentDefaultOrigins;
        }

        return [];
    }

    public static bool IsNonDevelopmentMisconfigured(IHostEnvironment environment, IReadOnlyCollection<string> allowedOrigins)
    {
        return !environment.IsDevelopment() && allowedOrigins.Count == 0;
    }
}
