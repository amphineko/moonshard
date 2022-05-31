using System.Text.Json.Serialization;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

internal class SongDetailResponse : Response
{
    [JsonPropertyName("songs")] public List<SongDetail>? Songs { get; set; }
}