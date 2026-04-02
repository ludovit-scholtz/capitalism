namespace MasterApi.Configuration;

public sealed class MasterServerOptions
{
    public const string SectionName = "MasterServer";

    public string RegistrationKey { get; set; } = string.Empty;

    public int ActiveThresholdSeconds { get; set; } = 90;
}