using System.Runtime.CompilerServices;
using Discord;
using MoonShard.DiscordBot.Services.AudioClients;

namespace MoonShard.DiscordBot.Services.AudioPlayers;

public class AudioPlayerRepository
{
    public AudioPlayerRepository(AudioClientRepository clients)
    {
        Clients = clients;
    }

    private AudioClientRepository Clients { get; }

    private ConditionalWeakTable<AudioClientContext, AudioPlayer> Players { get; } = new();

    public async Task EnqueueAsync(ulong guildId, IVoiceChannel voiceChannel, UrlAudioPlayerJob job,
        IMessageChannel statusChannel)
    {
        var context = await Clients.GetOrCreateAsync(guildId, voiceChannel, statusChannel);

        if (context.VoiceChannelId != voiceChannel.Id)
            throw new Exception("Audio client has already connected to a different voice channel.");

        if (!Players.TryGetValue(context, out var player))
        {
            player = new AudioPlayer(voiceChannel.Bitrate, context);
            Players.Add(context, player);

            player.PlaybackCompleted += (_, failedJob) =>
            {
                statusChannel.SendMessageAsync($"{failedJob.Name} has finished playing.");
            };
            player.PlaybackError += (_, t) =>
            {
                var (failedJob, error) = t;

                statusChannel.SendMessageAsync(error switch
                {
                    ExternalPlaybackException {Message: { } message} =>
                        $"External error occured while playing {failedJob.Name}: {message}",
                    InternalPlaybackException {Message: { } message} =>
                        $"Internal error occured while playing {failedJob.Name}: {message}",
                    {Message: { } message} =>
                        $"Unexpected error occured while playing {failedJob.Name}: {message}"
                });
            };

            player.Start();
        }

        await player.Enqueue(job);
    }

    public void Pause(ulong guildId, IVoiceChannel voiceChannel)
    {
        GetCurrentPlayer(guildId, voiceChannel).Pause();
    }

    public void Resume(ulong guildId, IVoiceChannel voiceChannel)
    {
        GetCurrentPlayer(guildId, voiceChannel).Resume();
    }

    private AudioPlayer GetCurrentPlayer(ulong guildId, IVoiceChannel voiceChannel)
    {
        var context = Clients.Get(guildId);

        if (context is not {VoiceChannelId: var currentVoiceChannelId})
            throw new Exception("Audio client is not connected to a voice channel.");

        if (currentVoiceChannelId != voiceChannel.Id)
            throw new Exception("Audio client is not connected to your voice channel.");

        if (!Players.TryGetValue(context, out var player))
            throw new Exception("No audio player found for this voice channel.");

        return player;
    }

    public void Skip(ulong guildId, IVoiceChannel voiceChannel)
    {
        GetCurrentPlayer(guildId, voiceChannel).Skip();
    }
}