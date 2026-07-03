# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Tidalarr is a high-performance Lidarr plugin for Tidal streaming service, built using the Lidarr.Plugin.Common shared library architecture. It provides both indexing and download capabilities for high-quality audio content from Tidal.

## Runtime & Docker Image Requirements (CRITICAL)

**Target framework**: `net8.0` — this plugin MUST target .NET 8.

**Lidarr Docker image**: Use ONLY a `.NET 8` plugins-branch image for CI and local testing. The correct tag format is `pr-plugins-3.x.y.z` (net8). Example:

```
LIDARR_DOCKER_VERSION=nightly-3.1.3.4970
```

- Image: `ghcr.io/hotio/lidarr:nightly-3.1.3.4970`

**NEVER use `pr-plugins-2.x` tags** (e.g., `pr-plugins-2.14.2.4786`) — those are .NET 6 images. Loading a .NET 8 plugin into a .NET 6 host causes `System.Runtime` assembly load failures and Lidarr crash-loops (`Could not load file or assembly 'System.Runtime, Version=8.0.0.0'`).

When bumping the Docker image tag, search the entire repo for the old tag string and update all hits (workflows, scripts, docs).

## Plugin Registration (CRITICAL — controls Lidarr System→Plugins UI visibility)

Lidarr has **two** distinct `IPlugin` interfaces, and conflating them silently breaks the System→Plugins UI:

| Interface | From | Used by |
|---|---|---|
| `NzbDrone.Core.Plugins.IPlugin` | `Lidarr.Core.dll` (host) | `/api/v1/system/plugins` — UI listing, update checks, uninstall |
| `Lidarr.Plugin.Abstractions.IPlugin` | Common (internalized via ILRepack) | TestKit `PluginSandbox` — never read by the live host |

`TidalarrPlugin : IPlugin` (Common's IPlugin) satisfies the bridge contract. `TidalIndexer`/`TidalDownloadClient` are discovered through their Lidarr base classes. Neither satisfies the host's `IPlugin`, so without an additional class the plugin loads fully and works but doesn't appear in System→Plugins (and can't be auto-updated/uninstalled through the UI).

`src/Tidalarr/Integration/TidalarrInstalledPlugin.cs` extends the host's `NzbDrone.Core.Plugins.Plugin` to close the gap:

```csharp
public sealed class TidalarrInstalledPlugin : NzbDrone.Core.Plugins.Plugin
{
    public override string Name => "Tidalarr";
    public override string Owner => "RicherTunes";
    public override string GithubUrl => "https://github.com/RicherTunes/Tidalarr";
}
```

DryIoc's `RegisterMany` (in `NzbDrone.Common.Composition.Extensions.AutoAddServices`) auto-discovers this class from the loaded plugin assembly. `InstalledVersion` is derived from `AssemblyInformationalVersionAttribute` via the base class — do **not** hardcode it. Tidalarr.csproj wires the assembly version from the top-level VERSION file (Directory.Build.props), and the `VersionContractTests` enforce that sources stay in sync.

## Release Asset Naming (CRITICAL — controls Lidarr UI install)

**Every release asset filename MUST contain the literal substring `net8.0.zip`.**

Lidarr's plugin install (UI "Install" on a GitHub URL) is implemented in `src/NzbDrone.Core/Plugins/PluginService.cs` on the `plugins` branch. The asset filter is:

```csharp
release.Assets.Any(a => a.Name.Contains($"{Framework}.zip", StringComparison.OrdinalIgnoreCase))
// where Framework = $"net{_platformInfo.Version.Major}.0"  →  "net8.0"
```

If no asset matches, `GetRemotePlugin` returns `null` and `InstallPluginService.Execute` silently no-ops — **the UI spinner spins forever with no error**. This is the failure mode users see as "Install button does nothing."

Other constraints the install enforces:

- `draft: false`
- `target_commitish` ∈ `{main, master}` (case-insensitive)
- Tag parses as a version (`v1.2.3`, `1.2.3`, or `1.2.3-prerelease`)
- Optional `Minimum Lidarr Version: X.Y.Z.W` in release body must be ≤ host version

Our release zip is named `Lidarr.Plugin.Tidalarr-v<VERSION>.net8.0.zip`; `scripts/ci.ps1` copies the canonical `New-PluginPackage` output to that artifact name. Do not rename without keeping the `net8.0.zip` suffix.

**Verify a release is installable:**

```bash
gh api repos/RicherTunes/tidalarr/releases --jq '.[0] | {tag_name, draft, target_commitish, assets: [.assets[].name]}'
```

At least one asset name must contain `net8.0.zip`.

**ALWAYS**:

- Use constants from `TidalConstants.cs` rather than hardcoding.
- Expose to the user what brings value in `TidalDownloadSettings.cs` or `TidalarrSettings.cs`; otherwise, it should be in `TidalConstants.cs`.
- Be aware that this project shares a common library with <http://github.com/RicherTunes/Lidarr.Plugin.Common> so always think of ways to ensure generic code can be shared with this library so other projects may benefits. Think architecturally when doing so.

## Plugin DLL Naming Contract (CRITICAL)

**The main plugin DLL filename MUST match the glob `Lidarr.Plugin.*.dll`.** Lidarr's PluginLoader (`NzbDrone.Common/Extensions/PathExtensions.cs:334`) scans `/config/plugins/{owner}/{name}/` with `Directory.GetFiles(folder, "Lidarr.Plugin.*.dll")` — any other filename is silently ignored. No error, no warning, no log line; the plugin just never appears in `/api/v1/system/plugins`.

For Tidalarr this is satisfied by `<AssemblyName>Lidarr.Plugin.Tidalarr</AssemblyName>` in `src/Tidalarr/Tidalarr.csproj`. Don't drop that line "to clean up" — it's load-bearing.

## Submodule pin coordination (ext-common-sha.txt)

`ext/Lidarr.Plugin.Common` is a git submodule pinned to a specific Common SHA. Two things must always agree on that SHA:

1. **The submodule gitlink** — what `git ls-tree HEAD ext/Lidarr.Plugin.Common` reports (updated by `git add ext/Lidarr.Plugin.Common` after checking out a new Common commit).
2. **`ext-common-sha.txt`** — a plaintext sentinel (40 hex chars + LF) at the repo root. Keep it in sync with the gitlink when bumping Common; local version-contract tests and shared lint gates catch drift.

**Why the sentinel exists**: the gitlink is invisible in a plain `git diff` (it shows only `-Subproject commit <sha>`), so the sentinel makes the pinned version greppable, reviewable in PRs, and assertable in tests (`VersionContractTests` cross-checks it against `plugin.json`'s `commonVersion`). Seeing `ext-common-sha.txt` dirtied in `git status` after a submodule bump is expected — commit it together with the gitlink.

**To bump the pin**: `pwsh ext/Lidarr.Plugin.Common/scripts/repin-common-submodule.sh --sha-from-submodule --stage` (or the `.ps1` variant) reads the submodule HEAD, rewrites `ext-common-sha.txt`, and stages both so they can't drift. Re-pin manually when Common's main advances — there is no scheduled auto-bump workflow on the Gitea-primary copy.

## Common helpers in use

- `PluginConfigRoots.Resolve("Tidalarr")` — `src/Tidalarr/Integration/TidalIndexerSettings.cs:15`, `src/Tidalarr/Integration/TidalarrSettings.cs:16`, `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexerSettings.cs:17`
- `BackendHealthCache` — `src/Tidalarr/Infrastructure/Resilience/TidalBackendHealthHandler.cs:33` (DelegatingHandler wrapping `BackendHealthCache.Shared`)
- `AuthFailureGate` — `src/Tidalarr/Integration/TidalModule.cs` (singleton wrapping the bridge-default `IAuthFailureHandler`, 60s probe interval). Mirrors apple + qobuz; prevents Lidarr's search loop from hammering api.tidal.com on a dead session (qobuzarr-incident class). Per-call entry-point wiring via private static helpers `IsAuthShortCircuited` + `RecordAuthOutcomeFromException` (apple's pattern from `AppleMusicIndexerAdapter.cs:63-104`) in:
  - `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs` — FetchReleases (short-circuit returns empty), Test (short-circuit surfaces a clear "auth needs attention" validation failure), and the inner catch + outer Test catch record failures
  - `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs` — Download (short-circuit throws an actionable InvalidOperationException), Test (validation failure), and inner/outer catches record failures
  - Helpers resolve `AuthFailureGate?` from the runtime's `IServiceProvider` per-call because Lidarr's `HttpIndexerBase` / `DownloadClientBase` ctors are fixed and can't accept additional DI parameters
- `PluginLogContext` — 6 ambient scopes at every canonical entry point:
  - Indexer: `TidalLidarrIndexer.cs:101` (Search, provider="tidal:api"), `TidalLidarrIndexer.cs:201` (Test)
  - Download client: `TidalLidarrDownloadClient.cs:77` (Download), `TidalLidarrDownloadClient.cs:290` (Test)
  - Auth: `TidalOAuthService.cs:96` (OAuthExchange), `TidalOAuthService.cs:137` (OAuthRefresh)
  - Coverage is complete: Tidal has no token-sign path (no JWT signing like Apple's MusicKit) so the apple "auth-token-sign" scope is N/A by design.
- `WarnOnce` — **not adopted; not needed**. A repo-wide grep for hand-rolled warn-once patterns (`HashSet<string>` + `_warned.Contains`-style) returns zero hits in `src/` — there's nothing to migrate. Common's `WarnOnce` remains available; revisit if a hot-loop log site adds a per-iteration warning in the future.
- `HttpExceptionClassifier` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:326` (indexer `Test()` catch) and `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:387` (download client `Test()` catch). Replaces the old `"Test failed ({CLR-type-name}): {ex.Message}"` UX with categorized actionable hints: Auth-class failures route to the `Authentication` validation field so the UI surfaces them in the credential section; Network / RateLimit / Timeout / Server / ClientRequest each get a tailored hint. Matches qobuz's adoption at `src/API/AdaptiveQobuzApiClient.cs:54` + `src/Services/AuthTokenManager.cs:376`.

## Terminal release suppression (stops the permanently-unavailable-track re-grab loop)

A track Tidal has delisted / had its streaming rights removed (surfaced as an HTTP 404 from
`tracks/{id}/playbackinfopostpaywall` for a track that IS on the album's tracklist) can never be
downloaded in any quality tier, and Lidarr's blocklist does not fire for this failure mode — so the
same album is re-grabbed on every scheduled search, forever. Tidal adopts the qobuz-proven fix:

- **Classification (correctness is the whole game).** `TidalStreamRestrictionClassifier.Classify(httpStatus, subStatus, userMessage)` (`Domain/Streaming/`) is a pure function mapping a failed playback-info response to a `TidalStreamUnavailableReason` (`Core/Exceptions/`). **Only `RightsRemoved` (HTTP 404) is PERMANENT**; everything else — auth (401), region/tier (403), not-ready (sub-status 4005), rate-limit (429), server (5xx), network, unknown/empty — is TRANSIENT and never suppressed. The safety bias is absolute: over-suppression permanently hides a recoverable album (a false negative), which is strictly worse than a bounded re-grab loop, so any ambiguous/unrecognized signal defaults to transient. Region/tier is deliberately excluded from permanent (availability can change), matching qobuz's geo decision. `IsPermanent()` is the single source of truth (`TidalStreamUnavailableReasonExtensions`).
- **Throw site.** `TidalApiClient.GetStreamInfoAsync` / `GetPlaybackInfoAsync` call `ThrowIfPermanentlyUnavailableAsync`, which throws a classified `TidalStreamUnavailableException` **only** for a permanent reason; transient failures fall through to the unchanged `EnsureSuccessStatusCode()` path so the auth-failure gate + retry semantics are untouched. (This closed the audit finding that `TidalStreamUnavailableException` had zero throw sites.) `TidalChunkStreamProvider.GetStreamAsync` no longer swallows a permanent exception — it records it and rethrows.
- **Bridge (album id is not in the stream provider's scope).** `TidalTerminalRestrictionScope` (`Domain/Streaming/`) is an ambient `AsyncLocal` collector (same pattern as Common's `DownloadTelemetryContext`). `TidalLidarrDownloadClient.Download`'s `doWork` opens a scope around the album download; the provider records any permanent per-track restriction into it; after a **failed** download, `TidalTerminalSuppressionRecorder.TryRecordAsync` writes the album id to the store (best-effort — a store failure never masks the download failure).
- **Store.** `TidalReleaseSuppressionStore` (`Application/Services/`) is the thin Tidal policy adapter over Common's durable `TerminalReleaseSuppressionStore` (bounded/TTL 30d/disk-persisted, keyed by album id). `ShouldSuppress(reason) = reason.IsPermanent()`. `.Shared` is the process-wide instance for plugin `"Tidalarr"`.
- **Parser withhold + interactive override.** `TidalReleaseSuppressionFilter.Apply(albums, store, isInteractive)` (`Application/Services/`) withholds suppressed albums on AUTOMATIC/RSS searches (this is what stops the loop) but OFFERS them on an INTERACTIVE (user-initiated) search so a user can recover a previously-restricted album without waiting out the TTL. Applied in `TidalLidarrIndexer.FetchReleases` (interactive flag from `TidalLidarrRequestGenerator.IsInteractiveSearch` ← `AlbumSearchCriteria.InteractiveSearch`) and the fallback `TidalLidarrParser.ParseResponse` (which carries no criteria, so treated as automatic — withhold).
- **Completion contract is NOT changed.** Suppression is a pure search-side side effect; an incomplete album still always reports `Failed` to Lidarr with the same message. Guarded by `TidalReleaseSuppressionFilterTests`, `TidalReleaseSuppressionStoreTests`, `TidalStreamRestrictionClassifierTests`, `TidalTerminalRestrictionScopeTests`, `TidalTerminalSuppressionRecorderTests`.
- **Honest caveat.** The permanent trigger (HTTP 404 on playback-info) is a best-effort classification not yet live-validated against every Tidal error shape. It is the ONLY permanent trigger and the recovery paths (interactive search + 30-day TTL) bound the cost of a rare false-permanent. If live data ever shows a transient 404 shape, tighten to additionally require sub-status 2001 — never loosen a transient case to permanent.

## Tidal Favorites import list (first streaming-catalog import list in the ecosystem)

`TidalFavoritesImportList : NzbDrone.Core.ImportLists.ImportListBase<TidalFavoritesImportListSettings>` (`Integration/LidarrNative/`) mirrors the authenticated user's Tidal favorites into Lidarr. It is a **future candidate to generalize into a Common `StreamingImportListBase`** — deliberately kept Tidal-local for now (invest-when-a-second-plugin-needs-it).

- **Discovery.** Inheriting `ImportListBase<T>` is enough for the host's DryIoc `RegisterMany` to auto-construct it (same mechanism as `TidalLidarrIndexer` / `TidalLidarrDownloadClient`; `TidalarrInstalledPlugin` already covers System→Plugins visibility). Ctor takes only the four base host services `(IImportListStatusService, IConfigService, IParsingService, NLog.Logger)`. Because it lives under `Integration/LidarrNative/`, it is compiled out of the host-free CI build by `Directory.Build.props`'s `SkipHostBridge` → `**/LidarrNative/**` `DefaultItemExcludes`, exactly like the indexer/download client.
- **Auth is SHARED — no re-auth.** Settings expose only `ConfigPath` + `Content` (a `TidalFavoritesContent` Select: albums-and-artists / albums-only / artists-only) + `Market`. The runtime is built by `TidalImportListRuntimeCache` (mirrors `TidalIndexerRuntimeCache`, keyed on `ConfigPath`) which registers `TidalModule.RegisterServices`, so the import list reads the **same OAuth token store** the indexer wrote — the session already carries `TidalTokens.UserId` and `TidalConstants.OAUTH_SCOPE` already includes `r_usr`.
- **Paginated favorites API.** `TidalApiClient.GetFavoriteAlbumsAsync` / `GetFavoriteArtistsAsync` (on `ITidalCore`) page `users/{userId}/favorites/{albums,artists}` via the shared private `FetchAllFavoritesAsync<TDto>` helper, unwrapping Tidal's `{ created, item }` envelope (`TidalFavoriteItemDto<T>`). Termination is the just-merged `GetAlbumTracksAsync` pattern: stop on empty page or once `seenEnvelopes >= declaredTotal`, `PagedResponseValidator.Validate(seenEnvelopes, declaredTotal, …)` on shortfall (loud, not silent truncation), no over-fetch (single-page = one request), hard-capped by `TidalConstants.FAVORITES_MAX_PAGES`. Integrity is measured on **envelopes paged** (not unwrapped items) so a rare null inner item can't masquerade as truncation. A session with no `UserId` throws an actionable `InvalidOperationException` **before** any network call.
- **Mapping + host-contract safety.** `TidalFavoritesMapper.Map` → `ImportListItemInfo` (`NzbDrone.Core.Parser.Model`, NOT `NzbDrone.Core.ImportLists` — the type lives in the former namespace): favorite album → `{ Artist = primary artist, Album = title, ReleaseDate }`, favorite artist → `{ Artist = name }`. De-duplicated case-insensitively (space-separated key so `"AB"+"C"` ≠ `"A"+"BC"`), entries missing an essential name dropped. `Fetch()` and `Test()` are sync host contracts driven via `Task.Run(...).GetAwaiter().GetResult()` (indexer's shim) and **never throw** out of the contract — a fetch error logs + returns empty (so the host doesn't clear already-imported items); `Test()` adds an actionable "authenticate the Tidalarr indexer first" `ValidationFailure` when `UserId` is missing. Fetch/auth cores are `internal static` seams (`FetchFavoritesAsync` / `ValidateAuthAsync`) unit-tested against `ITidalCore` / `ITidalAuth` stubs.
- **No Tidal catalog id on the item.** `ImportListItemInfo` is MusicBrainz/name-oriented; there is no Tidal-native id field, so Lidarr resolves favorites by artist/album **name**. This is a host-contract limitation, documented for users.
- **Tests.** Host-free `TidalApiClientFavoritesTests` (pagination/envelope/UserId — re-included after the `Tidal*.cs` remove in the test csproj). Host-coupled `TidalFavoritesImportListTests` (mapping/content-selection/dedup/auth-validation — explicitly `Compile Remove`d under `ExcludeHostBridge=true` because the subfolder path escapes the root-level `Tidal*.cs` glob). Live host: Docker E2E `Plugin_Loads_AppearsInImportListSchema` + `ImportList_Test_WithEmptySettings_ReturnsSensibleFailure` (Common TestKit already exposes the import-list schema/test assertions).
- **Honest caveat.** The favorites endpoint shape (`{ created, item }` envelope, `totalNumberOfItems` pagination) is modeled from Tidal's documented API but not yet live-validated against a real account in this change — flagged for the lidarr-e2e live gate (DryIoc discovery + real-favorites fetch). The `AuthFailureGate` is intentionally NOT wired here (an import list is a low-frequency scheduled call, not the search fan-out loop the gate protects); `GetValidTokensAsync` fast-fails a dead session.

## File ↔ class naming convention

Tidal's `src/Tidalarr/` tree groups types by responsibility (Domain.Streaming, Domain.Api, Integration, Infrastructure, etc.) and prefixes types with `Tidal` so they are unambiguous when grep'd across the five-plugin ecosystem. Multi-class files are allowed for cohesive groupings (DTOs, exception families, attribute annotations); single-class files MUST have the file name match the class name.

| File | Class(es) | Convention |
|------|-----------|------------|
| `Core/Exceptions/TidalExceptions.cs` | `TidalException` + 5 subclasses | Exception family (multi-class OK) |
| `Core/Models/TidalDtos.cs` | 10 DTO records | DTO group (multi-class OK) |
| `Domain/Streaming/TidalChunkDownloader.cs` | `TidalChunkDownloader` + `ChunkDownloadProgress` | Primary matches file; progress is a supporting type (OK) |
| `Domain/Streaming/IAudioProcessor.cs` | `IAudioProcessor` + `SystemAudioProcessor` | Interface + impl pair (OK) |
| `Integration/HostlessAnnotations.cs` | `FieldDefinitionAttribute` + `FieldOptionAttribute` | Attribute grouping (OK per C# convention) |
| `Domain/Streaming/TidalStreamManifest.cs` | `TidalStreamManifest` + `ManifestMimeType` enum | Renamed Wave 21 (was `StreamManifest`) — primary matches file |
| `Domain/Streaming/TidalAudioFormatHandler.cs` | `TidalAudioFormatHandler` | Renamed Wave 21 (was `AudioFormatHandler`) — primary matches file |

- `HostBridgeDownloadTrackerStore` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:38` (static store for in-flight downloads)
- `HostBridgeDownloadOrchestrator` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:39`
- `PrefixedReleaseGuidParser` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:363`
- `PlaceholderSearchUri` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:139`, `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:438`
- `SearchQuerySanitizer` (Common `Services.Intelligence`) — `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs` (`TidalLidarrRequestGenerator.GetSearchRequests` → `SearchQuerySanitizer.BuildPlan(artist, album).Tiers`). Canonical special-character variant generation + combined→artist-only→album-only fallback tiers, consolidating the former local `TidalSearchTermBuilder` (now deleted). Execution is now Common's delegate-only `SearchPlanExecutor` (`Services.Intelligence`), adopted via the thin Lidarr.Core-free `TidalAlbumSearch` adapter (`StopAfterFirstTierWithResults`, `serviceLabel="Tidal search"`, unwraps `TidalSearchResults.Albums`); the former local `TidalTieredAlbumSearch` executor was deleted (its loop mechanics live + are tested in Common). `TidalSearchPlan.Build` is the request generator's single plan-construction entry point so the parity + provenance suites pin the live path. Parity is enforced by `TidalSearchQuerySanitizerParityTests` (subclass of Common's `SearchQuerySanitizerParityTestBase`, 225-case corpus, implementing `BuildPlanViaPlugin`) and search-term provenance by `TidalSearchTermProvenanceTests` (subclass of Common's `SearchTermProvenanceComplianceTestBase`). Chain completeness is gated by `TidalSearchRequestChainTests` (subclass of Common's `SearchRequestChainComplianceTestBase`, TestKit `Compliance`) in `tests/Tidalarr.Tests/Unit/LidarrNative/` — host-free so it runs in the `ExcludeHostBridge=true` CI test build; it drives `TidalSearchPlan.BuildSearchPlaceholderUrls` (the host-free core of `GetSearchRequests`) and asserts the request chain is complete (well-formed placeholder URI, combined-first, only-plan-variants, every variant incl. the artist-only fallback, special-char sanitized). The `LPC0003` analyzer (Common `tools/Analyzers`) is wired into the plugin build to ban HtmlEncode/`DisplayText` on search paths. The critical HTML-encode removal lives in `TidalSearchService.NormalizeSearchTerm` (whitespace-only; the old `Sanitize.DisplayText` encoder corrupted accented/punctuated terms).
- `PathTraversalGuard` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:401`
- `AlbumReleaseInfoBuilder` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:540`, `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs:583`
- `TestValidationBuilder` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs:307`
- `DownloadPathValidator` — `src/Tidalarr/Integration/LidarrNative/TidalLidarrDownloadClient.cs` (Test() pre-check). Wave-31 adoption: syntactic path validation (traversal, relative, invalid chars) before filesystem probe.
- `ILyricsEnricher` / `LyricsEnricher` (Common's shared enricher, wrapping `LrclibClient`) — registered in `src/Tidalarr/Integration/TidalModule.cs:174` (`AddSingleton<ILyricsEnricher>(_ => new LyricsEnricher())`), consumed in `src/Tidalarr/Integration/TidalAudioPostProcessor.cs`. Best-effort synced-lyrics (.lrc) fetch alongside audio downloads via LRCLIB public API. **Consolidated to Common** (lyrics pilot, PR #299/#303): the former local `Application/Services/LyricsEnricher.cs` + `ILyricsEnricher` were deleted in favour of Common's; canonical gating (`SaveSyncedLyrics` master toggle + `UseLRCLIB` LRCLIB-fallback) is enforced by the `Check_UsesCommonLyricsEnricher` parity guard.
- `BoundedConcurrentDictionary<TKey, TValue>` — available (Common v1.15.0+ exposes `ContainsKey`, `Values`, indexer setter, and `IEnumerable<KeyValuePair>` alongside the original v1.10.0 TryAdd/TryGetValue/AddOrUpdate/GetOrAdd surface). No tidal call sites yet — candidates: `PKCEStateStore.InMemoryCache` (`src/Tidalarr/Infrastructure/Storage/PKCEStateStore.cs:33`) is domain-bounded by config-path count so adoption isn't required; revisit when a real growth concern surfaces.

See `ext/Lidarr.Plugin.Common/CHANGELOG.md` for the full catalog and [`docs/ECOSYSTEM_PARITY_MATRIX.md`](ext/Lidarr.Plugin.Common/docs/ECOSYSTEM_PARITY_MATRIX.md) for the historical cross-plugin parity scorecard. The current five-plugin CI contract is enforced by Common's ecosystem CI manifest and shared lint runner.

## Test infrastructure: `bin-tests/` split (cross-ALC type identity)

`tests/Tidalarr.Tests/Tidalarr.Tests.csproj` references `Tidalarr.csproj` with:

```xml
<AdditionalProperties>PluginPackagingDisable=true;OutputPath=bin-tests\;EnablePluginDeployment=false</AdditionalProperties>
```

`PluginPackagingDisable=true` skips the ILRepack merge so the test build keeps `Lidarr.Plugin.Common.dll` + `Lidarr.Plugin.Abstractions.dll` as standalone assemblies. `OutputPath=bin-tests\` redirects that build to `src/Tidalarr/bin-tests/` instead of `src/Tidalarr/bin/`, so the production-merged DLL (the one Lidarr loads in real installs) stays in `bin/` untouched.

**Why the split exists**: tests that pass `Lidarr.Plugin.Common.AuthFailureGate` / `IAuthFailureHandler` / etc. instances need the type identity to match what the test process references. The merged DLL internalizes these — same FQN, different assembly identity — which trips `MissingMethodException` / "doesn't implement IPlugin" runtime errors. The split lets tests work against the un-merged DLL where types resolve to the standalone Common / Abstractions the TestKit references.

**Fixtures that load the DLL** (`PluginSandboxRuntimeTests`, `TidalarrPluginSmokeTests`, future Docker E2E) MUST look in `bin-tests/` first and fall back to `bin/` for legacy builds. Mirrors qobuzarr's pattern; see `tests/Qobuzarr.Tests/Qobuzarr.Tests.csproj:55-60`.

## Build Commands

### **Development Builds (with CLI tools)**

For development work that requires CLI framework dependencies:

```bash
# Development build with CLI framework
dotnet build -p:IncludeCLIFramework=true

# Development build with specific configuration
dotnet build --configuration Debug -p:IncludeCLIFramework=true

# Restore and build for development
dotnet restore && dotnet build -p:IncludeCLIFramework=true
```

### **Production Builds (clean dependencies)**

For production deployments without pre-release CLI dependencies:

```bash
# Production build (default - clean dependencies)
dotnet build

# Production release build
dotnet build --configuration Release

# Production build with explicit CLI exclusion
dotnet build -p:IncludeCLIFramework=false
```

## CLI Framework Architecture

**🎯 Production-First Approach**: Tidalarr uses an opt-in CLI framework strategy for better production deployments and external adoption.

### **Why This Architecture?**

| Aspect | Benefit |
|--------|---------|
| **Development** | CLI tools available with `-p:IncludeCLIFramework=true` flag |
| **Production** | Clean stable dependencies, no pre-release packages |
| **External Adoption** | Other teams get clean library experience |
| **Scalability** | Sustainable architecture for multiple services |

### **How It Works**

1. **Default Behavior**: Development builds include CLI framework (`IncludeCLIFramework=true`)
2. **Production Override**: Use `-p:IncludeCLIFramework=false` for clean production builds
3. **CLI Project**: TidalCLI always includes CLI framework regardless of flag
4. **Conditional Dependencies**: Shared library only includes System.CommandLine/Spectre.Console when flag is enabled

## Project Structure

```
src/
├── Tidalarr/                 # Main plugin (Lidarr.Plugin.Tidalarr.dll)
│   ├── Core/                 # Core models, interfaces, constants
│   ├── Domain/               # API clients, authentication, streaming
│   ├── Infrastructure/       # Caching, performance, storage
│   ├── Integration/          # Lidarr integration (indexer, download client)
│   └── Application/          # Application services
│
TidalCLI/                     # CLI wrapper for testing and development
├── Commands/                 # CLI command implementations  
├── Services/                 # CLI-specific service adapters
└── Program.cs                # CLI entry point

ext/Lidarr.Plugin.Common/     # Shared library (submodule)
```

## Key Components

### **Plugin Architecture (Plugin-First Design)**

- **TidalIndexer**: Implements `BaseStreamingIndexer<TidalarrSettings>` for Lidarr search integration
- **TidalDownloadClient**: Implements `BaseStreamingDownloadClient<TidalDownloadSettings>` for downloads
- **TidalApiClient**: HTTP client using StreamingApiRequestBuilder pattern
- **TidalModelMapper**: Maps between Tidal models and shared library models
- **TidalResponseCache**: Tidal-specific caching extending StreamingResponseCache

### **Tidal-Specific Components (In Plugin)**

- **TidalStreamManifest**: DASH manifest parser for chunk URLs (Tidal-specific XML/MPD format)
- **TidalChunkDownloader**: Sequential chunk download and assembly (Tidal's streaming protocol)
- **TidalAudioFormatHandler**: M4A container with FLAC codec extraction (Tidal's format)
- **TidalQualityMapper**: Maps Lidarr quality to Tidal's AudioQuality enum
- **TidalConcurrentDownloadManager**: Semaphore-controlled album downloads

### **Shared Library Components (In Lidarr.Plugin.Common)**

- **BaseStreamingIndexer/DownloadClient**: Common streaming service patterns
- **StreamingApiRequestBuilder**: HTTP client with OAuth, rate limiting, retries
- **StreamingResponseCache**: Generic caching with TTL and memory management
- **OAuth2PKCEAuthenticationService**: Standard OAuth 2.0 + PKCE flow
- **StreamingModels**: Common models (StreamingTrack, StreamingAlbum, etc.)

### **CLI Architecture (Uses Plugin)**

- **CLI commands invoke plugin methods directly**
- **No business logic in CLI - pure interface layer**
- **CLI focuses on user interaction, plugin handles all streaming logic**

## Development Workflow

### **Plugin-First Development**

```bash
# 1. Clone with submodules
git clone --recursive <repo-url>

# 2. Build plugin first (core functionality)
dotnet build src/Tidalarr/

# 3. Build CLI (thin wrapper using plugin)
dotnet build TidalCLI/ -p:IncludeCLIFramework=true

# 4. Test through CLI (CLI uses plugin methods)
cd TidalCLI
dotnet run -- search "Miles Davis Kind of Blue"
dotnet run -- download-album <album-id>
```

### **Architecture Principle**

- **Plugin**: Contains all business logic, streaming protocols, format handling
- **CLI**: Thin interface layer that calls plugin methods
- **Shared Library**: Common patterns used by multiple streaming services

### **Production Deployment**

```bash
# 1. Clean production build
dotnet build --configuration Release

# 2. Deploy plugin DLL (no CLI dependencies)
cp bin/Release/net8.0/Lidarr.Plugin.Tidalarr.dll /path/to/lidarr/plugins/
```

## Shared Library Integration

Tidalarr integrates with `Lidarr.Plugin.Common` v1.1.0+ for:

- **60-70% code reduction** through shared utilities
- **Standardized authentication** (OAuth 2.0 + PKCE)
- **Unified caching and rate limiting**
- **Common HTTP client patterns**
- **Shared model mapping utilities**

### **Integration Status**

- ✅ **Phase 1**: Critical Infrastructure (inheritance patterns)
- ✅ **Phase 2**: Model Alignment (TidalModelMapper, caching)  
- ✅ **Phase 3**: Basic Service Integration (HTTP, auth)
- ⚠️ **Phase 4-6**: Advanced features (pending model property alignment)

## Configuration

### **Plugin Configuration**

- Configured through Lidarr UI: Settings → Indexers → Add → Tidalarr
- Settings handled by `TidalSettings` extending `BaseStreamingSettings`
- OAuth authentication managed by `TidalOAuthService`

### **OAuth Authorization URL Field (Do Not Remove)**

Tidalarr intentionally exposes an `OAuth Authorization URL` field in the indexer settings:

- **Location**: `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexerSettings.cs`
- **Property**: `OAuthAuthUrl` with `[FieldDefinition(0, ...)]`

**Why it exists**:

- Reduces OAuth setup friction and support/debug time
- Lidarr's UI does not reliably live-update computed fields inside the settings modal after `Test()`. This field exists so users can copy the auth URL without digging through logs, and so we have a reliable “plugin is loaded” signal in `/api/v1/*/schema`.
- The value is derived from `${ConfigPath}/pkce_state.json`. If missing/expired, the getter creates a fresh PKCE state file and returns the new URL (safe for schema rendering: best-effort, no throws).
- The field is intentionally derived/read-only (setter is a no-op)

**Regression history** (DO NOT REPEAT):

- ❌ Removed in `ff0cf39` ("remove non-functional OAuthAuthUrl field")
- ✅ Restored in `2b4225c` ("restore OAuthAuthUrl field with file-based implementation")

**When the field appears empty**:

- The `ConfigPath` is not set or is invalid
- You changed `ConfigPath` but haven’t saved/re-opened the modal yet (Lidarr typically evaluates computed fields when the modal is opened, not live while editing)
- Lidarr may not refresh this computed field inside the modal after clicking `Test()`. If you click Test and immediately need the URL, copy it from the validation error message, then refresh/re-open the settings modal to see the field populated.

**Redirect URL lifecycle (important)**:

- The OAuth Redirect URL is a one-time input used to exchange an auth code for tokens.
- Lidarr persists settings only when the user saves them; plugins cannot reliably mutate the stored Redirect URL value.
- If tokens expire and you see a state mismatch, the stored Redirect URL is stale. You do not need to clear it first; paste the NEW redirect URL from your most recent OAuth login (overwrite) and click Test again.

**When the field is missing entirely** (triage steps):

1. Confirm plugin is loaded: check `/api/v1/indexer/schema` for Tidalarr
2. Check Lidarr logs for plugin load errors
3. Multi-plugin runs can be affected by the upstream Lidarr AssemblyLoadContext lifecycle bug
4. Verify you're running the build with the field restored (`2b4225c` or later)

**Security**: `pkce_state.json` contains a PKCE `code_verifier`; never commit it or include it in logs/artifacts.

### **CLI Configuration**

```bash
# Configure authentication
dotnet run -- config set-auth --client-id your_id --client-secret your_secret

# Configure quality preferences
dotnet run -- config set-quality --preferred Lossless
```

## Testing

**IMPORTANT**: Always use the test runner script to ensure proper build flags:

```powershell
# Run all tests (recommended)
./scripts/test.ps1

# Run with filter
./scripts/test.ps1 -Filter "FullyQualifiedName~TidalApiClient"

# CI mode (excludes HostBridge tests)
./scripts/test.ps1 -ExcludeHostBridge
```

**Why not `dotnet test` directly?**
ILRepack merges dependencies with `Internalize=true`, making types like `IStreamingResponseCache` internal. Tests built without `-p:PluginPackagingDisable=true` will fail with `MissingMethodException`. The test script handles this automatically.

```bash
# Development build tests (with CLI framework)
dotnet test -p:IncludeCLIFramework=true -p:PluginPackagingDisable=true
```

## Troubleshooting

### **Build Issues**

**"System.CommandLine not found"**:

- Solution: Use `-p:IncludeCLIFramework=true` for development builds
- Root cause: CLI framework is opt-in for production-first architecture

**"Missing shared library dependencies"**:

- Solution: Update submodule: `git submodule update --remote`
- Check: `ext/Lidarr.Plugin.Common` is properly synced

### **CLI Issues**

**CLI commands not working**:

- Ensure CLI build: `dotnet build TidalCLI/ -p:IncludeCLIFramework=true`
- Verify CLI project references main plugin correctly

### **Multi-Plugin Co-Existence (FIXED 2026-05-10)**

Previously documented as "upstream Lidarr ALC lifecycle bug" — actually a plugin-side packaging issue. **Root-caused and fixed in common PR #485 + per-plugin host-version alignment.** See `ext/Lidarr.Plugin.Common/docs/dev-guide/ALC_MULTIPLUGIN_FIX.md` for the full retrospective.

**The rule**: every Tidalarr update must keep the merged plugin DLL free of `AssemblyRef`s the Lidarr host doesn't ship. Verify with:

```powershell
$pe = New-Object System.Reflection.PortableExecutable.PEReader([IO.MemoryStream]::new([IO.File]::ReadAllBytes('src/Tidalarr/bin/Lidarr.Plugin.Tidalarr.dll')))
$md = [System.Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($pe)
foreach ($arh in $md.AssemblyReferences) {
  $ar = $md.GetAssemblyReference($arh)
  Write-Host "$($md.GetString($ar.Name)) v$($ar.Version)"
}
```

**Host-pinned versions** (do NOT bump without verifying the host ships the same major version):

| Package | Pin | Lidarr host AssemblyVersion |
|---|---|---|
| `Microsoft.Extensions.DependencyInjection` | 8.0.1 | 8.0.0.0 |
| `Microsoft.Extensions.Logging` | 8.0.1 | 8.0.0.0 |
| `Microsoft.Extensions.Logging.Abstractions` | 8.0.3 | 8.0.0.0 |
| `Microsoft.Extensions.Http` | 8.0.1 | 8.0.0.0 |
| `FluentValidation` | 9.5.4 | 9.0.0.0 |
| `NLog` | 5.4.0 | 5.0.0.0 |

**Verify multi-plugin co-existence locally** when changing pins or packaging:

```powershell
pwsh ext/Lidarr.Plugin.Common/scripts/multi-plugin-coexistence-proof.ps1 -SkipBuild
```

(Spins up one Lidarr container with all locally-built plugins mounted; asserts each appears in `/api/v1/{indexer,downloadclient,importlist}/schema`.)

## Version Management

- Version managed in: `Tidalarr.csproj`
- Shared library version: Tracked via git submodule
- CLI framework: Conditionally included based on build flags

## Architecture Benefits

### **For Development Teams**

- ✅ Full CLI functionality with development flag
- ✅ Same convenience as before
- ✅ No workflow changes needed

### **For Production**

- ✅ Clean stable dependencies
- ✅ No pre-release packages
- ✅ Better deployment reliability
- ✅ Reduced attack surface

### **For External Adoption**

- ✅ Clean library experience by default
- ✅ No CLI baggage for plugin-only users
- ✅ Easier integration for other teams
- ✅ Future-proof scaling

## Contributing

1. **Development builds**: Always use `-p:IncludeCLIFramework=true`
2. **Test production builds**: Verify clean builds work with `-p:IncludeCLIFramework=false`
3. **Document changes**: Update this file if CLI dependencies change
4. **Follow shared library patterns**: Use `BaseStreamingIndexer`, `BaseStreamingDownloadClient`

---

## Quick Reference

```bash
# Development (default with CLI)
dotnet build -p:IncludeCLIFramework=true

# Production (clean dependencies)
dotnet build --configuration Release

# CLI testing
cd TidalCLI && dotnet run -- --help

# Shared library update
git submodule update --remote ext/Lidarr.Plugin.Common
```

This architecture ensures Tidalarr remains developer-friendly while providing production-ready, scalable deployments suitable for enterprise environments and external adoption.

---

## Technical Debt

This section tracks technical debt items that should be addressed but are not blocking current development. Technical debt is automatically prioritized and should never be put under the rug.

### Completed Items

| Item | Priority | Date | Description |
|------|----------|------|-------------|
| Quality Detection Enhancement | MEDIUM | 2025-01-25 | Fixed TidalSearchService to preserve API-detected qualities from audioQuality field; improved TidalApiClient.DetectAlbumQualities parsing |
| Artist ID Plumbing | LOW | 2024-12-XX | Added PrimaryArtistId to TidalTrackInfo and TidalAlbumInfo with fallback to name |
| Silent manifest parse failures | MEDIUM | 2026-05-25 | TidalStreamManifest now logs Warn on ParseStreamData / ParseDashManifest exceptions (previously swallowed silently). |
| T-3: dead settings (external audit) | HIGH | 2026-07-01 | Five `[FieldDefinition]`-exposed settings had no runtime consumer — the value could be changed with zero observable effect. **Removed** (no coherent behavior existed to wire, and Tidal discontinued MQA in 2023): `IncludeMqa`, `ReEncodeAAC` (the latter also wasn't exposed on the live `TidalLidarrDownloadClientSettings` UI class at all — only on the legacy CLI/HostBridge settings surfaces). **Wired** (clear documented intent, straightforward to implement): `EarlyReleaseLimit` → `TidalEarlyReleaseFilter` (excludes albums releasing further than N days out; `TidalLidarrIndexer.FetchReleases` + `TidalLidarrParser.ParseResponse`), `EnableCache`/`CacheDuration` → `TidalResponseCacheFactory` (configures `TidalResponseCache`'s master on/off + search-endpoint TTL; wired at both `TidalModule.cs` cache registrations). Guarded by `tests/Tidalarr.Tests/Documentation/DeadSettingsGuardTests.cs` — reflects over `TidalLidarrIndexerSettings`/`TidalLidarrDownloadClientSettings` for `[FieldDefinition]` properties and fails if any lacks a real (non-copy, non-`nameof`) consumer reference outside a small settings-declaration/mapping-only file allowlist. Portable to the other four plugins by swapping the settings types + allowlist. |

### Pending Items

| Item | Priority | File | Description |
|------|----------|------|-------------|
| None identified | - | - | Tidalarr has relatively clean architecture with good separation of concerns |
| `PerformanceMonitor` / `TidalResponseCache` double-registered | LOW | `src/Tidalarr/Integration/TidalModule.cs` | `RegisterSharedLibraryServices` and `ConfigureServices` each independently register `PerformanceMonitor` (as `AddSingleton<PerformanceMonitor>()` twice) and, pre-T-3, `TidalResponseCache`/`IStreamingResponseCache` — two separate singleton instances of what's conceptually one shared cache/monitor. Found during the T-3 audit; T-3 fixed both `TidalResponseCache` registrations to go through `TidalResponseCacheFactory` so either instance now respects Enable Cache/Cache Duration, but did not collapse the duplicate registrations themselves (out of scope — no observed behavioral bug, just wasted instances). |
| Download tracker items never set `TotalSize` | LOW | `src/Tidalarr/Integration/LidarrNative/` (download tracker) | External audit note: the Lidarr download queue shows progress as `0/0` for Tidal downloads because tracker items never populate `TotalSize`. Not investigated as part of T-3 (out of scope — UI cosmetic, not a dead-setting).|

## Local Verification (Billing-Blocked CI)

When GitHub Actions billing is blocked, run the merge-critical verification pipeline locally:

```bash
pwsh scripts/verify-local.ps1                    # Full pipeline (extract + build + package + closure + E2E)
pwsh scripts/verify-local.ps1 -SkipExtract       # Fast rerun (reuse cached host assemblies)
pwsh scripts/verify-local.ps1 -SkipTests         # Build + packaging closure only
pwsh scripts/verify-local.ps1 -NoRestore         # Skip dotnet restore (fast iteration)
pwsh scripts/verify-local.ps1 -IncludeSmoke      # + Docker smoke test (mounts plugin in Lidarr)
```

**Prerequisites**: PowerShell 7+ (`pwsh`), .NET 8 SDK, Docker (for extract/smoke stages).

The script delegates to `ext/Lidarr.Plugin.Common/scripts/local-ci.ps1`, which orchestrates the same gates as CI: host assembly extraction with .NET 8 + FV 9.5.4 guardrails, plugin packaging via `New-PluginPackage`, and packaging closure validation via `generate-expected-contents.ps1 -Check`.

## Docker E2E Harness (wave 21)

A runnable end-to-end harness boots a real Lidarr container, mounts the merged
Tidalarr plugin DLL, waits for the API, and asserts plugin liveness against the
Lidarr REST API. This is the smoke alarm for "did the plugin actually load
inside the host?" — sandbox tests cannot answer that.

### Run locally

```powershell
# One-shot (builds plugin via verify-local.ps1, then runs the smoke matrix)
pwsh scripts/e2e.ps1

# Re-run without rebuilding (DLL already in src/Tidalarr/bin/)
pwsh scripts/e2e.ps1 -SkipBuild

# Run a single test
pwsh scripts/e2e.ps1 -Filter 'FullyQualifiedName~Indexer_Test'

# Or directly via dotnet (after building)
dotnet test tests/Tidalarr.Tests/Tidalarr.Tests.csproj -c Release \
    -p:PluginPackagingDisable=true --filter "Category=DockerE2E"
```

If Docker Desktop isn't running the tests **skip gracefully** rather than fail —
they're safe to leave in any local test command. CI wiring is out of scope until
wave 22.

### Pinned image

`ghcr.io/hotio/lidarr:nightly-3.1.3.4970` (single-plugin instance on host
port `8690` per the multi-plugin guidance in this file). The tag is sourced
from `scripts/verify-local.ps1`'s `LidarrDockerVersion`. Bump in one place.

### What the smoke tests verify

All tests live in `tests/Tidalarr.Tests/Runtime/` and share one container via
`LidarrContainerFixture` (xUnit collection fixture, single startup per run):

| Test | Asserts |
|------|---------|
| `Plugin_Loads_AppearsInIndexerSchema` | `GET /api/v1/indexer/schema` lists Tidal |
| `Plugin_Loads_AppearsInDownloadClientSchema` | `GET /api/v1/downloadclient/schema` lists Tidal |
| `Indexer_Test_WithEmptySettings_ReturnsSensibleFailure` | `POST /api/v1/indexer/test` returns non-5xx (validation failure, not plugin-load failure) |
| `DownloadClient_Test_WithEmptySettings_ReturnsSensibleFailure` | `POST /api/v1/downloadclient/test` returns non-5xx |
| `Plugin_Loads_In_Real_Lidarr_Container` (`Category=Docker`, legacy) | wave-12 schema check, retained for backwards compat |

Acceptance criterion for the Test endpoints: **anything below 500**. A genuine
plugin-load failure (missing types, bad assemblies, ALC issues) shows up as a
500 InternalServerError. A 4xx with `[ { "errorMessage": "..." } ]` body is
expected — there's no real Tidal account.

### Adding a new smoke test

1. Add a new `[SkippableFact] [Trait("Category","DockerE2E")]` method to
   `DockerE2ETests.cs`, decorated with `[Collection(LidarrContainerCollection.Name)]`
   on the class so it shares the fixture.
2. Skip-guard with `Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason);`
3. Use `_fixture.Http`, `_fixture.BaseUrl`, `_fixture.ApiKey` to talk to Lidarr.
   Call `_fixture.GetContainerLogs()` in failure messages so a CI rerun in
   another timezone can still tell you what blew up.

### Extending the harness to other plugins (wave 22a — done)

As of wave 22a the orchestrator (container lifecycle, healthcheck, log
capture, skip-when-no-Docker) lives in
`Lidarr.Plugin.Common.TestKit.Hosting.LidarrContainerFixture`. Each plugin
provides only the per-plugin glue:

- **`tests/<Plugin>.Tests/Runtime/LidarrContainerFixture.cs`** — subclass
  common's fixture and pass a `LidarrContainerOptions` record with the
  per-plugin knobs: `DockerImage`, `ContainerName`, `LidarrPort`,
  `PluginMountPath` (e.g. `/config/plugins/<Owner>/<PluginName>`),
  `PluginDllFileName`, a `FindPluginDll(repoRoot)` resolver, and a
  `PluginEntrySubstring` ("Tidal", "Qobuz", "AppleMusic", "Brainarr"). Define
  the xUnit `[CollectionDefinition]` next to it.
- **`tests/<Plugin>.Tests/Runtime/DockerE2ETests.cs`** — `[SkippableFact]`s
  that delegate to the smoke-assertion extension methods on the fixture
  (`AssertPluginAppearsInIndexerSchemaAsync`,
  `AssertPluginAppearsInDownloadClientSchemaAsync`,
  `AssertIndexerTestReturnsSensibleFailureAsync`,
  `AssertDownloadClientTestReturnsSensibleFailureAsync`).
- **`scripts/e2e.ps1`** — copy verbatim, adjust `verify-local.ps1` integration
  if that plugin's CI runner differs.

Wave 22b will use this to add Docker E2E to applemusicarr / qobuzarr /
brainarr — the per-plugin glue is ~30 lines.

## Flaky Tests Policy

**Flaky tests are priority tech debt that must be paid immediately.** A test that passes sometimes and fails sometimes erodes trust in the entire test suite. When a flaky test is discovered:

1. **Fix it before starting new feature work** — flaky tests block reliable CI
2. **Document the root cause** in a commit message so the pattern is not repeated
3. **Never skip or disable** a flaky test without a tracking issue

### Known Flaky Tests (Tidalarr)

| Test | Root Cause | Fix |
|------|-----------|-----|
| `HostVersionCouplingTests.DirectoryPackagesProps_Should_Match_HostVersions_For_Coupled_Dependencies` | Test reads FluentValidation.dll from `ext/Lidarr/_output` which may not exist in all dev environments (Docker-only assembly) | Guard with `Skip` when assembly directory is missing, or document required setup |

## Ecosystem consolidation & parity discipline

This plugin is one of five copy-paste-adjacent Lidarr streaming plugins (amazonmusicarr, applemusicarr,
tidalarr, qobuzarr, brainarr) sharing `Lidarr.Plugin.Common`. **Every bug here is likely a bug class** present
in the sibling plugins too. Before shipping a fix to any shared-surface concern (auth/retry, rate-limit /
Retry-After, catalog→ReleaseInfo field mapping, path/SSRF guards, token store, pagination, date/number
parsing): **sweep the other plugins + Common for the same pattern, fix every instance, and push shared logic
down into Common** (plugins adopt it via a thin DI subclass; the out-of-tree DRM seam stays plugin-owned +
public — never consolidated, because ILRepack internalizes Common in the merged DLL). Common changes go via an
**isolated-worktree PR from origin/main**, must re-pin `ext-common-sha.txt`, and must keep this plugin's parity
tests green (the parity matrix is a contract). Verify the actual mechanism before assuming a class sweeps —
raw-JSON alias-probing plugins and typed-DTO plugins are vulnerable to different bug classes.

**Canonical rules:** `ext/Lidarr.Plugin.Common/AGENTS.md` → "Ecosystem Consolidation & Parity Discipline".
