using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Api.Utilities;

public static class ChangeTrackerExtensions
{
    /// <summary>
    /// The tracked entries of one type, read without the full change-detection pass that <see cref="ChangeTracker.Entries{TEntity}"/>
    /// starts first (about 0.2 s with the ~78,000 entities of a large tick, #646). The states of entities edited by plain property
    /// assignment are therefore not up to date, so use this only to find entities that were added or removed through the context, which
    /// is known immediately. The caller's <see cref="ChangeTracker.AutoDetectChangesEnabled"/> setting is kept, and a later save still
    /// detects every edit.
    /// </summary>
    public static List<EntityEntry<TEntity>> TrackedEntriesWithoutDetection<TEntity>(this ChangeTracker tracker)
        where TEntity : class
    {
        var autoDetectChanges = tracker.AutoDetectChangesEnabled;
        tracker.AutoDetectChangesEnabled = false;
        try
        {
            return tracker.Entries<TEntity>().ToList();
        }
        finally
        {
            tracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }
}
