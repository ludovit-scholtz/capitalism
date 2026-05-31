using System.Net.Http.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MasterApi.Utilities.Discord;

/// <summary>
/// Relays a message typed in the bridged Discord channel into the in-game chat of every active
/// game server, calling each shard's <c>postBridgedChatMessage</c> mutation on behalf of the
/// linked player (resolved by email). Mirrors the cross-server call pattern used by
/// <see cref="GameServerAccountPurgeService"/>.
/// </summary>
public sealed class DiscordGameChatBridge
{
    private const string PostBridgedChatMutation = """
        mutation PostBridgedChatMessage($input: PostBridgedChatMessageInput!) {
          postBridgedChatMessage(input: $input) {
            delivered
          }
        }
        """;

    private readonly MasterDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly IOptions<MasterServerOptions> _masterServerOptions;
    private readonly ILogger<DiscordGameChatBridge> _logger;

    public DiscordGameChatBridge(
        MasterDbContext db,
        HttpClient httpClient,
        IOptions<MasterServerOptions> masterServerOptions,
        ILogger<DiscordGameChatBridge> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _masterServerOptions = masterServerOptions;
        _logger = logger;
    }

    public async Task BroadcastAsync(string playerEmail, string content, CancellationToken cancellationToken)
    {
        var registrationKey = _masterServerOptions.Value.RegistrationKey?.Trim();
        if (string.IsNullOrWhiteSpace(registrationKey) || string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        var targets = await _db.GameServers
            .AsNoTracking()
            .Where(server => server.IsActive)
            .Where(server => server.ExpiresAtUtc > DateTime.UtcNow)
            .Where(server => server.ServerKey != null && server.ServerKey != "")
            .Where(server => server.GraphqlUrl != null && server.GraphqlUrl != "")
            .ToListAsync(cancellationToken);

        foreach (var server in targets)
        {
            try
            {
                var body = new
                {
                    query = PostBridgedChatMutation,
                    variables = new
                    {
                        input = new
                        {
                            registrationKey,
                            serverKey = server.ServerKey,
                            playerEmail,
                            content,
                        },
                    },
                };

                using var response = await _httpClient.PostAsJsonAsync(server.GraphqlUrl, body, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Failed to relay Discord chat to shard {ServerKey}: HTTP {StatusCode}.",
                        server.ServerKey,
                        (int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error relaying Discord chat to shard {ServerKey}.", server.ServerKey);
            }
        }
    }
}
