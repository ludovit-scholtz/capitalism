namespace Api.Security;

/// <summary>One recorded object-authorization denial: the real reason, never shown to the caller.</summary>
public sealed record ObjectDenialEvent(
    DateTime OccurredAtUtc,
    Guid ActorUserId,
    Guid? ApiKeyId,
    string ObjectType,
    Guid ObjectId,
    string Reason,
    bool IsProbeSignal);

public sealed record ObjectDenialCount(string Key, int Count);

/// <summary>Aggregated view of the recorded window for the admin dashboard.</summary>
public sealed record ObjectDenialSummary(
    long TotalRecorded,
    int WindowSize,
    int ProbeSignals,
    IReadOnlyList<ObjectDenialCount> ByObjectType,
    IReadOnlyList<ObjectDenialCount> ByReason);

/// <summary>
/// Thread-safe ring buffer of the most recent object-authorization denials (#392). The structured log remains the durable audit
/// stream; this gives admins an in-process view of recent enumeration probes without touching the database. Registered as a singleton.
/// The window is process-local (a restart or another replica starts empty) and each actor keeps at most <c>perActorCapacity</c> entries,
/// so one noisy caller cannot push everybody else's probes out of the window.
/// </summary>
public sealed class ObjectDenialRecorder(int capacity = 500, int perActorCapacity = 50)
{
    private readonly object gate = new();
    private readonly LinkedList<ObjectDenialEvent> events = new();
    private long total;

    public long TotalRecorded
    {
        get
        {
            lock (gate)
            {
                return total;
            }
        }
    }

    /// <summary>Benign reasons (a context mismatch or a state restriction) are not enumeration probes; unknown reasons fail safe to a probe.</summary>
    public static bool IsProbeSignal(string reason)
        => reason is not (ObjectAuthorizationService.WrongContextReason or ObjectAuthorizationService.NotAvailableReason);

    public void Record(ObjectDenialEvent denial)
    {
        lock (gate)
        {
            total++;
            var sameActor = 0;
            LinkedListNode<ObjectDenialEvent>? oldestOfActor = null;
            for (var node = events.First; node is not null; node = node.Next)
            {
                if (node.Value.ActorUserId == denial.ActorUserId)
                {
                    sameActor++;
                    oldestOfActor = node;
                }
            }

            if (sameActor >= Math.Max(1, perActorCapacity) && oldestOfActor is not null)
            {
                events.Remove(oldestOfActor);
            }

            events.AddFirst(denial);
            while (events.Count > Math.Max(1, capacity))
            {
                events.RemoveLast();
            }
        }
    }

    /// <summary>Newest first, at most <paramref name="limit"/> events.</summary>
    public IReadOnlyList<ObjectDenialEvent> Recent(int limit)
    {
        lock (gate)
        {
            return events.Take(Math.Max(0, limit)).ToList();
        }
    }

    public ObjectDenialSummary Summarize() => Snapshot(0).Summary;

    /// <summary>The summary and the newest <paramref name="limit"/> events from one locked view, so their counts always agree.</summary>
    public (ObjectDenialSummary Summary, IReadOnlyList<ObjectDenialEvent> Recent) Snapshot(int limit)
    {
        ObjectDenialEvent[] snapshot;
        long totalRecorded;
        lock (gate)
        {
            snapshot = events.ToArray();
            totalRecorded = total;
        }

        static IReadOnlyList<ObjectDenialCount> Count(IEnumerable<string> keys)
            => keys.GroupBy(k => k).Select(g => new ObjectDenialCount(g.Key, g.Count()))
                .OrderByDescending(c => c.Count).ThenBy(c => c.Key, StringComparer.Ordinal).ToList();

        var summary = new ObjectDenialSummary(
            totalRecorded,
            snapshot.Length,
            snapshot.Count(e => e.IsProbeSignal),
            Count(snapshot.Select(e => e.ObjectType)),
            Count(snapshot.Select(e => e.Reason)));
        return (summary, snapshot.Take(Math.Max(0, limit)).ToList());
    }
}
