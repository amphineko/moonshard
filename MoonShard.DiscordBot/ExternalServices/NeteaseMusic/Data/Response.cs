using System.Text.Json.Serialization;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

internal class Response
{
    [JsonPropertyName("code")] public int Code { get; set; }
}