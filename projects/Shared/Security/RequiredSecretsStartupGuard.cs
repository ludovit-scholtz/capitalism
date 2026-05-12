namespace Capitalism.Shared.Security;

public static class RequiredSecretsStartupGuard
{
    private static readonly string[] PlaceholderMarkers =
    [
        "__SET",
        "CHANGE_ME",
        "CHANGEME",
        "PLACEHOLDER",
        "<REQUIRED",
    ];

    public static bool TryGetUnsafeConnectionStringReason(string? connectionString, out string reason)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            reason = "is missing or empty";
            return true;
        }

        if (ContainsPlaceholderMarker(connectionString))
        {
            reason = "contains a placeholder marker";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    public static bool TryGetUnsafeRootAdministratorEmailsReason(IEnumerable<string>? rootAdministratorEmails, out string reason)
    {
        var emails = rootAdministratorEmails?
            .Where(static email => !string.IsNullOrWhiteSpace(email))
            .Select(static email => email.Trim())
            .ToList() ?? [];

        if (emails.Count == 0)
        {
            reason = "is empty";
            return true;
        }

        if (emails.Any(ContainsPlaceholderMarker))
        {
            reason = "contains placeholder marker values";
            return true;
        }

        reason = string.Empty;
        return false;
    }

    private static bool ContainsPlaceholderMarker(string value)
    {
        return PlaceholderMarkers.Any(marker => value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
