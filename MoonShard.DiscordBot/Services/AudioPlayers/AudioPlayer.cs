using System.Threading.Channels;
using Discord.Audio;
using MoonShard.DiscordBot.AudioServices;
using MoonShard.DiscordBot.Common;
using MoonShard.DiscordBot.Services.AudioClients;

namespace MoonShard.DiscordBot.Services.AudioPlayers;

public class AudioPlayer
{
    /// <summary>
    ///     Cancellation token to cancel (skip) current playing job.
    /// </summary>
    private CancellationTokenSource? _currentSkipTokenSource;

    public AudioPlayer(int bitRate, AudioClientContext context)
    {
        AudioClient = context.Client;
        BitRate = bitRate;
        OutputStream = context.BufferedPacketStream;
    }

    private IAudioClient AudioClient { get; }

    private int BitRate { get; }

    private BufferedPacketStream OutputStream { get; }

    private AsyncPauseTokenSource PauseTokenSource { get; } = new();

    private Channel<UrlAudioPlayerJob> Queue { get; } = Channel.CreateUnbounded<UrlAudioPlayerJob>();

    /// <summary>
    ///     Cancellation token to cancel current and pending jobs.
    /// </summary>
    private CancellationTokenSource StopTokenSource { get; } = new();

    /// <summary>
    ///     A queued item has completed playing.
    /// </summary>
    public event EventHandler<UrlAudioPlayerJob>? PlaybackCompleted;

    /// <summary>
    ///     Exception thrown during playback
    /// </summary>
    public event EventHandler<ValueTuple<UrlAudioPlayerJob, Exception>>? PlaybackError;

    public async Task Enqueue(UrlAudioPlayerJob job)
    {
        await Queue.Writer.WriteAsync(job);
    }

    public void Pause()
    {
        PauseTokenSource.Pause();
    }

    public void Resume()
    {
        PauseTokenSource.Resume();
    }

    public void Start()
    {
        _ = RunAsync(StopTokenSource.Token).ConfigureAwait(false);
    }

    public void Stop()
    {
        StopTokenSource.Cancel();
        PauseTokenSource.Resume(); // resume to unblock paused tasks
    }

    private async Task RunAsync(CancellationToken stopToken)
    {
        try
        {
            while (await Queue.Reader.WaitToReadAsync(stopToken) && !stopToken.IsCancellationRequested)
            while (Queue.Reader.TryRead(out var job))
                try
                {
                    var skip = new CancellationTokenSource();
                    var cancelToken = CancellationTokenSource.CreateLinkedTokenSource(stopToken, skip.Token).Token;

                    // always cancel last job
                    Interlocked.Exchange(ref _currentSkipTokenSource, new CancellationTokenSource())?.Dispose();

                    await AudioClient.SetSpeakingAsync(true);
                    await job.PlayAsync(BitRate, OutputStream, PauseTokenSource.Token, cancelToken);
                    await AudioClient.SetSpeakingAsync(false);

                    PlaybackCompleted?.Invoke(this, job);
                }
                catch (Exception e)
                {
                    PlaybackError?.Invoke(this, (job, e));
                }
        }
        finally
        {
            Interlocked.Exchange(ref _currentSkipTokenSource, null)?.Dispose();
        }

        // don't throw for stopToken, it's an expected cancellation
    }

    public void Skip()
    {
        _currentSkipTokenSource?.Cancel();
    }
}