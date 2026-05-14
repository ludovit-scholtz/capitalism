namespace Capitalism.Shared.Security;

public static class PlayerGender
{
    public const string Unspecified = "UNSPECIFIED";
    public const string Female = "FEMALE";
    public const string Male = "MALE";

    public static readonly string[] All = [Unspecified, Female, Male];

    public static bool IsValid(string? value)
        => value is not null && All.Contains(value, StringComparer.Ordinal);
}
