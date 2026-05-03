using System.ComponentModel.DataAnnotations;

namespace Api.Security;

public sealed class BiatecOidcOptions
{
    public const string SectionName = "BiatecOidc";

    public bool Enabled { get; init; }

    [Required]
    public string Authority { get; init; } = "https://localhost:44305";

    public string? Issuer { get; init; }

    [Required]
    public string Audience { get; init; } = "capitalism";

    public bool RequireHttpsMetadata { get; init; } = true;
}
