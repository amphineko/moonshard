using System.Text.Json.Serialization;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

internal class SongDownloadUrl
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; }
}