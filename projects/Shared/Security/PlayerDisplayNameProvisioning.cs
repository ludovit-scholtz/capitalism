using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Capitalism.Shared.Security;

public static partial class PlayerDisplayNameProvisioning
{
    private static readonly string[] Adjectives =
    [
        "Bold", "Brisk", "Bright", "Calm", "Clever", "Daring", "Eager", "Fierce",
        "Grand", "Keen", "Lucky", "Mighty", "Nimble", "Noble", "Rapid", "Sharp"
    ];

    private static readonly string[] Nouns =
    [
        "Trader", "Tycoon", "Pioneer", "Builder", "Founder", "Captain", "Strategist", "Merchant",
        "Visionary", "Investor", "Operator", "Maker", "Leader", "Navigator", "Planner", "Rival"
    ];

    public static string ResolveDisplayName(
        string? claimedDisplayName,
        string? normalizedEmail,
        string? subjectClaim)
    {
        var candidate = claimedDisplayName?.Trim();
        if (!string.IsNullOrWhiteSpace(candidate) && !LooksLikeSensitiveIdentifier(candidate, normalizedEmail))
        {
            return candidate;
        }

        var seed = normalizedEmail?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(seed))
        {
            seed = subjectClaim?.Trim();
        }

        if (string.IsNullOrWhiteSpace(seed))
        {
            seed = Guid.NewGuid().ToString("N");
        }

        return GenerateDeterministicAlias(seed);
    }

    public static bool ShouldReplaceExistingDisplayName(string? existingDisplayName, string? normalizedEmail)
    {
        if (string.IsNullOrWhiteSpace(existingDisplayName))
        {
            return true;
        }

        return LooksLikeSensitiveIdentifier(existingDisplayName, normalizedEmail);
    }

    public static bool LooksLikeSensitiveIdentifier(string? value, string? normalizedEmail)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var candidate = value.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedEmail)
            && string.Equals(candidate, normalizedEmail.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (candidate.Contains('@', StringComparison.Ordinal)
            || candidate.StartsWith("did:", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Guid.TryParse(candidate, out _))
        {
            return true;
        }

        if (candidate.Length == 58 && AlgorandAddressRegex().IsMatch(candidate))
        {
            return true;
        }

        return false;
    }

    private static string GenerateDeterministicAlias(string seed)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var adjective = Adjectives[digest[0] % Adjectives.Length];
        var noun = Nouns[digest[1] % Nouns.Length];
        var suffix = 100 + (((digest[2] << 8) | digest[3]) % 900);
        return $"{adjective} {noun} {suffix}";
    }

    [GeneratedRegex("^[A-Z2-7]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AlgorandAddressRegex();
}
