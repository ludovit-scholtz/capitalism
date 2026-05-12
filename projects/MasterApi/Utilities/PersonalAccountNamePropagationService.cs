using System.Text;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

public sealed class PersonalAccountNamePropagationService
{
    private const string SyncMutation = """
        mutation SyncPersonalAccountNameFromMaster($input: SyncPersonalAccountNameFromMasterInput!) {
          syncPersonalAccountNameFromMaster(input: $input) {
            playerFound
            wasUpdated
            personalAccountName
          }
        }
        """;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly MasterDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly MasterServerOptions _options;
    private readonly ILogger<PersonalAccountNamePropagationService> _logger;

    public PersonalAccountNamePropagationService(
        MasterDbContext db,
        HttpClient httpClient,
        IOptions<MasterServerOptions> options,
        ILogger<PersonalAccountNamePropagationService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PropagateAsync(string playerEmail, string personalAccountName, CancellationToken cancellationToken)
    {
        var registrationKey = _options.RegistrationKey.Trim();
        if (string.IsNullOrWhiteSpace(registrationKey))
        {
            _logger.LogWarning(
                "Skipping personal account name propagation for {PlayerEmail} because MasterServer:RegistrationKey is not configured.",
                playerEmail);
            return;
        }

        var now = DateTime.UtcNow;
        var targets = await _db.GameServers
            .AsNoTracking()
            .Where(server => server.IsActive)
            .Where(server => server.ExpiresAtUtc > now)
            .Where(server => !string.IsNullOrWhiteSpace(server.ServerKey))
            .Where(server => !string.IsNullOrWhiteSpace(server.GraphqlUrl))
            .ToListAsync(cancellationToken);

        if (targets.Count == 0)
        {
            return;
        }

        await Task.WhenAll(targets.Select(server => PropagateToServerAsync(
            server,
            registrationKey,
            playerEmail,
            personalAccountName,
            cancellationToken)));
    }

    private async Task PropagateToServerAsync(
        GameServerNode server,
        string registrationKey,
        string playerEmail,
        string personalAccountName,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                query = SyncMutation,
                variables = new
                {
                    input = new
                    {
                        registrationKey,
                        serverKey = server.ServerKey,
                        playerEmail,
                        personalAccountName,
                    },
                },
            }, SerializerOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, server.GraphqlUrl)
            {
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
            };

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to propagate personal account name for {PlayerEmail} to shard {ServerKey}. HTTP {StatusCode}.",
                    playerEmail,
                    server.ServerKey,
                    (int)response.StatusCode);
                return;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            if (document.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0)
            {
                var firstError = errors[0].GetProperty("message").GetString() ?? "Unknown GraphQL error.";
                _logger.LogWarning(
                    "Shard {ServerKey} rejected personal account name propagation for {PlayerEmail}: {ErrorMessage}",
                    server.ServerKey,
                    playerEmail,
                    firstError);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to propagate personal account name for {PlayerEmail} to shard {ServerKey}.",
                playerEmail,
                server.ServerKey);
        }
    }
}