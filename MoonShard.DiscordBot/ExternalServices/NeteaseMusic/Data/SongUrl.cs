using System.Text.Json.Serialization;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

internal class SongUrl
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }
}