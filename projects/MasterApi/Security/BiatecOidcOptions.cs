namespace MasterApi.Security;

public sealed class BiatecOidcOptions
{
    public const string SectionName = "BiatecOidc";

    public bool Enabled { get; set; } = true;

    public string Authority { get; set; } = "https://localhost:44305";

    public string Issuer { get; set; } = "https://google.biatec.io";

    public string Audience { get; set; } = "capitalism-master";

    public bool RequireHttpsMetadata { get; set; } = false;
}
