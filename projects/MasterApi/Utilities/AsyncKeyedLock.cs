using System.Collections.Concurrent;

namespace MasterApi.Utilities;

internal sealed class AsyncKeyedLock(IEqualityComparer<string>? comparer = null)
{
    private readonly ConcurrentDictionary<string, LockState> _states = new(comparer ?? StringComparer.Ordinal);

    public async ValueTask<Releaser> AcquireAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        LockState state;
        while (true)
        {
            state = _states.GetOrAdd(key, static _ => new LockState());
            Interlocked.Increment(ref state.RefCount);

            if (_states.TryGetValue(key, out var current) && ReferenceEquals(state, current))
            {
                break;
            }

            ReleaseReference(key, state);
        }

        try
        {
            await state.Semaphore.WaitAsync(cancellationToken);
            return new Releaser(_states, key, state);
        }
        catch
        {
            ReleaseReference(key, state);
            throw;
        }
    }

    private void ReleaseReference(string key, LockState state)
    {
        if (Interlocked.Decrement(ref state.RefCount) != 0)
        {
            return;
        }

        if (_states.TryRemove(new KeyValuePair<string, LockState>(key, state)))
        {
            state.Dispose();
        }
    }

    internal sealed class Releaser(
        ConcurrentDictionary<string, LockState> states,
        string key,
        LockState state) : IDisposable, IAsyncDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            state.Semaphore.Release();

            if (Interlocked.Decrement(ref state.RefCount) == 0
                && states.TryRemove(new KeyValuePair<string, LockState>(key, state)))
            {
                state.Dispose();
            }
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class LockState : IDisposable
    {
        internal SemaphoreSlim Semaphore { get; } = new(1, 1);

        internal int RefCount;

        public void Dispose()
        {
            Semaphore.Dispose();
        }
    }
}