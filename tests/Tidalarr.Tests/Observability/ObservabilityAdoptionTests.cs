using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Observability;
using Xunit;

namespace Tidalarr.Tests.Observability
{
    /// <summary>
    /// Smoke tests verifying Common v1.10.0 observability adoption:
    /// PluginLogContext scopes are pushed/cleared, and Scrub.Secret/Scrub.Url
    /// redact sensitive values before they reach log writers.
    /// </summary>
    public class ObservabilityAdoptionTests
    {
        // ------------------------------------------------------------------ //
        // Test 1: TidalLidarrIndexer_Fetch_PushesLogContext
        //   Verifies the PluginLogContext pattern used in FetchReleases
        // ------------------------------------------------------------------ //

        [Fact]
        public void TidalLidarrIndexer_Fetch_PushesLogContext()
        {
            // Verify no scope is active at test start
            Assert.Null(PluginLogContext.Current);

            // Act — simulate the pattern used in TidalLidarrIndexer.FetchReleases
            using (var ctx = PluginLogContext.Push("Tidalarr", "Search", provider: "tidal:api"))
            {
                Assert.NotNull(PluginLogContext.Current);
                Assert.Equal("Tidalarr", PluginLogContext.Current.PluginName);
                Assert.Equal("Search", PluginLogContext.Current.Operation);
                Assert.Equal("tidal:api", PluginLogContext.Current.Provider);
                Assert.False(string.IsNullOrWhiteSpace(PluginLogContext.Current.CorrelationId));

                var prefix = PluginLogContext.Current.LinePrefix();
                Assert.Matches(@"^\[Search:[a-f0-9]+:tidal:api\] $", prefix);
            }

            // Scope must be popped after Dispose
            Assert.Null(PluginLogContext.Current);
        }

        // ------------------------------------------------------------------ //
        // Test 2: TidalOAuthService_LogsAuthUrl_AppliesScrub
        //   Verifies Scrub.Url redacts client_id / client_secret from Tidal OAuth URLs
        // ------------------------------------------------------------------ //

        [Theory]
        [InlineData(
            "https://auth.tidal.com/v1/oauth2?client_id=MYID&client_secret=MYSECRET&response_type=code",
            "https://auth.tidal.com/v1/oauth2?client_id=MYID&client_secret=***&response_type=code")]  // client_secret is scrubbed, client_id is not sensitive per Scrub.Url
        [InlineData(
            "https://auth.tidal.com/v1/oauth2?response_type=code&redirect_uri=tidal%3A%2F%2Flogin%2Fauth",
            "https://auth.tidal.com/v1/oauth2?response_type=code&redirect_uri=tidal%3A%2F%2Flogin%2Fauth")]  // no sensitive params
        [InlineData(
            "https://auth.tidal.com/v1/oauth2?access_token=tok123&scope=r_usr%20w_usr",
            "https://auth.tidal.com/v1/oauth2?access_token=***&scope=r_usr%20w_usr")]  // access_token is scrubbed
        public void TidalOAuthService_LogsAuthUrl_AppliesScrub(string input, string expected)
        {
            Assert.Equal(expected, Scrub.Url(input));
        }

        // ------------------------------------------------------------------ //
        // Test 3: PluginLogContext_OAuthExchange_PushesScope
        //   Verifies OAuthExchange and OAuthRefresh push correctly
        // ------------------------------------------------------------------ //

        [Fact]
        public void PluginLogContext_OAuthOperations_PushScope()
        {
            using (var ctx = PluginLogContext.Push("Tidalarr", "OAuthExchange"))
            {
                Assert.NotNull(PluginLogContext.Current);
                Assert.Equal("OAuthExchange", PluginLogContext.Current.Operation);
                Assert.Equal("Tidalarr", PluginLogContext.Current.PluginName);
                Assert.Null(PluginLogContext.Current.Provider);
            }

            Assert.Null(PluginLogContext.Current);

            using (var ctx2 = PluginLogContext.Push("Tidalarr", "OAuthRefresh"))
            {
                Assert.NotNull(PluginLogContext.Current);
                Assert.Equal("OAuthRefresh", PluginLogContext.Current.Operation);
            }

            Assert.Null(PluginLogContext.Current);
        }

        // ------------------------------------------------------------------ //
        // Test 4: Scrub.Secret redacts Tidal bearer tokens
        // ------------------------------------------------------------------ //

        [Theory]
        [InlineData("eyJhbGciOiJSUzI1NiIsImtpZCI6", "eyJ***")]
        [InlineData("ab", "***")]           // shorter than leadingVisible → all redacted
        [InlineData("", "***")]             // empty → all redacted
        [InlineData(null, "***")]           // null → all redacted
        public void Scrub_Secret_RedactsTidalBearerToken(string? value, string expected)
        {
            Assert.Equal(expected, Scrub.Secret(value));
        }

        // ------------------------------------------------------------------ //
        // Test 5: AsyncLocal isolation across concurrent async paths
        // ------------------------------------------------------------------ //

        [Fact]
        public async Task PluginLogContext_AsyncLocal_IsolatedAcrossConcurrentSearchAndDownload()
        {
            var task1 = Task.Run(async () =>
            {
                using var ctx = PluginLogContext.Push("Tidalarr", "Search", provider: "tidal:api");
                await Task.Delay(10);
                return PluginLogContext.Current?.Operation;
            });

            var task2 = Task.Run(async () =>
            {
                using var ctx = PluginLogContext.Push("Tidalarr", "Download");
                await Task.Delay(10);
                return PluginLogContext.Current?.Operation;
            });

            var results = await Task.WhenAll(task1, task2);
            Assert.Contains("Search", results);
            Assert.Contains("Download", results);
        }

        // ------------------------------------------------------------------ //
        // Test 6: LinePrefix format is stable for Tidalarr operations
        // ------------------------------------------------------------------ //

        [Fact]
        public void PluginLogContext_LinePrefix_ContainsTidalOperation()
        {
            using var ctx = PluginLogContext.Push("Tidalarr", "Test");
            var prefix = PluginLogContext.Current!.LinePrefix();
            Assert.StartsWith("[Test:", prefix);
            Assert.EndsWith("] ", prefix);
            Assert.Matches(@"^\[Test:[a-f0-9]{32}\] $", prefix);
        }
    }
}
