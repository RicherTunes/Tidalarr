using System.Net;
using System.Text;
using System.Text.Json;
using Lidarr.Plugin.Common.Errors;
using Tidalarr.Core.Interfaces;
using Tidalarr.Core.Models;
using Tidalarr.Domain.Api;

namespace Tidalarr.Tests;

/// <summary>
/// T-paged-truncation-guard: TidalApiClient.GetAlbumTracksAsync used to fetch exactly one page
/// (limit=1000, offset implicitly 0) and never checked the server-declared totalNumberOfItems
/// against what was actually returned. If Tidal ever paginates a response (page size capped
/// below the requested limit, or a genuinely huge album), the extra tracks were silently
/// dropped -- an incomplete album would import as if it were complete, or with the wrong
/// track count.
///
/// Fix: page through offset/limit until all declared items are collected, and fail loudly
/// (Common's PagedResponseValidator / PagedResponseIntegrityException) if pagination stalls
/// before the declared total is reached, instead of silently returning a partial list.
///
/// host-free-ci: this test has no Lidarr host dependency and must be explicitly re-included
/// after the ExcludeHostBridge=true Tidal*.cs remove in Tidalarr.Tests.csproj.
/// </summary>
public class TidalApiClientPaginationTests
{
    [Fact]
    public async Task GetAlbumTracksAsync_MultiPageAlbum_FetchesAllDeclaredTracks()
    {
        // Server declares 3 tracks total but only returns 2 per page -- exercises the
        // offset/limit follow-up request.
        PagedHandler handler = new(
        [
            (0, PageJson(totalNumberOfItems: 3, ids: [1, 2])),
            (2, PageJson(totalNumberOfItems: 3, ids: [3])),
        ]);
        TidalApiClient client = new(new HttpClient(handler), new PaginationAuth());

        List<TidalTrackInfo> tracks = await client.GetAlbumTracksAsync("al1");

        Assert.Equal(3, tracks.Count);
        Assert.Equal(["1", "2", "3"], tracks.Select(t => t.Id));
        Assert.Equal(2, handler.RequestCount); // proves a second page was actually fetched
    }

    [Fact]
    public async Task GetAlbumTracksAsync_PaginationStalls_ThrowsInsteadOfSilentTruncation()
    {
        // Server declares 5 tracks but the second page comes back empty (server bug / stall).
        // The old behaviour returned whatever partial list it had; the fix must fail loudly.
        PagedHandler handler = new(
        [
            (0, PageJson(totalNumberOfItems: 5, ids: [1, 2])),
            (2, PageJson(totalNumberOfItems: 5, ids: [])),
        ]);
        TidalApiClient client = new(new HttpClient(handler), new PaginationAuth());

        PagedResponseIntegrityException ex = await Assert.ThrowsAsync<PagedResponseIntegrityException>(
            () => client.GetAlbumTracksAsync("al1"));

        Assert.Contains("al1", ex.Message);
    }

    [Fact]
    public async Task GetAlbumTracksAsync_SinglePageAlbum_DoesNotIssueFollowUpRequest()
    {
        // Regression guard: the common case (album fits in one page) must not regress into
        // issuing a wasted second HTTP call.
        PagedHandler handler = new([(0, PageJson(totalNumberOfItems: 2, ids: [1, 2]))]);
        TidalApiClient client = new(new HttpClient(handler), new PaginationAuth());

        List<TidalTrackInfo> tracks = await client.GetAlbumTracksAsync("al1");

        Assert.Equal(2, tracks.Count);
        Assert.Equal(1, handler.RequestCount);
    }

    private static string PageJson(int totalNumberOfItems, int[] ids)
    {
        var items = ids.Select(id => new
        {
            id,
            title = $"Track {id}",
            artist = new { name = "Artist", id = "a1" },
            album = new { id = "al1", title = "Alb" },
            trackNumber = id,
            duration = 180,
            streamReady = true,
            audioQuality = "LOSSLESS"
        });
        return JsonSerializer.Serialize(new { items, totalNumberOfItems });
    }

    private sealed class PagedHandler(List<(int Offset, string Json)> pages) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            string query = request.RequestUri?.Query ?? string.Empty;
            int offset = 0;
            foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "offset" && int.TryParse(kv[1], out int parsed))
                {
                    offset = parsed;
                }
            }

            string json = pages.FirstOrDefault(p => p.Offset == offset).Json
                ?? throw new InvalidOperationException($"Unexpected page request at offset={offset}");

            HttpResponseMessage response = new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class PaginationAuth : ITidalAuth
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
}
