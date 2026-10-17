using System.Net.Http.Json;
using System.Text.Json;
using Api.Configuration;
using Api.Types;
using Microsoft.Extensions.Options;

namespace Api.Utilities;

public interface IMasterGameAdministrationService
{
    Task<MasterGameAdministrationAccessSnapshot> GetGameAdministrationAccessAsync(string email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GlobalGameAdminGrantSummary>> GetGlobalGameAdminGrantsAsync(string requesterEmail, CancellationToken cancellationToken = default);

    Task<GlobalGameAdminGrantSummary> AssignGlobalGameAdminAsync(string requesterEmail, string targetEmail, CancellationToken cancellationToken = default);

    Task RemoveGlobalGameAdminAsync(string requesterEmail, string targetEmail, CancellationToken cancellationToken = default);

    Task<GameNewsFeedResult> GetGameNewsFeedAsync(string? playerEmail, bool includeDrafts, string? requesterEmail, CancellationToken cancellationToken = default);

    Task MarkGameNewsReadAsync(string playerEmail, IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken = default);
    
    Task<int> MarkAllGameNewsReadAsync(string playerEmail, CancellationToken cancellationToken = default);

    Task<GameNewsEntryResult> UpsertGameNewsEntryAsync(
        string requesterEmail,
        Guid? entryId,
        string entryType,
        string status,
        IReadOnlyList<GameNewsLocalizationInput> localizations,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TutorialBountyStatusResult>> GetTutorialBountyStatusesAsync(
        string playerEmail,
        CancellationToken cancellationToken = default);
}

public sealed record MasterGameAdministrationAccessSnapshot(
    bool HasGlobalAdminRole,
    bool IsRootAdministrator,
    bool CanAccessEveryGameDashboard);

public sealed partial class MasterGameAdministrationService(
    IHttpClientFactory httpClientFactory,
    IOptions<MasterServerRegistrationOptions> options) : IMasterGameAdministrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<MasterGameAdministrationAccessSnapshot> GetGameAdministrationAccessAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsConfigured())
        {
            return new MasterGameAdministrationAccessSnapshot(false, false, false);
        }

        var payload = await SendGraphQlAsync<GetGameAdministrationAccessResponse>(
            """
            query Access($input: GetGameAdministrationAccessInput!) {
              gameAdministrationAccess(input: $input) {
                hasGlobalAdminRole
                isRootAdministrator
                canAccessEveryGameDashboard
              }
            }
            """,
            new
            {
                input = BuildServiceInput(new
                {
                    email,
                })
            },
            cancellationToken);

        return new MasterGameAdministrationAccessSnapshot(
            payload.GameAdministrationAccess.HasGlobalAdminRole,
            payload.GameAdministrationAccess.IsRootAdministrator,
            payload.GameAdministrationAccess.CanAccessEveryGameDashboard);
    }

    public async Task<IReadOnlyList<GlobalGameAdminGrantSummary>> GetGlobalGameAdminGrantsAsync(
        string requesterEmail,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsConfigured())
        {
            return [];
        }

        var payload = await SendGraphQlAsync<GetGlobalGameAdminGrantsResponse>(
            """
            query Grants($input: GetGlobalGameAdminGrantsInput!) {
              globalGameAdminGrants(input: $input) {
                id
                email
                grantedByEmail
                grantedAtUtc
                updatedAtUtc
              }
            }
            """,
            new
            {
                input = BuildServiceInput(new
                {
                    requesterEmail,
                })
            },
            cancellationToken);

        return payload.GlobalGameAdminGrants;
    }

    public async Task<GlobalGameAdminGrantSummary> AssignGlobalGameAdminAsync(
        string requesterEmail,
        string targetEmail,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var payload = await SendGraphQlAsync<AssignGlobalGameAdminResponse>(
            """
            mutation Assign($input: GlobalGameAdminGrantInput!) {
              assignGlobalGameAdmin(input: $input) {
                id
                email
                grantedByEmail
                grantedAtUtc
                updatedAtUtc
              }
            }
            """,
            new
            {
                input = BuildServiceInput(new
                {
                    requesterEmail,
                    targetEmail,
                })
            },
            cancellationToken);

        return payload.AssignGlobalGameAdmin;
    }

    public async Task RemoveGlobalGameAdminAsync(
        string requesterEmail,
        string targetEmail,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        await SendGraphQlAsync<RemoveGlobalGameAdminResponse>(
            """
            mutation Remove($input: GlobalGameAdminGrantInput!) {
              removeGlobalGameAdmin(input: $input)
            }
            """,
            new
            {
                input = BuildServiceInput(new
                {
                    requesterEmail,
                    targetEmail,
                })
            },
            cancellationToken);
    }

    public async Task<GameNewsFeedResult> GetGameNewsFeedAsync(
        string? playerEmail,
        bool includeDrafts,
        string? requesterEmail,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsConfigured())
        {
            return new GameNewsFeedResult();
        }

        var payload = await SendGraphQlAsync<GetGameNewsFeedResponse>(
            """
            query Feed($input: GetGameNewsFeedInput!) {
              gameNewsFeed(input: $input) {
                unreadCount
                items {
                  id
                  entryType
                  status
                  targetServerKey
                  createdByEmail
                  updatedByEmail
                  createdAtUtc
                  updatedAtUtc
                  publishedAtUtc
                  isRead
                  localizations {
                    locale
                    title
                    summary
                    htmlContent
                  }
                }
              }
            }
            """,
            new
            {
                input = BuildServiceInput(new
                {
                    playerEmail,
                    includeDrafts,
                    limit = 100,
                })
            },
            cancellationToken);

        return payload.GameNewsFeed;
    }

    public async Task MarkGameNewsReadAsync(
        string playerEmail,
        IReadOnlyCollection<Guid> entryIds,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsConfigured() || entryIds.Count == 0)
        {
            return;
        }

        await SendGraphQlAsync<MarkGameNewsReadResponse>(
            """
            mutation MarkRead($input: MarkGameNewsReadInput!) {
              markGameNewsRead(input: $input)
            }
            """,
            new
            {
                input = BuildServiceInput(new
                {
                    playerEmail,
                    entryIds,
                })
            },
            cancellationToken);
    }
    
    public async Task<int> MarkAllGameNewsReadAsync(
        string playerEmail,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsConfigured())
        {
            return 0;
        }

        var payload = await SendGraphQlAsync<MarkAllGameNewsReadResponse>(
            """
            mutation MarkAllRead($input: MarkAllGameNewsReadInput!) {
              markAllGameNewsRead(input: $input)
            }
            """,
            new
            {
                input = BuildServiceInput(new
                {
                    playerEmail,
                })
            },
            cancellationToken);

        return payload.MarkAllGameNewsRead;
    }

    public async Task<GameNewsEntryResult> UpsertGameNewsEntryAsync(
        string requesterEmail,
        Guid? entryId,
        string entryType,
        string status,
        IReadOnlyList<GameNewsLocalizationInput> localizations,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var payload = await SendGraphQlAsync<UpsertGameNewsEntryResponse>(
            """
            mutation Upsert($input: UpsertGameNewsEntryInput!) {
              upsertGameNewsEntry(input: $input) {
                id
                entryType
                status
                targetServerKey
                createdByEmail
                updatedByEmail
                createdAtUtc
                updatedAtUtc
                publishedAtUtc
                isRead
                localizations {
                  locale
                  title
                  summary
                  htmlContent
                }
              }
            }
            """,
            new
            {
                input = BuildServiceInput(new
                {
                    entryId,
                    entryType,
                    status,
                    localizations,
                })
            },
            cancellationToken);

        return payload.UpsertGameNewsEntry;
    }

    public async Task<IReadOnlyList<TutorialBountyStatusResult>> GetTutorialBountyStatusesAsync(
        string playerEmail,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsConfigured())
        {
            return [];
        }

        var payload = await SendGraphQlAsync<GetTutorialBountyStatusesResponse>(
            """
            query TutorialBounties($input: GetTutorialBountyStatusesInput!) {
              tutorialBountyStatuses(input: $input) {
                milestone
                bountyCode
                isAwarded
                awardedAtUtc
                rewardPoints
              }
            }
            """,
            new
            {
                input = BuildServiceInput(new
                {
                    playerEmail,
                })
            },
            cancellationToken);

        return payload.TutorialBountyStatuses;
    }

    private object BuildServiceInput(object payload)
    {
        var source = payload
            .GetType()
            .GetProperties()
            .ToDictionary(property => property.Name, property => property.GetValue(payload));

        source["registrationKey"] = options.Value.RegistrationKey;
        source["serverKey"] = options.Value.ServerKey;
        return source;
    }

    private void EnsureConfigured()
    {
        if (!options.Value.IsConfigured())
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The master game-administration service is not configured for this shard.")
                    .SetCode("MASTER_SERVER_UNAVAILABLE")
                    .Build());
        }
    }

    private async Task<TData> SendGraphQlAsync<TData>(
        string query,
        object variables,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("master-server");
        using var response = await client.PostAsJsonAsync(
            options.Value.ApiUrl,
            new GraphQlRequest
            {
                Query = query,
                Variables = variables,
            },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var graphQlResponse = await response.Content.ReadFromJsonAsync<GraphQlResponse<TData>>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Master API returned an empty response.");

        if (graphQlResponse.Errors is { Count: > 0 })
        {
            var firstError = graphQlResponse.Errors[0];
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(firstError.Message)
                    .SetCode(firstError.Extensions?.Code ?? "MASTER_API_ERROR")
                    .Build());
        }

        return graphQlResponse.Data
            ?? throw new InvalidOperationException("Master API returned no data.");
    }
}


public sealed class TutorialBountyStatusResult
{
    public string Milestone { get; init; } = string.Empty;

    public string BountyCode { get; init; } = string.Empty;

    public bool IsAwarded { get; init; }

    public DateTime? AwardedAtUtc { get; init; }

    public decimal RewardPoints { get; init; }
}
