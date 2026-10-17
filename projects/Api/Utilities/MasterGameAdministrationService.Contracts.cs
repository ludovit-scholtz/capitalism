using System.Net.Http.Json;
using System.Text.Json;
using Api.Configuration;
using Api.Types;
using Microsoft.Extensions.Options;

namespace Api.Utilities;

public sealed partial class MasterGameAdministrationService
{
    private sealed class GraphQlRequest
    {
        public string Query { get; init; } = string.Empty;

        public object Variables { get; init; } = new();
    }

    private sealed class GraphQlResponse<TData>
    {
        public TData? Data { get; init; }

        public List<GraphQlError>? Errors { get; init; }
    }

    private sealed class GraphQlError
    {
        public string Message { get; init; } = string.Empty;

        public GraphQlErrorExtensions? Extensions { get; init; }
    }

    private sealed class GraphQlErrorExtensions
    {
        public string? Code { get; init; }
    }

    private sealed class GetGameAdministrationAccessResponse
    {
        public MasterGameAdministrationAccessContract GameAdministrationAccess { get; init; } = new();
    }

    private sealed class MasterGameAdministrationAccessContract
    {
        public bool HasGlobalAdminRole { get; init; }

        public bool IsRootAdministrator { get; init; }

        public bool CanAccessEveryGameDashboard { get; init; }
    }

    private sealed class GetGlobalGameAdminGrantsResponse
    {
        public List<GlobalGameAdminGrantSummary> GlobalGameAdminGrants { get; init; } = [];
    }

    private sealed class AssignGlobalGameAdminResponse
    {
        public GlobalGameAdminGrantSummary AssignGlobalGameAdmin { get; init; } = new();
    }

    private sealed class RemoveGlobalGameAdminResponse
    {
        public bool RemoveGlobalGameAdmin { get; init; }
    }

    private sealed class GetGameNewsFeedResponse
    {
        public GameNewsFeedResult GameNewsFeed { get; init; } = new();
    }

    private sealed class MarkGameNewsReadResponse
    {
        public bool MarkGameNewsRead { get; init; }
    }
    
    private sealed class MarkAllGameNewsReadResponse
    {
        public int MarkAllGameNewsRead { get; init; }
    }

    private sealed class UpsertGameNewsEntryResponse
    {
        public GameNewsEntryResult UpsertGameNewsEntry { get; init; } = new();
    }

    private sealed class GetTutorialBountyStatusesResponse
    {
        public List<TutorialBountyStatusResult> TutorialBountyStatuses { get; init; } = [];
    }
}
