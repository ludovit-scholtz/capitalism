using System.Threading.Channels;

namespace MasterApi.Utilities.Discord;

/// <summary>A single in-game chat message awaiting delivery to the bridged Discord channel.</summary>
public sealed record DiscordChatRelayMessage(string AuthorDisplayName, string Content, string? ServerName);

/// <summary>
/// In-memory hand-off between the game servers (which forward in-game chat through the
/// <c>forwardInGameChatToDiscord</c> mutation) and the Discord bot hosted service that delivers
/// those messages to the bridged Discord channel. Bounded so a slow/disconnected bot cannot grow
/// memory without limit; the oldest messages are dropped when full.
/// </summary>
public sealed class DiscordChatRelay
{
    private readonly Channel<DiscordChatRelayMessage> _channel =
        Channel.CreateBounded<DiscordChatRelayMessage>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    public void Enqueue(DiscordChatRelayMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<DiscordChatRelayMessage> ReadAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
