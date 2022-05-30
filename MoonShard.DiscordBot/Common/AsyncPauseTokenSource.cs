using System.Collections.Concurrent;

namespace MoonShard.DiscordBot.Common;

public class AsyncPauseTokenSource : IDisposable
{
    private readonly ManualResetEvent _unpauseEvent = new(false);

    private readonly ConcurrentBag<TaskCompletionSource> _waiters = new();

    public AsyncPauseTokenSource()
    {
        ThreadPool.RegisterWaitForSingleObject(_unpauseEvent, (_, _) =>
        {
            while (_waiters.TryTake(out var waiter)) waiter.TrySetResult();
        }, null, -1, false);
    }

    public bool IsPaused { get; private set; }

    public AsyncPauseToken Token => new(this);

    public void Dispose()
    {
        _unpauseEvent.Dispose();
    }

    public void Pause()
    {
        IsPaused = true;
    }

    public void Resume()
    {
        IsPaused = false;
        _unpauseEvent.Set();
    }

    internal Task WaitWhilePausedAsync()
    {
        if (!IsPaused) return Task.CompletedTask;

        var tcs = new TaskCompletionSource();
        _waiters.Add(tcs);

        if (!IsPaused) tcs.TrySetResult();

        return tcs.Task;
    }
}