using System.Text.Json.Serialization;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

internal class SongUrlResponse : Response
{
    [JsonPropertyName("data")] public List<SongUrl>? Data { get; set; }
}