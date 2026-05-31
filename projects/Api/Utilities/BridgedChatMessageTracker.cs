using System.Collections.Concurrent;

namespace Api.Utilities;

/// <summary>
/// Tracks chat messages that originated from the Discord bridge so the in-game → Discord forwarder
/// does not echo them back to Discord (which would duplicate the message). Entries are short-lived
/// and trimmed once the set grows past a small cap.
/// </summary>
public sealed class BridgedChatMessageTracker
{
    private const int MaxEntries = 1000;
    private readonly ConcurrentDictionary<Guid, DateTime> _bridgedMessageIds = new();

    public void MarkBridged(Guid messageId)
    {
        _bridgedMessageIds[messageId] = DateTime.UtcNow;
        if (_bridgedMessageIds.Count > MaxEntries)
        {
            TrimOldest();
        }
    }

    public bool IsBridged(Guid messageId) => _bridgedMessageIds.ContainsKey(messageId);

    private void TrimOldest()
    {
        foreach (var entry in _bridgedMessageIds.OrderBy(pair => pair.Value).Take(_bridgedMessageIds.Count - MaxEntries))
        {
            _bridgedMessageIds.TryRemove(entry.Key, out _);
        }
    }
}
