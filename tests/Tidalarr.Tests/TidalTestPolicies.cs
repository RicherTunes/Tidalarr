using System.Net;
using Lidarr.Plugin.Common.Services.Download;

namespace Tidalarr.Tests;

/// <summary>
/// R2-02: shared SSRF policy for unit tests that drive <c>TidalChunkDownloader</c> with synthetic, non-resolving
/// segment hosts (e.g. <c>https://bytes</c>, <c>http://test</c>). Production keeps the Strict default
/// (<c>ResolveDns=true</c>); tests inject a deterministic <see cref="RemoteMediaUriPolicy.DnsResolver"/> so those
/// hosts classify as public instead of NXDOMAIN-failing the guard — without weakening the production policy.
/// Mirrors Tidal's real need for <c>AllowHttp=true</c>.
/// </summary>
internal static class TidalTestPolicies
{
    public static RemoteMediaUriPolicy Resolving { get; } = new()
    {
        AllowHttp = true,
        DnsResolver = _ => new[] { IPAddress.Parse("8.8.8.8") },
    };
}
