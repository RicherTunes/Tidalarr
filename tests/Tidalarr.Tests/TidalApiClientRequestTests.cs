using System.Net;
using System.Text;
using System.Text.Json;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

public class TidalApiClientRequestTests
{
    [Fact]
    public async Task GetTrackAsync_BuildsExpectedEndpointAndQuery()
    {
        CaptureHandler capture = new(JsonSerializer.Serialize(new TidalTrackDto(
            id: "t1",
            title: "T",
            artist: new("A", "a1"),
            album: new("al1", "Alb", new("A", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 1, 1, true, "c"),
            trackNumber: 1,
            duration: 10,
            streamReady: true,
            audioQuality: "LOSSLESS")));
        TidalApiClient client = new(new HttpClient(capture), new RequestAuth());
        TidalTrackInfo _ = await client.GetTrackAsync("t1");
        Assert.Contains("/tracks/t1", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("countryCode=US", capture.LastRequest?.RequestUri?.Query);
        Assert.Contains("sessionId=sess", capture.LastRequest?.RequestUri?.Query);
    }

    [Fact]
    public async Task GetAlbumAsync_BuildsExpectedEndpointAndQuery()
    {
        TidalAlbumDto dto = new("al1", "Alb", new("A", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 1, 1, true, "c");
        CaptureHandler capture = new(JsonSerializer.Serialize(dto));
        TidalApiClient client = new(new HttpClient(capture), new RequestAuth());
        TidalAlbumInfo _ = await client.GetAlbumAsync("al1");
        Assert.Contains("/albums/al1", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("countryCode=US", capture.LastRequest?.RequestUri?.Query);
    }

    [Fact]
    public async Task GetAlbumTracksAsync_BuildsExpectedEndpointAndQuery()
    {
        TidalAlbumTracksDto payload = new([new("t", "T", new("A", "a1"), new("al1", "Alb", new("A", "a1"), DateTime.UtcNow.ToString("yyyy-MM-dd"), 1, 1, true, "c"), 1, 10, true, "LOSSLESS")], 1);
        CaptureHandler capture = new(JsonSerializer.Serialize(payload));
        TidalApiClient client = new(new HttpClient(capture), new RequestAuth());
        List<TidalTrackInfo> _ = await client.GetAlbumTracksAsync("al1");
        Assert.Contains("/albums/al1/tracks", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("limit=1000", capture.LastRequest?.RequestUri?.Query);
    }

    [Fact]
    public async Task SearchAsync_BuildsExpectedEndpointAndQuery()
    {
        TidalSearchResponseDto payload = new(new([]), new([]));
        CaptureHandler capture = new(JsonSerializer.Serialize(payload));
        TidalApiClient client = new(new HttpClient(capture), new RequestAuth());
        TidalSearchResults _ = await client.SearchAsync("abc");
        Assert.Contains("/search", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("query=abc", capture.LastRequest?.RequestUri?.Query);
        Assert.Contains("types=albums%2Ctracks", capture.LastRequest?.RequestUri?.Query);
    }

    /// <summary>
    /// Regression: TidalApiClient previously stored a shared StreamingApiRequestBuilder as a field.
    /// Each call appended to that builder's _queryParams list — so after Test() sent query=test,
    /// the next real search had query=test as the first param and Tidal ignored the actual query,
    /// returning "test"-matching albums (Testimony, Testament, etc.) for every user search.
    /// Fix: create a fresh builder per request. This test ensures the second search only sends
    /// its own query, not a concatenation of both queries.
    /// </summary>
    [Fact]
    public async Task SearchAsync_TwoSequentialSearches_EachSendsOnlyItsOwnQuery()
    {
        TidalSearchResponseDto payload = new(new([]), new([]));
        MultiCaptureHandler capture = new(JsonSerializer.Serialize(payload));
        TidalApiClient client = new(new HttpClient(capture), new RequestAuth());

        // First search (simulates Test() smoke check)
        _ = await client.SearchAsync("test");
        string firstQuery = capture.Requests[0].RequestUri?.Query ?? string.Empty;
        Assert.Contains("query=test", firstQuery);
        // First request must NOT already contain the second query term
        Assert.DoesNotContain("LE+SSERAFIM", firstQuery);
        Assert.DoesNotContain("LE%20SSERAFIM", firstQuery);

        // Second search (simulates real Lidarr user search)
        _ = await client.SearchAsync("LE SSERAFIM BOOMPALA (piano ver.)");
        string secondQuery = capture.Requests[1].RequestUri?.Query ?? string.Empty;

        // The bug: second request contained query=test&...&query=LE%20SSERAFIM%E2%80%A6
        // and Tidal used the first query= value, returning test-related albums.
        // After the fix, query=test must NOT appear in the second request at all.
        Assert.DoesNotContain("query=test", secondQuery);
        Assert.Contains("LE%20SSERAFIM", secondQuery);

        // Sanity: exactly one query= occurrence in each request (no duplicates)
        Assert.Equal(1, CountOccurrences(firstQuery, "query="));
        Assert.Equal(1, CountOccurrences(secondQuery, "query="));
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    [Fact]
    public async Task GetStreamInfoAsync_BuildsExpectedEndpoint_AndParams()
    {
        TidalPlaybackInfoDto dto = new(Convert.ToBase64String(Encoding.UTF8.GetBytes("<MPD/>")), "application/dash+xml", "NONE", null);
        CaptureHandler capture = new(JsonSerializer.Serialize(dto));
        TidalApiClient client = new(new HttpClient(capture), new RequestAuth());
        TidalStreamInfo _ = await client.GetStreamInfoAsync("t1", TidalQuality.Lossless);
        Assert.Contains("/tracks/t1/playbackinfopostpaywall", capture.LastRequest?.RequestUri?.AbsoluteUri);
        Assert.Contains("audioquality=LOSSLESS", capture.LastRequest?.RequestUri?.Query);
        Assert.Contains("playbackmode=STREAM", capture.LastRequest?.RequestUri?.Query);
        Assert.Contains("assetpresentation=FULL", capture.LastRequest?.RequestUri?.Query);
    }

    private class RequestAuth : ITidalAuth
    {
        public bool IsAuthenticated => true;
        public Task<TidalAuthUrl> GenerateAuthUrlAsync()
        {
            return Task.FromResult(new TidalAuthUrl("", "", "", string.Empty));
        }

        public Task<TidalTokens> ExchangeCodeAsync(string authCode, string codeVerifier)
        {
            return Task.FromResult(Default());
        }

        public Task<TidalTokens> RefreshTokensAsync(string refreshToken)
        {
            return Task.FromResult(Default());
        }

        public Task<TidalTokens> GetValidTokensAsync()
        {
            return Task.FromResult(Default());
        }

        public TidalCallbackResult ParseCallbackUrl(string callbackUrl)
        {
            return TidalCallbackResult.Failure("Not implemented in test stub");
        }

        private static TidalTokens Default()
        {
            return new("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");
        }
    }

    private class MultiCaptureHandler(string response, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly string _response = response;
        private readonly HttpStatusCode _code = code;
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            HttpResponseMessage msg = new(this._code)
            {
                Content = new StringContent(this._response, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        }
    }

    private class CaptureHandler(string response, HttpStatusCode code = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly string _response = response;
        private readonly HttpStatusCode _code = code;
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            HttpResponseMessage msg = new(this._code)
            {
                Content = new StringContent(this._response, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(msg);
        }
    }
}



