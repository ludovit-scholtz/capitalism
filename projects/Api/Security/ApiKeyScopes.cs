namespace Api.Security;

public static class ApiKeyScopes
{
    public const string ReadOnly = "read-only";
    public const string BotOnly = "bot-only";
    public const string TradingOnly = "trading-only";
    public const string CompanyBound = "company-bound";

    public static readonly string[] All = [ReadOnly, BotOnly, TradingOnly, CompanyBound];
    public static readonly string[] PrimaryScopes = [ReadOnly, BotOnly, TradingOnly];

    public static bool IsValid(string? scope)
        => Normalize(scope) is not null;

    public static string? Normalize(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        return scope.Trim().ToLowerInvariant() switch
        {
            ReadOnly => ReadOnly,
            BotOnly => BotOnly,
            TradingOnly => TradingOnly,
            CompanyBound => CompanyBound,
            _ => null,
        };
    }
}
