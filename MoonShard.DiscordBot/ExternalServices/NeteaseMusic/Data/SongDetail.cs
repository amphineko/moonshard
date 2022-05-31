using System.Text.Json.Serialization;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

internal class SongDetail
{
    [JsonPropertyName("name")] public string Name { get; set; }

    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("ar")] public Artist[] Artists { get; set; }
}