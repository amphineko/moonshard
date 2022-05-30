using MoonShard.DiscordBot.AudioServices;
using MoonShard.DiscordBot.Common;

namespace MoonShard.DiscordBot.Services.AudioPlayers;

public class LocalFileJob : UrlAudioPlayerJob
{
    private LocalFileJob(string name, string resolvedPath) : base(name)
    {
        ResolvedPath = resolvedPath;
    }

    private string ResolvedPath { get; }

    public static LocalFileJob Create(string filename, string basePath)
    {
        var path = Path.GetFullPath(filename, basePath);
        if (!path.StartsWith(Path.GetFullPath(basePath)))
            throw new ArgumentException("Path traversal detected", nameof(filename));

        return new LocalFileJob(Path.GetFileName(filename), path);
    }

    public override async Task PlayAsync(int bitRate, BufferedPacketStream outputStream, AsyncPauseToken pauseToken,
        CancellationToken cancellationToken)
    {
        await PlayAsync(ResolvedPath, bitRate, outputStream, pauseToken, cancellationToken);
    }
}