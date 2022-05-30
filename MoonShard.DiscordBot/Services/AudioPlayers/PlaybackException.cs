namespace MoonShard.DiscordBot.Services.AudioPlayers;

public class PlaybackException : AggregateException
{
    public PlaybackException(string message, Exception innerException) : base(message, innerException)
    {
    }
}