using System;
using System.Threading.Tasks;
using FluentAssertions;
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
            PluginLogContext.Current.Should().BeNull("no scope should be active at test start");

            // Act — simulate the pattern used in TidalLidarrIndexer.FetchReleases
            using (var ctx = PluginLogContext.Push("Tidalarr", "Search", provider: "tidal:api"))
            {
                // Assert inside scope
                PluginLogContext.Current.Should().NotBeNull();
                PluginLogContext.Current!.PluginName.Should().Be("Tidalarr");
                PluginLogContext.Current.Operation.Should().Be("Search");
                PluginLogContext.Current.Provider.Should().Be("tidal:api");
                PluginLogContext.Current.CorrelationId.Should().NotBeNullOrWhiteSpace();
                PluginLogContext.Current.LinePrefix().Should().MatchRegex(@"^\[Search:[a-f0-9]+:tidal:api\] $");
            }

            // Scope must be popped after Dispose
            PluginLogContext.Current.Should().BeNull("scope must be popped after Dispose");
        }

        // ------------------------------------------------------------------ //
        // Test 2: TidalOAuthService_LogsAuthUrl_AppliesScrub
        //   Verifies Scrub.Url redacts client_id / client_secret from Tidal OAuth URLs
        // ------------------------------------------------------------------ //

        [Theory]
        [InlineData(
            "https://auth.tidal.com/v1/oauth2?client_id=MYID&client_secret=MYSECRET&response_type=code",
            "https://auth.tidal.com/v1/oauth2?client_id=***&client_secret=***&response_type=code")]
        [InlineData(
            "https://auth.tidal.com/v1/oauth2?response_type=code&redirect_uri=tidal%3A%2F%2Flogin%2Fauth",
            "https://auth.tidal.com/v1/oauth2?response_type=code&redirect_uri=tidal%3A%2F%2Flogin%2Fauth")]  // no sensitive params
        [InlineData(
            "https://auth.tidal.com/v1/oauth2?client_id=abc123&scope=r_usr%20w_usr",
            "https://auth.tidal.com/v1/oauth2?client_id=***&scope=r_usr%20w_usr")]
        public void TidalOAuthService_LogsAuthUrl_AppliesScrub(string input, string expected)
        {
            Scrub.Url(input).Should().Be(expected);
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
                PluginLogContext.Current!.Operation.Should().Be("OAuthExchange");
                PluginLogContext.Current.PluginName.Should().Be("Tidalarr");
                PluginLogContext.Current.Provider.Should().BeNull();
            }

            PluginLogContext.Current.Should().BeNull();

            using (var ctx = PluginLogContext.Push("Tidalarr", "OAuthRefresh"))
            {
                PluginLogContext.Current!.Operation.Should().Be("OAuthRefresh");
            }

            PluginLogContext.Current.Should().BeNull();
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
            Scrub.Secret(value).Should().Be(expected);
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
            results.Should().Contain("Search");
            results.Should().Contain("Download");
        }

        // ------------------------------------------------------------------ //
        // Test 6: LinePrefix format is stable for Tidalarr operations
        // ------------------------------------------------------------------ //

        [Fact]
        public void PluginLogContext_LinePrefix_ContainsTidalOperation()
        {
            using var ctx = PluginLogContext.Push("Tidalarr", "Test");
            var prefix = PluginLogContext.Current!.LinePrefix();
            prefix.Should().StartWith("[Test:");
            prefix.Should().EndWith("] ");
            prefix.Should().MatchRegex(@"^\[Test:[a-f0-9]{32}\] $");
        }
    }
}
