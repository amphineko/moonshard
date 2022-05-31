namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

public class NeteaseMusicJobFactory
{
    /// <param name="endpoint">should be null when api endpoint is not configured, and job creation will throw</param>
    public NeteaseMusicJobFactory(string? endpoint)
    {
        Client = endpoint is { } ? new NeteaseMusicClient(endpoint) : null;
    }

    private NeteaseMusicClient? Client { get; }

    public async Task<NeteaseMusicJob> CreateAsync(int id)
    {
        return Client is { }
            ? await NeteaseMusicJob.CreateAsync(id, Client)
            : throw new InvalidOperationException("Netease Music API endpoint is not configured");
    }
}