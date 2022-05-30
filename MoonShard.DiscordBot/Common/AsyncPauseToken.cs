namespace MoonShard.DiscordBot.Common;

public sealed class AsyncPauseToken
{
    private readonly AsyncPauseTokenSource _source;

    internal AsyncPauseToken(AsyncPauseTokenSource source)
    {
        _source = source;
    }

    public bool IsPausedRequested => _source is {IsPaused: true};

    public async Task WaitWhilePausedAsync()
    {
        await _source.WaitWhilePausedAsync();
    }
}