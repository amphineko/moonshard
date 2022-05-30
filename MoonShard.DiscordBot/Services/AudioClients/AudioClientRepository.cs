using System.Collections.Concurrent;
using Discord;

namespace MoonShard.DiscordBot.Services.AudioClients;

/// <remarks>
///     Dictionary key is the guild id, which the audio client is connected to.
/// </remarks>
public class AudioClientRepository
{
    private readonly ConcurrentDictionary<ulong, AudioClientContext> _audioClients = new();

    public AudioClientContext? Get(ulong guildId)
    {
        _audioClients.TryGetValue(guildId, out var context);
        return context;
    }

    public async Task<AudioClientContext> GetOrCreateAsync(ulong guildId, IVoiceChannel voiceChannel,
        IMessageChannel statusChannel)
    {
        if (_audioClients.TryGetValue(guildId, out var active) && active is {VoiceChannelId: var voiceChannelId})
        {
            if (voiceChannelId != voiceChannel.Id)
                throw new Exception("Active audio client is connected to a different voice channel.");

            return active;
        }

        var client = await voiceChannel.ConnectAsync(true);
        var context = new AudioClientContext(voiceChannel.Id, client, statusChannel);
        context.Stopped += (_, _) =>
        {
            _audioClients.TryRemove(new KeyValuePair<ulong, AudioClientContext>(guildId, context));
        };
        context.Start();

        _audioClients[guildId] = context;
        return context;
    }
}