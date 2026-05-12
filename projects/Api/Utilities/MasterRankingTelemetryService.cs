using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
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
    private static readonly ConcurrentDictionary<string, Lazy<Task>> InFlightReports = new(StringComparer.Ordinal);

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
            var normalizedEventType = eventType.Trim().ToUpperInvariant();
            var normalizedPlayerEmail = playerEmail.Trim().ToLowerInvariant();
            var idempotencyKey = BuildIdempotencyKey(
                normalizedEventType,
                normalizedPlayerEmail,
                options.Value.ServerKey,
                uniqueScopeKey,
                externalEventId);
            var reportTask = InFlightReports.GetOrAdd(
                idempotencyKey,
                _ => new Lazy<Task>(
                    () => SendReportAsync(
                        normalizedEventType,
                        normalizedPlayerEmail,
                        uniqueScopeKey,
                        externalEventId,
                        idempotencyKey,
                        cancellationToken),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            var sharedTask = reportTask.Value;

            await sharedTask;

            if (sharedTask.IsCompleted)
            {
                InFlightReports.TryRemove(new KeyValuePair<string, Lazy<Task>>(idempotencyKey, reportTask));
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

    private async Task SendReportAsync(
        string normalizedEventType,
        string normalizedPlayerEmail,
        string? uniqueScopeKey,
        string? externalEventId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
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
                        eventType = normalizedEventType,
                        playerEmail = normalizedPlayerEmail,
                        uniqueScopeKey = uniqueScopeKey,
                        externalEventId = externalEventId,
                        idempotencyKey,
                        payloadJson = "{}",
                        occurredAtUtc = DateTime.UtcNow,
                    }
                }
            };

            using var response = await client.PostAsJsonAsync(options.Value.ApiUrl, body, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = TryDeserializeGraphQlResponse(responseText);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "MasterRankingTelemetry: ingestRankingEvent failed with HTTP {StatusCode} for event {EventType}, player {Email}, scope {ScopeKey}. GraphQLErrors={Errors}. Response={Response}",
                    (int)response.StatusCode,
                    normalizedEventType,
                    normalizedPlayerEmail,
                    uniqueScopeKey,
                    BuildGraphQlErrorSummary(result),
                    TrimForLog(responseText));
                return;
            }

            if (result?.Errors is { Count: > 0 })
            {
                logger.LogWarning(
                    "MasterRankingTelemetry: ingestRankingEvent returned GraphQL errors for event {EventType}, player {Email}, scope {ScopeKey}. Errors={Errors}",
                    normalizedEventType,
                    normalizedPlayerEmail,
                    uniqueScopeKey,
                    BuildGraphQlErrorSummary(result));
            }
        }
        catch (Exception ex)
        {
            // Telemetry failures must never affect gameplay.
            logger.LogWarning(ex,
                "MasterRankingTelemetry: failed to report event {EventType} for player {Email}.",
                normalizedEventType,
                normalizedPlayerEmail);
        }
    }

    private static GraphQlResponse? TryDeserializeGraphQlResponse(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GraphQlResponse>(responseText, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildGraphQlErrorSummary(GraphQlResponse? response)
    {
        if (response?.Errors is not { Count: > 0 })
        {
            return "none";
        }

        return string.Join(" | ", response.Errors.Select(error =>
        {
            var code = string.IsNullOrWhiteSpace(error.Code) ? "no-code" : error.Code;
            return $"{code}:{error.Message}";
        }));
    }

    private static string TrimForLog(string? value, int maxLength = 1200)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength] + "...";
    }

    private static string BuildIdempotencyKey(
        string eventType,
        string playerEmail,
        string? serverKey,
        string? uniqueScopeKey,
        string? externalEventId)
    {
        var raw = string.Join(
            "|",
            eventType.Trim().ToUpperInvariant(),
            playerEmail.Trim().ToLowerInvariant(),
            (serverKey ?? string.Empty).Trim(),
            (uniqueScopeKey ?? string.Empty).Trim(),
            (externalEventId ?? string.Empty).Trim());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }

    private sealed class GraphQlResponse
    {
        public List<GraphQlError>? Errors { get; init; }
    }

    private sealed class GraphQlError
    {
        public string Message { get; init; } = string.Empty;

        public Dictionary<string, JsonElement>? Extensions { get; init; }

        public string? Code
        {
            get
            {
                if (Extensions is null)
                {
                    return null;
                }

                if (!Extensions.TryGetValue("code", out var codeElement))
                {
                    return null;
                }

                return codeElement.ValueKind == JsonValueKind.String
                    ? codeElement.GetString()
                    : codeElement.ToString();
            }
        }
    }
}
