using System.Text.Json.Serialization;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

internal class SongDownloadUrlResponse : Response
{
    [JsonPropertyName("data")] public SongDownloadUrl Data { get; set; }
}