using System.Text.Json;
using System.Web;

namespace MoonShard.DiscordBot.ExternalServices.NeteaseMusic;

internal class NeteaseMusicClient
{
    internal NeteaseMusicClient(string endpoint)
    {
        Endpoint = new Uri(endpoint);
    }

    private Uri Endpoint { get; }

    private HttpClient HttpClient { get; } = new();

    internal async Task<SongDetail> GetSongDetail(string id)
    {
        var url = new UriBuilder(Endpoint);
        url.Path = Path.Combine(url.Path, "song/detail");

        var query = HttpUtility.ParseQueryString(Endpoint.Query);
        query["ids"] = id;
        url.Query = query.ToString();

        var body = await HttpClient.GetStringAsync(url.Uri);
        var response = JsonSerializer.Deserialize<SongDetailResponse>(body);
        return response is {Code: 200, Songs.Count: > 0}
            ? response.Songs[0]
            : throw new Exception(
                $"Failed to get song detail for {id}: code {response?.Code}, result count {response?.Songs?.Count}");
    }

    internal async Task<string> GetSongDownloadUrl(string id, CancellationToken cancellationToken, int bitRate = 999000)
    {
        var url = new UriBuilder(Endpoint);
        url.Path = Path.Combine(url.Path, "song/download/url");

        var query = HttpUtility.ParseQueryString(Endpoint.Query);
        query["id"] = id;
        query["br"] = bitRate.ToString();
        url.Query = query.ToString();

        var body = await HttpClient.GetStringAsync(url.Uri, cancellationToken);
        var response = JsonSerializer.Deserialize<SongDownloadUrlResponse>(body);
        return
            response is {Code: 200}
                ? response is {Data.Url: { } downloadUrl}
                    ? downloadUrl
                    : throw new Exception($"Song {id} is not available for playing")
                : throw new Exception($"Failed to get song download url for {id}: code {response?.Code}");
    }

    internal async Task<string> GetSongUrl(string id, CancellationToken cancellationToken, int bitRate = 999000)
    {
        var url = new UriBuilder(Endpoint);
        url.Path = Path.Combine(url.Path, "song/url");

        var query = HttpUtility.ParseQueryString(Endpoint.Query);
        query["id"] = id;
        query["br"] = bitRate.ToString();
        url.Query = query.ToString();

        var body = await HttpClient.GetStringAsync(url.Uri, cancellationToken);
        var response = JsonSerializer.Deserialize<SongUrlResponse>(body);
        return
            response is {Code: 200}
                ? response is {Data: {Count: > 0} urls} && !string.IsNullOrWhiteSpace(urls[0].Url)
                    ? urls[0].Url
                    : throw new Exception($"Song {id} is not available for playing")
                : throw new Exception($"Failed to get song download url for {id}: code {response?.Code}");
    }
}