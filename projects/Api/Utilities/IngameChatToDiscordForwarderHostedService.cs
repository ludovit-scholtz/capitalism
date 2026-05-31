using System.Net.Http.Json;
using Api.Configuration;
using Api.Types;
using HotChocolate.Execution;
using HotChocolate.Subscriptions;
using Microsoft.Extensions.Options;

namespace Api.Utilities;

/// <summary>
/// Subscribes to the in-game chat topic and forwards each visible message to the master server's
/// <c>forwardInGameChatToDiscord</c> mutation, where the Discord bot mirrors it into the bridged
/// Discord channel. Messages that originated from Discord (tracked by
/// <see cref="BridgedChatMessageTracker"/>) are skipped to avoid echo loops. Disabled unless the
/// master chat bridge is configured.
/// </summary>
public sealed class IngameChatToDiscordForwarderHostedService : BackgroundService
{
    private const string ForwardMutation = """
        mutation ForwardInGameChatToDiscord($input: ForwardChatToDiscordInput!) {
          forwardInGameChatToDiscord(input: $input) {
            accepted
          }
        }
        """;

    private readonly ITopicEventReceiver _receiver;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<MasterServerRegistrationOptions> _options;
    private readonly BridgedChatMessageTracker _bridgedTracker;
    private readonly ILogger<IngameChatToDiscordForwarderHostedService> _logger;

    public IngameChatToDiscordForwarderHostedService(
        ITopicEventReceiver receiver,
        IHttpClientFactory httpClientFactory,
        IOptions<MasterServerRegistrationOptions> options,
        BridgedChatMessageTracker bridgedTracker,
        ILogger<IngameChatToDiscordForwarderHostedService> logger)
    {
        _receiver = receiver;
        _httpClientFactory = httpClientFactory;
        _options = options;
        _bridgedTracker = bridgedTracker;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.ChatBridgeEnabled
            || string.IsNullOrWhiteSpace(options.ApiUrl)
            || string.IsNullOrWhiteSpace(options.RegistrationKey)
            || string.IsNullOrWhiteSpace(options.ServerKey))
        {
            _logger.LogInformation("In-game chat to Discord bridge disabled or not configured.");
            return;
        }

        try
        {
            var stream = await _receiver.SubscribeAsync<InGameChatMessage>(
                nameof(Subscription.ChatMessageSent),
                stoppingToken);

            await foreach (var message in stream.ReadEventsAsync().WithCancellation(stoppingToken))
            {
                if (message is null || !message.IsVisible || _bridgedTracker.IsBridged(message.Id))
                {
                    continue;
                }

                await ForwardAsync(message, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "In-game chat to Discord forwarder stopped unexpectedly.");
        }
    }

    private async Task ForwardAsync(InGameChatMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("master-server");
            var body = new
            {
                query = ForwardMutation,
                variables = new
                {
                    input = new
                    {
                        registrationKey = _options.Value.RegistrationKey,
                        serverKey = _options.Value.ServerKey,
                        authorDisplayName = message.AuthorDisplayName,
                        content = message.Content,
                    },
                },
            };

            using var response = await client.PostAsJsonAsync(_options.Value.ApiUrl, body, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to forward in-game chat to Discord: HTTP {StatusCode}.",
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error forwarding in-game chat message to Discord.");
        }
    }
}
