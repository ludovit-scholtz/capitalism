using System.Text;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities;

public interface IGameServerAccountPurgeService
{
    Task PurgeAsync(string playerEmail, CancellationToken cancellationToken);
}

/// <summary>
/// Calls every active game server to purge a player's game data when their master
/// account is deleted. Building/bank handling is owned by the game server.
/// </summary>
public sealed class GameServerAccountPurgeService : IGameServerAccountPurgeService
{
    private const string PurgeMutation = """
        mutation PurgePlayerAccountFromMaster($input: PurgePlayerAccountFromMasterInput!) {
          purgePlayerAccountFromMaster(input: $input) {
            playerFound
            companiesRemoved
            buildingsDestroyed
            banksTransferredToGovernment
          }
        }
        """;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly MasterDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly MasterServerOptions _options;
    private readonly ILogger<GameServerAccountPurgeService> _logger;

    public GameServerAccountPurgeService(
        MasterDbContext db,
        HttpClient httpClient,
        IOptions<MasterServerOptions> options,
        ILogger<GameServerAccountPurgeService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PurgeAsync(string playerEmail, CancellationToken cancellationToken)
    {
        var registrationKey = _options.RegistrationKey.Trim();
        if (string.IsNullOrWhiteSpace(registrationKey))
        {
            _logger.LogWarning(
                "Skipping account purge for {PlayerEmail} because MasterServer:RegistrationKey is not configured.",
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

        await Task.WhenAll(targets.Select(server => PurgeOnServerAsync(
            server,
            registrationKey,
            playerEmail,
            cancellationToken)));
    }

    private async Task PurgeOnServerAsync(
        GameServerNode server,
        string registrationKey,
        string playerEmail,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestBody = JsonSerializer.Serialize(new
            {
                query = PurgeMutation,
                variables = new
                {
                    input = new
                    {
                        registrationKey,
                        serverKey = server.ServerKey,
                        playerEmail,
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
                    "Failed to purge account {PlayerEmail} on shard {ServerKey}. HTTP {StatusCode}.",
                    playerEmail,
                    server.ServerKey,
                    (int)response.StatusCode);
                throw new GameServerPurgeException(
                    $"Shard {server.ServerKey} returned HTTP {(int)response.StatusCode} for account purge.");
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);
            if (document.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0)
            {
                var firstError = errors[0].GetProperty("message").GetString() ?? "Unknown GraphQL error.";
                _logger.LogWarning(
                    "Shard {ServerKey} rejected account purge for {PlayerEmail}: {ErrorMessage}",
                    server.ServerKey,
                    playerEmail,
                    firstError);
                throw new GameServerPurgeException(
                    $"Shard {server.ServerKey} rejected account purge: {firstError}");
            }
        }
        catch (GameServerPurgeException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to purge account {PlayerEmail} on shard {ServerKey}.",
                playerEmail,
                server.ServerKey);
            throw new GameServerPurgeException(
                $"Failed to purge account on shard {server.ServerKey}.", ex);
        }
    }
}

/// <summary>Raised when a game server fails to confirm purge of a player's data.</summary>
public sealed class GameServerPurgeException : Exception
{
    public GameServerPurgeException(string message)
        : base(message)
    {
    }

    public GameServerPurgeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
