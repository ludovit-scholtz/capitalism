using MasterApi.Utilities;

namespace MasterApi.Tests;

public sealed class AsyncKeyedLockTests
{
    [Fact]
    public async Task AcquireAsync_SameKeySerializesConcurrentWorkCaseInsensitively()
    {
        var keyedLock = new AsyncKeyedLock(StringComparer.OrdinalIgnoreCase);
        var activeCount = 0;
        var maxActiveCount = 0;

        async Task RunAsync(string key)
        {
            await using var handle = await keyedLock.AcquireAsync(key);
            var current = Interlocked.Increment(ref activeCount);
            CaptureMax(ref maxActiveCount, current);

            await Task.Delay(20);

            Interlocked.Decrement(ref activeCount);
        }

        await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(index => RunAsync(index % 2 == 0 ? "Government@Capitalism.Game" : "government@capitalism.game")));

        Assert.Equal(1, Volatile.Read(ref maxActiveCount));
    }

    [Fact]
    public async Task AcquireAsync_DifferentKeysDoNotBlockIndependentWork()
    {
        var keyedLock = new AsyncKeyedLock(StringComparer.OrdinalIgnoreCase);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = false;

        var firstTask = Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("first@example.com");
            firstEntered.SetResult();
            await releaseFirst.Task;
        });

        await firstEntered.Task;

        await Task.Run(async () =>
        {
            await using var handle = await keyedLock.AcquireAsync("second@example.com");
            secondEntered = true;
        });

        releaseFirst.SetResult();
        await firstTask;

        Assert.True(secondEntered);
    }

    private static void CaptureMax(ref int target, int candidate)
    {
        while (true)
        {
            var snapshot = Volatile.Read(ref target);
            if (candidate <= snapshot)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref target, candidate, snapshot) == snapshot)
            {
                return;
            }
        }
    }
}