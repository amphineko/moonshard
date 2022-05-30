namespace MoonShard.DiscordBot.Services.AudioPlayers;

public class InternalPlaybackException : PlaybackException
{
    public InternalPlaybackException(string message, Exception innerException) : base(message, innerException)
    {
    }
}