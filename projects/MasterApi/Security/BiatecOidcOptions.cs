namespace MasterApi.Security;

public sealed class BiatecOidcOptions
{
    public const string SectionName = "BiatecOidc";

    public bool Enabled { get; set; } = true;

    public string Authority { get; set; } = "https://google.biatec.io";

    public string Issuer { get; set; } = "https://google.biatec.io";

    public string Audience { get; set; } = "capitalism";

    public bool RequireHttpsMetadata { get; set; } = true;
}
