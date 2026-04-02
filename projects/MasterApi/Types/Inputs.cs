namespace MasterApi.Types;

public sealed class RegisterGameServerInput
{
    public string RegistrationKey { get; set; } = string.Empty;

    public string ServerKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Region { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public string BackendUrl { get; set; } = string.Empty;

    public string GraphqlUrl { get; set; } = string.Empty;

    public string FrontendUrl { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public int PlayerCount { get; set; }

    public int CompanyCount { get; set; }

    public long CurrentTick { get; set; }
}

public sealed class GameServerSummary
{
    public Guid Id { get; init; }

    public string ServerKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;

    public string BackendUrl { get; init; } = string.Empty;

    public string GraphqlUrl { get; init; } = string.Empty;

    public string FrontendUrl { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public int PlayerCount { get; init; }

    public int CompanyCount { get; init; }

    public long CurrentTick { get; init; }

    public DateTime RegisteredAtUtc { get; init; }

    public DateTime LastHeartbeatAtUtc { get; init; }

    public bool IsOnline { get; init; }
}