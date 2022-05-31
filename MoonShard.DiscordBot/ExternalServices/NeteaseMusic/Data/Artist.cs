using System.Text.Json.Serialization;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

internal class Artist
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("name")] public string Name { get; set; }
}