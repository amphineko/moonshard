namespace MoonShard.DiscordBot.Services.AudioPlayers;

public class ExternalPlaybackException : PlaybackException
{
    public ExternalPlaybackException(string message, Exception innerException) : base(message, innerException)
    {
    }
}