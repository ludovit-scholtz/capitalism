using System.Net.Http.Json;
using System.Text.Json;
using Api.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Utilities;

/// <summary>
/// Sends ranking telemetry events to the MasterApi so player activity in this game
/// shard earns bounty points in the master ranking system.
/// All calls are fire-and-forget resilient: failures are logged but never propagate
/// to callers so gameplay is never blocked by master-server connectivity issues.
/// </summary>
public interface IMasterRankingTelemetryService
{
    /// <summary>
    /// Reports a player activity event to the MasterApi ranking system.
    /// <paramref name="uniqueScopeKey"/> is used to deduplicate within a UTC day per player.
    /// </summary>
    Task ReportEventAsync(
        string eventType,
        string playerEmail,
        string? uniqueScopeKey = null,
        string? externalEventId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op implementation used when the master server is not configured.
/// </summary>
public sealed class NoOpMasterRankingTelemetryService : IMasterRankingTelemetryService
{
    public Task ReportEventAsync(
        string eventType,
        string playerEmail,
        string? uniqueScopeKey = null,
        string? externalEventId = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Live implementation that calls the MasterApi <c>ingestRankingEvent</c> GraphQL mutation.
/// </summary>
public sealed class MasterRankingTelemetryService(
    IHttpClientFactory httpClientFactory,
    IOptions<MasterServerRegistrationOptions> options,
    ILogger<MasterRankingTelemetryService> logger) : IMasterRankingTelemetryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task ReportEventAsync(
        string eventType,
        string playerEmail,
        string? uniqueScopeKey = null,
        string? externalEventId = null,
        CancellationToken cancellationToken = default)
    {
        if (!options.Value.IsTelemetryConfigured())
        {
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("master-server");
            var body = new
            {
                query = """
                        mutation IngestRankingEvent($input: IngestRankingEventInput!) {
                          ingestRankingEvent(input: $input) {
                            id
                            status
                          }
                        }
                        """,
                variables = new
                {
                    input = new
                    {
                        registrationKey = options.Value.RegistrationKey,
                        serverKey = options.Value.ServerKey,
                        eventType = eventType,
                        playerEmail = playerEmail,
                        uniqueScopeKey = uniqueScopeKey,
                        externalEventId = externalEventId,
                        payloadJson = "{}",
                        occurredAtUtc = (DateTime?)null,
                    }
                }
            };

            using var response = await client.PostAsJsonAsync(options.Value.ApiUrl, body, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GraphQlResponse>(JsonOptions, cancellationToken);
            if (result?.Errors is { Count: > 0 })
            {
                logger.LogWarning(
                    "MasterRankingTelemetry: ingestRankingEvent returned error for event {EventType}, player {Email}: {Error}",
                    eventType,
                    playerEmail,
                    result.Errors[0].Message);
            }
        }
        catch (Exception ex)
        {
            // Telemetry failures must never affect gameplay.
            logger.LogWarning(ex,
                "MasterRankingTelemetry: failed to report event {EventType} for player {Email}.",
                eventType,
                playerEmail);
        }
    }

    private sealed class GraphQlResponse
    {
        public List<GraphQlError>? Errors { get; init; }
    }

    private sealed class GraphQlError
    {
        public string Message { get; init; } = string.Empty;
    }
}
