using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Capitalism.NPCBot.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Lightweight GraphQL HTTP client for the Capitalism game API.
/// Manages per-request bearer token injection and deserialises responses.
/// </summary>
public sealed class GameApiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly ILogger<GameApiClient> _logger;
    private readonly string _graphqlUrl;

    public GameApiClient(
        HttpClient http,
        IOptions<BotOptions> options,
        ILogger<GameApiClient> logger)
    {
        _http = http;
        _logger = logger;
        _graphqlUrl = options.Value.GraphqlUrl;
    }

    /// <summary>
    /// Executes a GraphQL query or mutation.
    /// </summary>
    /// <typeparam name="T">Expected type of the <c>data</c> wrapper object.</typeparam>
    /// <param name="query">GraphQL document string.</param>
    /// <param name="variables">Optional variables dictionary.</param>
    /// <param name="bearerToken">Optional JWT token for authenticated calls.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Deserialised <typeparamref name="T"/> from the response data object.</returns>
    public async Task<T> ExecuteAsync<T>(
        string query,
        object? variables = null,
        string? bearerToken = null,
        CancellationToken ct = default)
    {
        var payload = new { query, variables };
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        using var request = new HttpRequestMessage(HttpMethod.Post, _graphqlUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

        if (bearerToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("HTTP {Status} from GraphQL: {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException($"GraphQL HTTP error {(int)response.StatusCode}: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("errors", out var errors))
        {
            var (firstMsg, code) = GraphQLResponseParser.ParseFirstError(errors);
            _logger.LogWarning("GraphQL error [{Code}]: {Message}", code, firstMsg);
            throw new GraphQLException(firstMsg, code);
        }

        if (!root.TryGetProperty("data", out var data))
            throw new InvalidOperationException($"No 'data' field in GraphQL response: {body}");

        return JsonSerializer.Deserialize<T>(data.GetRawText(), JsonOpts)
               ?? throw new InvalidOperationException("Deserialisation returned null.");
    }
}

/// <summary>Represents a GraphQL domain error returned by the game API.</summary>
public sealed class GraphQLException(string message, string code) : Exception(message)
{
    /// <summary>Machine-readable error code from <c>extensions.code</c>.</summary>
    public string Code { get; } = code;
}
