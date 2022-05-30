using Discord;
using Discord.Audio;
using Discord.Net;
using MoonShard.DiscordBot.AudioServices;

namespace MoonShard.DiscordBot.Services.AudioClients;

public sealed class AudioClientContext : IDisposable
{
    private int _stopRequested;

    public AudioClientContext(ulong voiceChannelId, IAudioClient client, IMessageChannel statusChannel)
    {
        VoiceChannelId = voiceChannelId;
        Client = client;

        client.Disconnected += async error =>
        {
            var message = error switch
            {
                {InnerException: WebSocketClosedException ex} => $"{ex.Reason} ({ex.CloseCode})",
                WebSocketClosedException ex => $"{ex.Reason} ({ex.CloseCode})",
                _ => error.Message
            };

            await Task.WhenAll(
                statusChannel.SendMessageAsync($"Disconnected from voice channel: {message}"),
                StopAsync()
            ).ConfigureAwait(false);
        };

        DirectOpusStream = client.CreateDirectOpusStream();
        BufferedPacketStream = new BufferedPacketStream(DirectOpusStream);
    }

    public ulong VoiceChannelId { get; }

    public BufferedPacketStream BufferedPacketStream { get; }

    public IAudioClient Client { get; }

    private AudioOutStream DirectOpusStream { get; }

    public void Dispose()
    {
        DirectOpusStream.Dispose();
        Client.Dispose();
    }

    public event EventHandler? Stopped;

    public void Start()
    {
        BufferedPacketStream.Start();
    }

    private async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _stopRequested, 1) == 1) return;

        BufferedPacketStream.Stop();
        if (Client.ConnectionState is ConnectionState.Connected or ConnectionState.Connecting)
            await Client.StopAsync();

        Stopped?.Invoke(this, EventArgs.Empty);
    }
}