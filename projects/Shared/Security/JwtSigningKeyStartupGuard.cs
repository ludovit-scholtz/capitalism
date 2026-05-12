namespace Capitalism.Shared.Security;

public static class JwtSigningKeyStartupGuard
{
    public const int MinimumSafeSigningKeyLength = 32;

    private static readonly string[] GenericPlaceholderValues =
    [
        "your-secret-key",
        "changeme",
    ];

    public static bool TryGetUnsafeReason(
        string? signingKey,
        IEnumerable<string?> committedPlaceholderValues,
        out string reason)
    {
        var normalizedSigningKey = signingKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedSigningKey))
        {
            reason = "Jwt:SigningKey is null, empty, or whitespace.";
            return true;
        }

        var normalizedPlaceholders = committedPlaceholderValues
            .Concat(GenericPlaceholderValues)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (normalizedPlaceholders.Contains(normalizedSigningKey))
        {
            reason = "Jwt:SigningKey matches a known placeholder value.";
            return true;
        }

        if (normalizedSigningKey.Length < MinimumSafeSigningKeyLength)
        {
            reason = $"Jwt:SigningKey is shorter than {MinimumSafeSigningKeyLength} characters.";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}
