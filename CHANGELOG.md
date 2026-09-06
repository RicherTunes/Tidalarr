<!-- docval:ignore-workflow-refs — this file is an append-only historical record; workflow paths named in past entries may no longer exist -->

# Changelog

All notable changes to Tidalarr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Changed

- Re-pin `ext/Lidarr.Plugin.Common` to `d4a0850961948fbd165578df9ca19b7cd1de3781`; `commonVersion`: `1.18.0-dev` -> `1.18.0`.
- Adopt shared cancellation-safe concurrency leases and operation-timeout ownership: cancelled waits and redirects release only owned permits, caller cancellation propagates, and supplied clocks drive timeout expiry.
- Adopt the Common release containing monotonic retry budgets and overflow-safe exponential backoff; keep Tidal-specific request adapters unchanged.
- Declare minimum Lidarr `3.1.3.4970` to match the compiled host references. Shared packaging and smoke gates reject understated host requirements.
- Adopt shared retry-budget hardening, authoritative package-version evaluation and ZIP identity checks, and one role-aware Docker smoke runner with run-owned cleanup.
- Enforce the existing warning budget using shared unique-diagnostic accounting; repeated build output is not new debt and unrepresented summary warnings remain charged.

## [1.3.0] - 2026-07-17

### Changed (chore: Common repin + dependency CVE gate, 2026-07-16)

- **`ext/Lidarr.Plugin.Common` repinned** `12dd294` → `b4b3145` (`ext-common-sha.txt` updated in lockstep). Picks up: OAuth refresh cancellation/timeout hardening in `OAuthStreamingAuthenticationService` (tidal's `TidalOAuthService` overrides the legacy `RefreshTokensInternalAsync(string)` seam, which still works unchanged; the new cancellable overload can be adopted later — no action required now), the shared dependency-CVE scan gate (`ext/Lidarr.Plugin.Common/scripts/ci/check-vulnerable-packages.ps1`), a `System.Security.Cryptography.Xml` 8.0.3 transitive floor in Common, queue-v2 opt-in groundwork, and removal of the dead `TokenDelegatingHandler` + `HostConcurrencyGate` helpers (zero tidalarr references, verified).
- **CI gains a `dependency-scan` job** (`.gitea/workflows/ci.yml` + guarded GitHub mirror): runs Common's `check-vulnerable-packages.ps1 -Target Tidalarr.sln`, failing the build on any High/Critical known-vulnerable direct or transitive NuGet package. Local scan at the new pin: clean — no floors needed.

### Changed (D-12: one runtime/ServiceProvider per credential set, 2026-07-16)

- **The three per-host-class runtime caches collapsed into a single `TidalRuntimeCache`.** `TidalIndexerRuntimeCache`, `TidalDownloadClientRuntimeCache`, and `TidalImportListRuntimeCache` each built their OWN `ServiceProvider` — and therefore their own singleton `TidalOAuthService` — over the same rotating-refresh-token file (`tidal_tokens.json`). T-2's cross-instance rotation clobber was mitigated in-service (`RefreshOrClearOnRevokedAsync` re-reads the store; that defense stays), but the structural fix is one provider per credential set: indexer + download client + import list now resolve the SAME `TidalOAuthService`, so refresh single-flight genuinely covers all three. The merged cache is keyed on **ConfigPath only** (the token file's home). The old indexer key's `|RedirectUrl` component was dropped deliberately: RedirectUrl is a one-time OAuth *input*, not credential state — the code exchange reads the pasted URL from host settings per call, and the provider-side `TidalCredentials` value is ignored by `TidalAuthTokenAuthAdapter`. Its two incidental effects are preserved explicitly: the paste flows as in-place sub-state on the registered settings singletons (`TidalRuntime.ApplyIndexerSettings`), and the indexer clears the token manager's in-memory session after a successful exchange. The download orchestrator is now lazy (created only when the download client first needs it), per-side settings (quality/concurrency/lyrics/market) are refreshed in place on every resolve, and provider disposal semantics (graveyard + `DisposeAsync` tearing down HttpClient chains) are unchanged and now pinned. (`TidalRuntimeCacheTests`: share/join/distinct-paths/RedirectUrl-reuse/sub-state/disposal; RED evidence — pre-collapse, indexer and download client resolved two distinct `TidalOAuthService` instances over one ConfigPath.)

### Fixed (hardening batch — silent seams + queue 0/0, 2026-07-16)

- **T-5: token refresh failures are no longer silent.** `ManagedTokenProvider.RefreshTokenCoreAsync` swallowed every refresh failure with a bare `catch { return string.Empty; }` (plus a silent inner catch on the post-OAuth re-prime), so auth could degrade with zero log signal. The provider now takes an optional `ILogger<ManagedTokenProvider>` and logs a redacted WARNING on refresh failure (exception type + `Sanitize.SafeErrorMessage`-scrubbed reason — no token material) and a DEBUG on a failed best-effort re-prime. The return-value contract (empty token, never a throw) is unchanged. (`ManagedTokenProviderRefreshLoggingTests`)
- **D-5: failed delete-data removals are no longer silent.** `TidalDownloadRemovalCoordinator.Remove` called `HostBridgeDownloadTrackerStore.Remove` without the `onDeleteError` callback Common supports, so a failed `deleteData` directory delete (locked file, permissions, AV scanner) vanished and orphaned data piled up. The coordinator now forwards an optional `onDeleteError` callback and `TidalLidarrDownloadClient.RemoveItem` wires it to a WARN log with the download id. The tracker entry is still removed either way (the queue must clear). (`TidalDownloadRemovalCoordinatorDeleteErrorTests`)
- **Queue no longer shows 0/0 for Tidal downloads.** Tracker items created by `TidalLidarrDownloadClient.Download` never set `TotalSize`, so `ProjectDownloadItems` derived `RemainingSize` from 0 and Lidarr's queue showed "0/0" for every download regardless of progress. The item factory (extracted as the testable `CreateTrackedDownloadItem` seam) now seeds `TotalSize` from the grabbed release's per-quality size estimate (`ReleaseInfo.Size`, computed by the indexer via Common's `AlbumSizeEstimator`), so the queue derives remaining bytes from progress. Closes the long-standing "Download tracker items never set `TotalSize`" tech-debt entry. (`TidalLidarrDownloadClientTotalSizeTests`)
- **`PerformanceMonitor` double registration collapsed.** `RegisterSharedLibraryServices` and `ConfigureServices` each called `AddSingleton<PerformanceMonitor>()`, leaving two descriptors of one conceptual singleton (an `IEnumerable<PerformanceMonitor>` consumer or decorator would see two instances). Only the `RegisterSharedLibraryServices` registration remains. (`TidalModuleDiTests.PerformanceMonitor_IsRegisteredExactlyOnce`)

### Docs (2026-07-16)

- README gains a **Terminal release suppression** section documenting the suppression behavior shipped 2026-07-02 (permanent unavailability → withheld from automatic/RSS searches; interactive search recovers; transient failures never suppressed).

### Fixed (api — 429-exhaustion rate-limit reporting on every endpoint, 2026-07-10)

- **A persistent 429 now feeds `IRateLimitReporter` on EVERY `TidalApiClient` endpoint, not just playback-info.** `GetStreamInfoAsync` (and the same shape in `GetTrackAsync`, `GetAlbumAsync`, `GetAlbumTracksAsync`, `SearchAsync`, and the favorites pager behind `GetFavoriteAlbumsAsync`/`GetFavoriteArtistsAsync`) had a dead 429-report branch: Common's `ExecuteWithRetryAsync` disposes throttled responses and surfaces retry exhaustion as an `HttpRequestException`, so the post-call `ReportRateLimitStatusAsync(response)` never saw an exhausted 429 — the reporter stayed blind exactly while Tidal was actively throttling. The `GetPlaybackInfoAsync` fix (filtered `catch` on `HttpStatusCode.TooManyRequests` reporting the conservative 60s default, then rethrowing) is now a single private helper, `ExecuteWithRetryReporting429Async`, that every endpoint routes through — including `GetPlaybackInfoAsync` itself, whose inline copy collapsed into it. (`TidalApiClientStreamInfoHardeningTests` pins all six endpoint entry points)

### Fixed (download — chunk-provider hardening, 2026-07-10)

- **Cancellation now propagates into manifest resolution.** `TidalStreamService.GetStreamInfoAsync` / `GetParsedManifestAsync` accept a `CancellationToken` (default-valued, so existing callers keep compiling) and forward it to `TidalApiClient`'s playback-info/stream-info calls; `TidalChunkStreamProvider.GetStreamAsync` passes the caller's token instead of dropping it, so cancelling a download aborts the manifest fetch promptly instead of letting it complete. (`TidalChunkStreamProviderTests`)
- **Cancellation is no longer swallowed into the legacy fallback.** `TidalChunkStreamProvider`'s unfiltered `catch` treated `OperationCanceledException` as a transient manifest failure and re-resolved through the legacy stream-info path (double API load, delayed cancel); the catch now filters `when (ex is not OperationCanceledException)` so cancellation propagates. The permanent-restriction catch above it is unchanged.
- **`TidalApiClient.GetPlaybackInfoAsync` hardened to sibling parity.** It used a bare `SendAsync`; it is now routed through the same helpers as `GetStreamInfoAsync`: `ExecuteWithRetryAsync` (a transient 5xx/408 is retried to success instead of failing the track), `ReportRateLimitStatusAsync`, and `LogApiCallStarted`/`LogApiCallCompleted` — plus a 429-retry-exhaustion report so persistent rate limiting actually feeds `IRateLimitReporter` (the retry helper disposes throttled responses, so a 429 previously never reached the reporter). Permanent-restriction classification keeps its order: HTTP 404 is not a retryable status, so a permanently-unavailable track still throws `TidalStreamUnavailableException` on the FIRST attempt without any retry, before `EnsureSuccessStatusCode`. (`TidalApiClientPlaybackInfoHardeningTests`)
- **`TidalChunkStreamProvider` gains its first direct unit tests**, additionally pinning the terminal-suppression contract (a permanent `TidalStreamUnavailableException` is recorded to `TidalTerminalRestrictionScope` AND rethrown — never hidden by the legacy fallback), chunk-vs-legacy path selection (manifest-with-chunks takes the chunk path; a chunkless manifest falls back to legacy stream info), and the transient-manifest-failure fallback running exactly once.

### Added (download — payload validation, 2026-07-10)

- **Non-audio payloads can no longer reach Lidarr's import as fake audio files.** A CDN/API error body served as HTTP 200 (an HTML soft-404, a JSON problem document) used to land on disk as a `.m4a`/`.flac` and be handed to import — the failure class qobuz hit live. `TidalDownloadOrchestrator` (new, `Application/Services/`) subclasses Common's `SimpleDownloadOrchestrator` and wires its new `ValidateDownloadedPayload` seam to Common's canonical `DownloadPayloadValidator` (text/HTML/JSON detection + audio magic bytes: fLaC, ftyp/M4A, OggS, RIFF, ID3). A rejected payload fails just that track — deleted from disk, feeding the existing AlbumCompletionPolicy incomplete⇒Failed contract. Runs with the FINAL path after post-processing on every download path (album loop and direct track downloads, both the chunk-provider and URL engines). `TidalModule.CreateOrchestrator` now returns it, still passing `metadataApplier: null` so Common's ISRC-writing default applier stays active. (`TidalDownloadOrchestratorTests`)

### Fixed (dev tooling, 2026-07-10)

- `scripts/test.ps1` now builds with `-m:1 -p:UseSharedCompilation=false` — the CLI/tests/plugin projects all reference the Common submodule's Abstractions project, and parallel msbuild nodes raced on its output (`CS2012 cannot open ... for writing`), intermittently failing local test builds. Same fix class the CI workflows already carry.

### Dependencies (2026-07-10)

- `ext/Lidarr.Plugin.Common` submodule re-pinned to **`12dd294`** (`commonVersion` **`1.18.0-dev`**) — Common main's 416/containment/LRCLIB hardening merge: 416 resume clean-restart, album-root containment on the naming seam, LRCLIB artist verification — no plugin source changes required. `ext-common-sha.txt` matches the checked-out submodule HEAD.
- `ext/Lidarr.Plugin.Common` submodule re-pinned to **`d3cc1c3`** (`commonVersion` **`1.18.0-dev`**) — brings the `SimpleDownloadOrchestrator` naming + payload-validation extension seams (`BuildTrackOutputPath` / `ValidateDownloadedPayload`) the adoption above builds on, plus their thread-safety documentation and OCE-contract/telemetry test pins. `ext-common-sha.txt` matches the checked-out submodule HEAD.

### Dependencies (2026-07-03)

- `ext/Lidarr.Plugin.Common` submodule re-pinned to **`a5e9dca`** (`commonVersion` **`1.18.0-dev`**) so tidalarr stays on the current Common mainline after the redirect-target DNS/303 SSRF hardening. No tidalarr source changes required: the Tidal API, OAuth, orchestrator, and chunk downloader clients already disable automatic redirects, so Common validates handled media redirects before the next-hop request and keeps DNS-resolution failures retryable while hard-blocking private/unsafe targets.

### Added (import list — Tidal Favorites, 2026-07-03)

- **Tidal Favorites import list** — the first streaming-catalog import list in the ecosystem. `TidalFavoritesImportList : ImportListBase<TidalFavoritesImportListSettings>` (DryIoc-discovered like the indexer/download client) mirrors the authenticated user's Tidal library into Lidarr: favorite albums (as artist + album entries) and/or favorite artists (as artist entries), selectable via a **Favorites To Import** dropdown (albums and artists / albums only / artists only). Appears in Settings → Import Lists → Add → Tidalarr Favorites.
- **No re-auth.** Authentication is shared with the Tidalarr indexer: the import list reads the OAuth token from the same `ConfigPath` token store (the session carries `TidalTokens.UserId`, and `TidalConstants.OAUTH_SCOPE` already includes `r_usr`), so once the indexer is authenticated the import list needs no separate login.
- **Paginated favorites API.** `TidalApiClient.GetFavoriteAlbumsAsync` / `GetFavoriteArtistsAsync` page `users/{userId}/favorites/{albums,artists}` (limit/offset + declared `totalNumberOfItems`), unwrapping Tidal's `{ created, item }` envelope. Pagination reuses the just-merged integrity approach: it terminates on an empty page or once every declared item is collected, fails loudly (`PagedResponseValidator` / `PagedResponseIntegrityException`) rather than silently truncating if a page stalls, does not over-fetch (single-page libraries issue exactly one request), and is hard-capped (`FAVORITES_MAX_PAGES`) so a misreporting server can never loop unbounded. A session with no `UserId` throws an actionable error before any network call (`TidalApiClientFavoritesTests`).
- **Actionable Test().** `Test()` validates the session has a real Tidal user id and surfaces a clear "authenticate the Tidalarr indexer first" `ValidationFailure` (never throws) when it doesn't. `Fetch()` maps favorites to `ImportListItemInfo`, de-duplicates case-insensitively, drops entries missing an essential name, and never throws out of the host contract (a fetch error logs and returns empty rather than clearing previously-imported items). Guarded by `TidalFavoritesImportListTests` (mapping, content selection, dedup, auth validation) and, for the live host, the Docker E2E `Plugin_Loads_AppearsInImportListSchema` / `ImportList_Test_WithEmptySettings_ReturnsSensibleFailure` smoke tests.

### Added (download — terminal-release suppression, 2026-07-02)

- **Permanently-unavailable tracks no longer re-grab-loop.** When Tidal has no deliverable playable asset for a track that is still listed on an album (rights removed / catalog delisting — surfaced as an HTTP 404 from `tracks/{id}/playbackinfopostpaywall`), no quality tier of that album can ever complete the grab, and Lidarr's blocklist does not fire for this failure mode — so a scheduled search re-grabs the same album forever. Common's `TerminalReleaseSuppressionStore` (bounded, TTL'd, disk-persisted) now records the album id on such a terminal deficit, and `TidalLidarrIndexer.FetchReleases` / `TidalLidarrParser.ParseResponse` withhold every `ReleaseInfo` for a suppressed album id on automatic/RSS searches so the next scheduled search has nothing left to re-grab (`TidalReleaseSuppressionFilterTests`, `TidalReleaseSuppressionStoreTests`).
- **User recovery: interactive search bypasses suppression.** An explicit user-initiated (interactive) search offers a suppressed album immediately instead of waiting out the 30-day TTL; automatic/RSS searches keep respecting suppression so the override can't reopen the loop. The interactive flag is threaded from `AlbumSearchCriteria.InteractiveSearch` via `TidalLidarrRequestGenerator.IsInteractiveSearch`.
- **Classified stream-unavailable throw site (audit gap closed).** `TidalStreamUnavailableException` previously had ZERO throw sites. `TidalApiClient.GetStreamInfoAsync` / `GetPlaybackInfoAsync` now classify a failed playback-info response via the new pure `TidalStreamRestrictionClassifier` and throw a classified `TidalStreamUnavailableException` **only** when the reason is permanent (`RightsRemoved`). Every transient failure (auth 401, region/tier 403, not-ready sub-status 4005, rate-limit 429, server 5xx, network, unknown/empty) is left to the existing `EnsureSuccessStatusCode()` path so the auth-failure gate and retry semantics are byte-for-byte unchanged. **Classification errs TRANSIENT whenever ambiguous** — mis-suppressing a recoverable album (false negative) is strictly worse than a bounded re-grab loop, so `RightsRemoved` is the sole permanent reason (region/tier is deliberately excluded, matching qobuz's geo decision).
- **Completion contract unchanged.** Suppression is a pure search-side side effect: the download client's report to Lidarr (`Failed`, same failure message) is untouched — an incomplete album still always reports `Failed` (`TidalLidarrDownloadClientGetItemsTests.ProjectDownloadItems_FailedAlbum_StillReportsFailed`). The permanent per-track restriction is bridged from the stream provider (which has no album id in scope) to the download client via the `TidalTerminalRestrictionScope` ambient `AsyncLocal` collector (same pattern as Common's `DownloadTelemetryContext`); `TidalTerminalSuppressionRecorder.TryRecordAsync` records the album id only after a failed download that observed a permanent restriction, best-effort (a store failure never masks the original download failure).

### Build / cleanup

- `.gitignore` extended with `*.net8.0.zip`, `package-release/`, `release-notes.md` so release-build artifacts no longer pollute the working tree.

### Added

- `AuthFailureGate` singleton registered in `TidalModule` — wraps the bridge-default `IAuthFailureHandler` registered by `AddBridgeDefaults()` so the indexer, download client, and OAuth service share one latch state. Mirrors apple + qobuz adoption (`AppleMusicarrStreamingPlugin.cs:130-134`, `QobuzarrStreamingPlugin.cs:36`). Closes the long-standing comment-only reference at `TidalModule.cs:59` ("independent of AuthFailureGate") that left Lidarr's search loop free to hammer `api.tidal.com` on a dead session — the qobuzarr-incident class where a user got IP-banned after auth expired.
- Per-entry-point gate wiring in `TidalLidarrIndexer` + `TidalLidarrDownloadClient` via private static helpers (`IsAuthShortCircuited` + `RecordAuthOutcomeFromException` + `LooksLikeAuthFailure`) that mirror apple's `AppleMusicIndexerAdapter.cs:63-104` pattern. The helpers resolve `AuthFailureGate?` from the runtime's `IServiceProvider` per-call because Lidarr's `HttpIndexerBase` / `DownloadClientBase` ctor signatures are fixed and can't accept additional DI parameters.

### Fixed

- `TidalStreamManifest`: parse failures now emit Warn log entries (was silent swallow) for manifest format drift visibility.

### Changed

- `TidalLidarrIndexer.FetchReleases` short-circuits and returns empty when the gate is latched bad and no probe slot is available — search results are deterministic instead of generating 401-storm log noise.
- `TidalLidarrIndexer.Test` short-circuits with an actionable "auth latched bad" validation failure when the gate has no probe slot — the user sees a clear "paste a fresh redirect URL" path instead of a generic timeout.
- `TidalLidarrDownloadClient.Download` throws an actionable `InvalidOperationException` when the gate is latched bad — Lidarr surfaces this as a download failure with recovery instructions instead of starting a download that will burn API quota / risk IP-ban.
- `TidalLidarrDownloadClient.Test` mirrors the indexer's gate-aware behavior.
- All four entry-point catch blocks now call `RecordAuthOutcomeFromException` so that 401/403 failures latch the gate for subsequent calls.

### Tests

- `AuthFailureGateAdoptionTests` (8 facts):
  - 4 DI-registration facts inspect `IServiceCollection` by type `FullName` rather than `GetRequiredService<T>()` because the merged Tidalarr.dll's ILRepack-internalized `Lidarr.Plugin.Common` and `Lidarr.Plugin.Abstractions` copies share an FQN with the standalone references the test project uses but have a different assembly identity — direct `typeof(T)` lookup would miss the registration.
  - 4 wiring facts use reflection on the merged DLL to verify `TidalLidarrIndexer` + `TidalLidarrDownloadClient` define the private static helpers (`IsAuthShortCircuited`, `RecordAuthOutcomeFromException`) — defense against a future refactor silently removing them.

### Known limitations

- Behavior-level testing (latching the gate and observing indexer short-circuit through real method calls) is blocked by the same cross-ALC issue that breaks the existing `BackendHealthCacheAdoptionTests` (6 pre-existing failures): the test project's standalone `Lidarr.Plugin.Common` / `Lidarr.Plugin.Abstractions` types can't be passed across the merged DLL's internalized boundary. The proper fix is a `bin-tests/` split (qobuzarr-style) where the test project consumes an un-merged Tidalarr.dll — tracked as a separate parity gap.

### Documentation

- CLAUDE.md `## Common helpers in use` section gains `PluginLogContext` (6 confirmed scopes at every canonical entry point: Search, indexer Test, Download, downloadclient Test, OAuthExchange, OAuthRefresh — Tidal has no token-sign path so the apple "auth-token-sign" scope is N/A by design) and `WarnOnce` (documented as not adopted-because-not-needed; repo-wide grep for hand-rolled warn-once patterns returns zero hits). Closes the audit gap of "PluginLogContext partial" and "WarnOnce missing" — both resolve as full coverage / N/A by lack of need.

### Changed (UX — Test() failure messages)

- `TidalLidarrIndexer.Test()` and `TidalLidarrDownloadClient.Test()` catch blocks now route exceptions through `HttpExceptionClassifier` (Common) → categorize as Auth / Network / Timeout / RateLimit / ClientRequest / Server and emit a tailored hint instead of `"Test failed ({CLR-type-name}): {ex.Message}"`. Auth-class failures now surface in the `Authentication` validation field (UI credential section) rather than the generic `Test` bucket. Matches qobuz's adoption pattern at `src/API/AdaptiveQobuzApiClient.cs:54` + `src/Services/AuthTokenManager.cs:376`.

### Changed (parity — class naming)

- `TidalConstants.cs` gains the canonical `PluginName` / `ServiceName` / `PluginVendor` const block matching apple + qobuz convention. Cosmetic parity; no behavior change.
- Class `StreamManifest` → `TidalStreamManifest` (file already `TidalStreamManifest.cs`). Brings the class name in line with peer files in `Tidalarr.Domain.Streaming.*` namespace. References updated across 6 source/test/CLI files; 141 affected tests still green.
- Class `AudioFormatHandler` → `TidalAudioFormatHandler` (file already `TidalAudioFormatHandler.cs`). Same rationale + pattern as the `StreamManifest` rename. References updated across 5 source/test/CLI files.

### Changed (test infrastructure — `bin-tests/` split)

- Test `<ProjectReference>` to `Tidalarr.csproj` now passes `OutputPath=bin-tests\;EnablePluginDeployment=false` alongside the existing `PluginPackagingDisable=true`. The test build now writes an un-merged `Lidarr.Plugin.Tidalarr.dll` (plus standalone `Lidarr.Plugin.Common.dll` + `Lidarr.Plugin.Abstractions.dll`) to `src/Tidalarr/bin-tests/` instead of clobbering the production-merged DLL in `src/Tidalarr/bin/`. Matches qobuzarr's pattern at `tests/Qobuzarr.Tests/Qobuzarr.Tests.csproj:55-60`.
- `PluginSandboxRuntimeTests.FindPluginDll` and `TidalarrPluginLoadFixture.InitializeAsync` updated to look in `bin-tests/` first, falling back to `bin/` for legacy/manual builds.
- `.gitignore` adds `bin-tests/` to the ignore list (matches qobuzarr).
- **Net effect**: 11 previously-failing tests are now green — 6 in `BackendHealthCacheAdoptionTests` (cross-ALC type identity), 4 in `PluginSandboxRuntimeTests` (IPlugin discovery), 1 in `TidalarrPluginSmokeTests` (service resolution). Full suite: 1309 passed / 0 failed / 14 skipped. Closes parity-matrix axis #12 (`bin-tests/` split for cross-ALC type identity).

### Changed (CI — Wave-23)

- `.github/workflows/codeql.yml` + `release.yml`: Docker image pin `ghcr.io/hotio/lidarr:pr-plugins` → `pr-plugins-3.1.2.4913` matching apple+brainarr. Floating tag risk was "works today, breaks silently tomorrow" if hotio cuts a `pr-plugins` rebuild for Lidarr 3.1.3.

## [1.2.9] - 2026-05-29

### Added

- Lyrics enrichment via Common's `LrclibClient` — synced-lyrics (.lrc) fetched alongside audio downloads through LRCLIB public API.
- `DownloadPathValidator` adopted in download client `Test()` — syntactic path validation (traversal, relative, invalid chars) before filesystem probe.
- `GetAlbumWithTracksAsync` now delegates to API client to load tracks — fixes albums without preloaded track data.
- MIT LICENSE file added.

### Fixed

- **Search total failure now surfaces clearly** instead of returning misleading empty result — indexer distinguishes between "no results" and "API failed" states.
- **Single-flight token refresh TOCTOU race** — `GetValidTokensAsync` now uses proper single-flight pattern to prevent concurrent refresh attempts.
- **Empty chunk-URL arrays rejected** in `TidalChunkDownloader` — prevents crashes from malformed manifests (TDD).
- **HttpClient leak** — `TidalIndexer` now implements `IDisposable` to properly dispose HTTP client.
- **CancellationToken threaded through** `ValidateChunkAccessibilityAsync` — proper cancellation propagation.
- **InfoUrl security** — album.Id is now URL-encoded to prevent injection (backported from qobuz c9a1574).
- **Deadlock prevention** — sync-over-async migration wrapped in `Task.Run` to prevent UI deadlocks.
- **Performance** — eliminated `Enum.ToString()` allocations in per-album/per-track hot paths.
- **Async efficiency** — added `ConfigureAwait(false)` to 107 bare awaits across 6 plugin files.

### Changed

- **AuthFailureGate consumer helpers adopted** from Common — `ShouldShortCircuit` and `RecordExceptionOutcome` replace local implementations.
- **RateLimitHeaderUtilities** adopted from Common for `Retry-After` header parsing.
- **Common submodule bumped** through multiple waves (v1.14.0 → v1.18.0-dev) — brings Wave-22-28 mega-merge, parity matrix, hot-path hardening.
- **Plugin.json extended** with `owner`, `repository`, `supportUri`, `changelogUri` for ecosystem parity.
- **Docker E2E builds against real Lidarr assemblies** — parity with qobuz/apple/brainarr approach.
- **CI/CD improvements**:
  - Reusable workflow refs migrated from SHA pins to `workflows/v1` tag.
  - Version read from VERSION file instead of removed const.
  - Submodule SHA pins synchronized with verify-pins gate.
  - Host assemblies path handling improved.
- **Security**:
  - Public Tidal protocol constants allowlisted in gitleaks.
  - Interoperability/anti-circumvention disclaimer added.
  - test.trx untracked (leaked machine names/paths).
  - Gitignore hardened for credentials.

### Dependencies

- Common submodule re-pinned to `c2aca69` (AuthFailureGate consumer helpers).
- FluentAssertions 6.12.2 → 8.10.0
- Microsoft.SourceLink.GitHub 8.0.0 → 10.0.300
- coverlet.msbuild 8.0.1 → 10.0.1
- ILRepack.Lib.MSBuild.Task 2.0.44.2 → 2.0.45
- actions/github-script 8 → 9
- actions/setup-dotnet 4 → 5

### Build

- Plugin-load-gate disabled for tidalarr (incompatible with internalize).
- Canonical-abstractions sidecar opt-out in packaging-gates.
- Missing init-common-submodule composite action added.

## [1.2.8] - 2026-05-24

### Changed

- Common submodule bumped v1.12.0 → v1.13.1.
- Sync plugin.json drift after Common bump.

## [1.2.7] - 2026-05-24

### Fixed

- **Search query-bleed hotfix** — `StreamingApiRequestBuilder` accumulated query params across calls, causing all searches to return cached/wrong responses. Fixed upstream in Common v1.12.0 with fail-on-reuse guard.

### Changed

- Sync-over-async guard migrated to Common canonical script.
- Info-log demotion for reduced noise.

## [1.2.6] - 2026-05-24

### Fixed

- **PathTraversalGuard trailing-slash hotfix** — all downloads no longer rejected with "resolves outside" when download path has trailing separator. Fixed upstream in Common #552 and pulled via submodule bump.

### Changed

- Sync-over-async lint guard adopted.

## [1.2.5] - 2026-05-24

### Added

- `HostBridgeRuntimeCache` — credential-change invalidation flushes cached bridge context immediately; 60s graveyard prevents in-flight request failures (Wave 13A).
- `PluginLogContext` + `Scrub` observability at 5 entry points — structured per-request correlation and log redaction across Indexer and DownloadClient pipelines (Wave 13C).

### Changed

- Stale credential edge case eliminated — cache no longer returns bridge context built from superseded OAuth tokens after settings save.

## [1.2.4] - 2026-05-24

### Changed

- `AlbumReleaseInfoBuilder` adopted — unified `ReleaseInfo` string construction replaces two hand-rolled format sites in `TidalLidarrIndexer`.
- `HostBridgeDownloadOrchestrator` adopted in `TidalLidarrDownloadClient` — settings-snapshot + tracked-enqueue fixes ProbeOnly race where in-flight snapshot could observe partial settings writes.

### Dependencies

- Common submodule bumped to v1.11.0.

## [1.2.3] - 2026-05-24

### Fixed

- `DownloadClient.Test()` multi-field failures now surface all errors — `TestValidationBuilder` from Common adopted; fixes latent bug where first failure silently swallowed subsequent field errors.

### Changed

- `BackendHealthCache` adopted via `TidalBackendHealthHandler` (DelegatingHandler, outermost layer of all 4 HTTP pipelines) — replaces hand-rolled per-plugin copy.
- `HostGateRegistry.Shutdown` called on module dispose.

### Dependencies

- Common submodule bumped to v1.10.0.

## [1.2.2] - 2026-05-23

### Fixed

- `invalid_grant` OAuth UX fix — clears consumed/expired authorization code from state and surfaces clear field-level error message instead of generic failure.
- `TidalModule.Version` now derived from assembly `InformationalVersionAttribute`; locked by contract test (TDD).
- `DownloadClient.Test()` adopted `TestValidationBuilder` — accumulates all field-level failures before returning, fixing latent bug where first error silently swallowed subsequent field failures.

### Changed

- HostBridge primitives adopted (`HostBridgeDownloadTracker`, `PrefixedReleaseGuidParser`, `PlaceholderSearchUri`) — ~120 LOC saved vs hand-rolled equivalents.
- Common submodule bumped to v1.9.5.

## [1.2.1] - 2026-05-23

### Fixed

- Common v1.9.3 — Lidarr-Docker token-protection hotfix + adversarial-review hardening.

## [1.2.0] - 2026-05-23

### Added

- `AuthFailureGate` adoption — quality-downgrade detector + Lidarr-native `FetchReleases` gate + HttpClient handler-chain trap closure.
- CI parity-lint VersionContract step + workflow Pester test.
- Documentation: Shared Infrastructure section, CHANGELOG, archived 9 historical plans.
- Security hardening backlog document (10 findings, 2 High).
- Hardcoded-creds ADR + packaging-gates workflow + regression test.
- `HttpExceptionClassifier` adoption in `TidalLidarrDownloadClient.Test()` — categorizes failures with actionable hints.

### Fixed

- Packaging failure made fatal + commonVersion bump.

### Changed

- Common bump to v1.11.0 for wave-16 security fixes — dropped 2 overrides, applied `[ParityAllowedTokenStore]`.

### Infrastructure

- Lidarr.Plugin.*.dll naming contract documented.

### Dependencies

- Common bumped through multiple versions (v1.5.0 → v1.7.1 → various SHA pins).

## [1.1.1] - 2026-05-23

### Fixed

- Release asset named with `net8.0.zip` suffix — required for Lidarr UI install to recognize plugin.
- TidalRateLimiter wired into every HttpClient — eliminates 429 storms.

## [1.1.0] - 2026-05-10

### Added

- Multi-plugin co-existence support.
- Docker E2E coverage cliff fix + sidecar-tolerant scripts in Common.
- FluentValidation pinned to 9.5.4 (host-coupled AssemblyVersion 9.0.0.0).
- M.E.* 8.0 alignment in Common testkit.

### Changed

- Common bumped to 90da1f6 (Abstractions cross-ALC fix) + aligned M.E.* pins.
- Common bumped to 904d5ae.
- Manifest: dropped deprecated `minimumVersion` (MAN004).
- Auto-generated UI screenshots refreshed.
- Non-http(s) redirect URLs rejected (cross-platform parse).
- `MetricsFactoryHttpMessageHandlerFilter` suppressed (ALC trap).
- M.E.Http reverted to 8.0.1 (was 9.0.0 — runtime mismatch).
- Manifest: bumped commonVersion 1.5.0 → 1.7.1.
- ext-common-sha.txt pin updated to 263a182.
- M.E.Http aligned to 9.0 + minimumVersion added.
- Sync-over-async lint: 4 new Category-A sites allowlisted.
- M.E.{DI,Logging.Abs} bumped to 9.0.0.
- Security: `CentralPackageTransitivePinningEnabled` enabled.
- Security: `System.Security.Cryptography.Xml` pinned >= 8.0.3.
- CI smoke-test pinned to Common SHA.

### UX

- TidalDownloadClient PreferredQuality describes tiers + subscription requirements (wave 83).
- OAuth token-exchange messages name stale-redirect cause (wave 79).
- Test() ConfigPath + generic-failure messages match wave 68/72 (wave 73).
- TidalLidarrIndexerSettings ConfigPath error names default location (wave 68).

### Quality

- Quality regression coverage for null-tags fix (wave 50).
- TidalQualityDetector null-tags + structured optimizer logging (waves 47, 49).
- CancellationToken propagated through search/album-detail paths (wave 38).
- Optimizer fire-and-forget swallow made explicit (wave 37).

### Infrastructure

- Docker E2E job wired using common composite action (wave 23).
- Consume common's lifted LidarrContainerFixture (wave 22a).
- Docker-based E2E harness — tidalarr smoke tests in real Lidarr container (wave 21).
- Long-tail coverage gap-fill (wave 20).
- Common bumped to 27cbe1b for wave-16 security fixes.
- PKCEStateStore encrypted at rest via `FileTokenStore<PKCEState>`.
- Coverlet measures Tidalarr modules numerically.
- ObservabilityShim reflective bind-and-invoke path covered.
- Settings migrated FluentValidation `.Errors.First()` → `.ToString()` (wave 11c).
- Common bump to 52a17ed (wave 11) — dropped 2 overrides, applied `[ParityAllowedTokenStore]`.
- Targeted coverage gap-fill (wave 12).
- Behavior-contract parity checks adopted.
- Remaining 4 cov-test failures resolved.
- Pre-existing tidalarr cov-test failures resolved (8 → 0).
- Common's `ChunkedHttpAssembler` adopted — collapsed two near-duplicate chunk paths (phase 5d).
- Common `PluginConfigRoots` adopted; `ConfigPathDefaults` deleted (phase 5b).
- Token storage migrated to common's encrypted `FileTokenStore<T>` (phase 2).
- PKCE routed through common's `PKCEGenerator`; collapsed token providers, added log redaction (phase 1).
- Local work recovered after SSD-crash + data-recovery corruption.
- Auto-generated UI screenshots refreshed.

### Documentation

- Multi-Plugin section updated — fixed 2026-05-10.

## [1.0.1-preview-obs-20251011-1534-911d939] - 2025-10-11

### Added

- CLI hardening: parsing for search/download + gated CLI tests.
- TFM_RATIONALE docs (core net6.0, CLI net9.0), linked in README and docs index.
- Submodule pinning workflow comparing `ext/Lidarr.Plugin.Common` to `ext-common-sha.txt`.
- `TidalResiliencePolicy` + tests removed; Polly dependencies dropped from plugin.
- Tech-debt issue template + `scripts/create-tech-debt-issues.ps1`.
- TECH_DEBT_BACKLOG with prioritized items + acceptance criteria.
- `CliFact` to conditionally skip CLI/packaging tests via `RUN_REAL_CLI_TESTS` env.
- PR template with HostBridge links, testing + packaging checklist.
- docs/README index and README Host vs. Core section with HostBridge link.
- HostBridge migration checklist, UI example, and diagnostics JSON examples.
- README links HostBridge integration guide for host setup.
- HostBridge integration guide with DI registration and mapping examples.
- DI extension `AddTidalarrHostBridgeServices` to register `IHostSettingsMapper`.
- `IHostSettingsMapper` for unified host->core conversion.
- Pretty enum labels + mapping: `TidalQualityHost` enum with NzbDrone `FieldOption` labels.
- `PreferredQuality` to `DownloadClient` host settings with `SelectOptions=typeof(TidalQualityHost)`.
- `ToCore()` mapping for Indexer/Download host settings.
- Host-only settings with NzbDrone annotations: `TidalarrHostSettings` implements `IIndexerSettings`/`IProviderConfig`.
- Core settings made hostless; HostBridge supplies annotated equivalents.
- Hostless annotations (`FieldDefinition`/`FieldOption`/`FieldType`).
- NzbDrone.* usages replaced with local aliases in settings/models.
- `IIndexerSettings`/`IProviderConfig` and `NzbDroneValidationResult` methods removed.
- Internal path validation extension (no host libs).
- Lidarr.Core/Common references dropped from plugin csproj.

### Infrastructure

- CLI diagnostics + packaging tests (Trait scope=cli).
- CLI integration tests for settings/indexer/download validate commands.
- Dependency-closure test on packaged zip.
- Tests opt-in via `[Fact(Skip=...)]` and marked Trait scope=cli.
- Robust repo-root detection + safe process invocations.
- Common `PluginOperationResult` and JSON helper adopted.
- Local `OperationResult` replaced with `Lidarr.Plugin.Abstractions.Results.PluginOperationResult`.
- Generic success payload with diagnostics id; failures mapped to `PluginError` with id/metadata.
- CLI emits Common-shaped JSON for settings/indexer/download validate commands.
- `DiagnosticTapHandler` removed.
- Tests assert Common result shape (value/error + metadata).
- `InternalsVisibleTo` for TidalCLI to access diagnostics helpers.
- PluginOperationResultJson vendored into ext; CLI diagnostics output switched to Common JSON shape.
- Nightly and release workflows; CLI tests filtered by default; submodule bumped to main.
- Diagnostics + resilience rewired for upstream API.
- CLI indexer-validate and download-validate commands (diagnostics JSON).
- HTTP calls aligned with current Common extensions.
- Local diagnostic tap references dropped.
- CLI settings-validate command that prints diagnostics JSON (CFG codes).
- `OperationResult` APIs exposed.
- CFG* diagnostics for aggregated settings (validate/apply) with tests.
- Mapping helper refactored.
- Indexer diagnostics (IX000/IX100/IX200) for settings + init.
- Diagnostics tests added.
- Stable IDs asserted.
- Internals exposed to tests.
- `StreamingTokenManager` with adapter + `ManagedTokenProvider`.
- DI registration and OAuth handler wiring.
- `Tidalarr.HostBridge` project and `TidalProtocol` moved; test project references bridge.
- `TidalStreamingAuthManager` and DI registration added.
- `IPlugin` entry implemented.
- Common `ContentDecodingSnifferHandler` adopted.
- `TidalProtocol` adjusted to match tests.
- Deterministic build flags added.
- Unified plugin guardrails added to CI.
- Universal adaptive rate limiter adopted.
- Latest lidarr.plugin.common submodule update.
- Extensions stack aligned with shared module.
- Streaming DI helpers centralized.
- Shared packaging targets imported.
- Lidarr adapters and alignment docs added.
- Latest shared helper fixes pulled.
- Repo build helper scripts added.
- Lidarr.Plugin.Common submodule updated.
- CLI smoke coverage for core commands added.

### CLI

- Cover art embedded after album download.
- Artist album folders created for downloads.
- README + agent file created.
- Submodule pointer updated to latest common lib.
- CLI quality mapping and config usage refined.
- Warnings-as-errors enforced in plugin and CLI.
- Nullable uses and async stubs fixed.
- CLI defaults and DI calls updated.
- `InputSanitizer` replaced with `Sanitize.DisplayText`.
- Build clean.
- Submodule merge `main-updated` into main and pointer update.
- Auto-open browser on auth-start.
- Configurable defaults (output dir, quality).
- Error messages improved for live commands.
- `TidalModule.RegisterServices` + `CreateOrchestrator` for DI-based composition used.
- DI-like orchestrator construction helper added.
- Live search command added.
- Duplicate switch cases fixed.
- Live OAuth commands (auth-start/auth-complete) added.
- Download commands using shared `SimpleDownloadOrchestrator` + `TidalChunkStreamProvider` added.

### Plugin

- `TidalChunkStreamProvider` DI-registered.
- `CreateOrchestrator` factory using shared `SimpleDownloadOrchestrator` added.
- `TidalChunkStreamProvider` implementing `IAudioStreamProvider` using `TidalStreamService` + `TidalChunkDownloader` added.
- Updated common submodule vendor (orchestrator progress + stream provider).
- Tidal orchestrator construction example added (commented, not built).
- Common submodule vendor (resilience adapter doc + simple orchestrator resume checkpoints).
- Common submodule vendor updated with resilience adapter and simple orchestrator.
- Shared `OAuthDelegatingHandler` with `IStreamingTokenProvider` used.
- Adapter for stubs added.
- API+chunk HTTP switched to shared resilience (429 Retry-After, retry budget, per-host gates).
- Filenames NFC+sanitize.
- Preview filtering.
- Downloads write .partial + atomic move + optional signature validation.
- Quality mapping preserves Tidal IDs while aligning tiers.

### Core

- DASH manifest parser implementation with TidalSharp patterns completed.
- Shared library with merged CLI framework improvements updated.
- Production-first CLI framework architecture integrated.
- Shared library submodule updated with build artifact cleanup.
- Roadmap updated with shared library integration progress.
- Shared library integration with `Lidarr.Plugin.Common` initiated.
- `Lidarr.Plugin.Common` submodule updated with .gitignore improvements.
- Integration with all Tidalarr services and shared library components completed.
- `Lidarr.Plugin.Common` submodule updated to v1.1.0.
- `Lidarr.Plugin.Common` enhanced (v1.1.0).
- Proper integration with `Lidarr.Plugin.Common` submodule.
- Core utilities extracted to `Lidarr.Plugin.Common` library.
- Using statements cleaned up in `EndToEndIntegrationTests`.
- Comprehensive unit test coverage for 100% testing goal added.
- Final build fixes and syntax corrections.
- Telemetry, observability and architect requirements completed.
- Architect feedback implementation - resilience and memory completed.
- Tidalarr MVP implementation with architect feedback integration completed.
