namespace Api.Configuration;

public sealed class MasterServerRegistrationOptions
{
    public const string SectionName = "MasterServer";

    public bool RegistrationEnabled { get; set; } = true;

    public string ApiUrl { get; set; } = string.Empty;

    public string RegistrationKey { get; set; } = string.Empty;

    public string ServerKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public string BackendUrl { get; set; } = string.Empty;

    public string FrontendUrl { get; set; } = string.Empty;

    public string GraphqlUrl { get; set; } = string.Empty;

    public int HeartbeatIntervalSeconds { get; set; } = 30;

    public bool IsConfigured()
    {
        return RegistrationEnabled
            && !string.IsNullOrWhiteSpace(ApiUrl)
            && !string.IsNullOrWhiteSpace(RegistrationKey)
            && !string.IsNullOrWhiteSpace(ServerKey)
            && !string.IsNullOrWhiteSpace(DisplayName)
            && !string.IsNullOrWhiteSpace(BackendUrl)
            && !string.IsNullOrWhiteSpace(FrontendUrl);
    }
}