using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MoonShard.DiscordBot.AudioServices;
using MoonShard.DiscordBot.Common;
using MoonShard.DiscordBot.Services.AudioPlayers;

namespace MoonShard.DiscordBot.ExternalServices.Bilibili;

public class BilibiliVideoJob : UrlAudioPlayerJob
{
    private static readonly HttpClient HttpClient = new();

    private BilibiliVideoJob(string name, string av, string cid) : base(name)
    {
        Av = av;
        Cid = cid;
    }

    private string Av { get; }

    private string Cid { get; }

    public static async Task<BilibiliVideoJob> CreateAsync(string url, CancellationToken cancellationToken = default)
    {
        ParseUrl(url, out var av, out var bv);
        if (av is not {Length: > 0} && bv is not {Length: > 0})
            throw new Exception("Neither av nor bv is found in the url");

        var info = await GetVideoInfoAsync(av, bv, cancellationToken);
        return new BilibiliVideoJob(info.Title, info.Av.ToString(), info.Cid.ToString());
    }

    private static async Task<VideoInfo> GetVideoInfoAsync(string? av, string? bv, CancellationToken cancellationToken)
    {
        var url = new UriBuilder("https://api.bilibili.com/x/web-interface/view")
        {
            Query =
                av is {Length: > 0}
                    ? $"&aid={av}"
                    : bv is {Length: > 0}
                        ? $"&bvid={bv}"
                        : throw new Exception("Either av or bv must be specified")
        };

        var response = await HttpClient.GetStringAsync(url.ToString(), cancellationToken);
        var result = JsonSerializer.Deserialize<Response<VideoInfo>>(response);
        return result switch
        {
            {Code: 0, Data: { }} => result.Data,
            {Code: -400} => throw new IOException($"Bad Request: {result.Message}"),
            {Code: -403} => throw new IOException($"Forbidden: {result.Message}"),
            {Code: -404} => throw new IOException($"Video Not Found: {result.Message}"),
            {Code: 62002} => throw new IOException($"Video is hidden or locked: {result.Message}"),
            {Code: 61004} => throw new IOException($"Video is pending review: {result.Message}"),
            _ => throw new IOException($"Unknown error: {result?.Message} ({result?.Code})")
        };
    }

    private static async Task<string> GetVideoStreamUrlAsync(string av, string cid, CancellationToken cancellationToken)
    {
        var url = new UriBuilder("https://api.bilibili.com/x/player/playurl")
        {
            Query = $"avid={av}&cid={cid}&qn=64&fnval=1&fnver=0&fourk=0"
        };

        var response = await HttpClient.GetStringAsync(url.ToString(), cancellationToken);
        var result = JsonSerializer.Deserialize<Response<PlayUrl>>(response);
        return result switch
        {
            {Code: 0, Data.DUrls: { } dUrls} => dUrls switch
            {
                {Length: > 0} => dUrls[0].Url,
                _ => throw new IOException($"No video stream found for av{av}")
            },
            {Code: -400} => throw new IOException($"Bad Request: {result.Message}"),
            {Code: -404} => throw new IOException($"Video Not Found: {result.Message}"),
            _ => throw new IOException($"Unknown error: {result?.Message} ({result?.Code})")
        };
    }

    private static void ParseUrl(string url, out string? av, out string? bv)
    {
        av = null;
        bv = null;

        var avRegex = new Regex(@"^av(\d+)\/?$");
        var bvRegex = new Regex(@"^BV([a-zA-Z0-9]{10})\/?$");

        foreach (var segment in new Uri(url, UriKind.RelativeOrAbsolute).Segments)
        {
            if (avRegex.Match(segment) is {Success: true, Groups: { } avGroups})
            {
                av = avGroups[1].Value;
                break;
            }

            if (bvRegex.Match(segment) is {Success: true, Groups: { } bvGroups})
            {
                bv = bvGroups[1].Value;
                break;
            }
        }
    }

    public override async Task PlayAsync(int bitRate, BufferedPacketStream outputStream, AsyncPauseToken pauseToken,
        CancellationToken cancellationToken)
    {
        var streamUrl = await GetVideoStreamUrlAsync(Av, Cid, cancellationToken);
        await PlayAsync(streamUrl, bitRate, outputStream, pauseToken, cancellationToken);
    }

    private class Response<T>
    {
        [JsonPropertyName("code")] public int Code { get; set; } = -400;

        [JsonPropertyName("message")] public string Message { get; set; } = "";

        [JsonPropertyName("data")] public T? Data { get; set; }
    }

    private class VideoInfo
    {
        [JsonPropertyName("aid")] public int Av { get; set; }

        [JsonPropertyName("cid")] public int Cid { get; set; }

        [JsonPropertyName("title")] public string Title { get; set; }
    }

    private class PlayUrl
    {
        [JsonPropertyName("durl")] public DUrl[]? DUrls { get; set; }
    }

    private class DUrl
    {
        [JsonPropertyName("url")] public string Url { get; set; }
    }
}