using MoonShard.DiscordBot.AudioServices;
using MoonShard.DiscordBot.Common;
using MoonShard.DiscordBot.Services.AudioPlayers;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

public class NeteaseMusicJob : UrlAudioPlayerJob
{
    internal NeteaseMusicJob(string name, int id, NeteaseMusicClient client) : base(name)
    {
        Id = id;
        Client = client;
    }

    private int Id { get; }

    private NeteaseMusicClient Client { get; }

    public override async Task PlayAsync(int bitRate, BufferedPacketStream outputStream, AsyncPauseToken pauseToken,
        CancellationToken cancellationToken)
    {
        string downloadUrl;
        try
        {
            downloadUrl = await await Client
                // try endpoint "/song/download/url"
                .GetSongDownloadUrl(Id.ToString(), cancellationToken)
                .ContinueWith(async downloadUrlTask =>
                {
                    if (downloadUrlTask.IsCompletedSuccessfully)
                        return downloadUrlTask.Result;

                    // if failed, try endpoint "/song/url"
                    return await Client.GetSongUrl(Id.ToString(), cancellationToken);
                }, cancellationToken);
        }
        catch (Exception e)
        {
            throw new ExternalPlaybackException(e.Message, e);
        }

        await PlayAsync(downloadUrl, bitRate, outputStream, pauseToken, cancellationToken);
    }

    internal static async Task<NeteaseMusicJob> CreateAsync(int id, NeteaseMusicClient client)
    {
        var song = await client.GetSongDetail(id.ToString());
        var name = $"{song.Name} by {song.Artists[0].Name}";
        return new NeteaseMusicJob(name, id, client);
    }
}