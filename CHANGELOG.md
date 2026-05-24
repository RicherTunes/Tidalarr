# Changelog

All notable changes to Tidalarr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.1] - 2026-05-23

### Critical fix — Lidarr Docker startup failure

Bumps `ext/Lidarr.Plugin.Common` from v1.9.1 to **v1.9.3** (skips v1.9.2; goes directly to the adversarial-review-hardened release). Picks up Common's fix for the Lidarr Docker `UnauthorizedAccessException: Access to the path '/app/bin/.config' is denied` bug that affected every plugin storing tokens via `FileTokenStore<TidalTokens>` on hotio/linuxserver images. Tidalarr code is unchanged.

The fix arrives via the submodule bump. Operators see:
- A writable DataProtection key dir chosen from a candidate chain (`XDG_DATA_HOME` → ... → `Path.GetTempPath()` as last resort), never a relative path.
- Graceful degradation to a plaintext `NullTokenProtector` if every backend fails to initialise. The on-disk envelope uses a distinct `lpc:plain:v1:` prefix so audit queries can tell unprotected blobs apart from real ciphertext.
- A diagnostic surface (`TokenProtectorFactory.IsDegradedToPlaintext` + `LastDiagnostics`) plugin-startup code can read to log a one-line warning when the keystore is degraded.
- `LP_COMMON_REQUIRE_PROTECTOR=true` opt-in for operators who want hard-failure instead of plaintext fallback.

See [Lidarr.Plugin.Common v1.9.3 changelog](https://github.com/RicherTunes/Lidarr.Plugin.Common/blob/main/CHANGELOG.md#193---2026-05-23) for root-cause + fix details.

[Full diff](https://github.com/RicherTunes/Tidalarr/compare/v1.2.0...v1.2.1)

## [1.2.0] - 2026-05-23

### AuthFailureGate adoption + adversarial-review fixes

- **Lidarr-native `TidalLidarrIndexer.FetchReleases` now consults `AuthFailureGate`.** The previous wave's gate wire-up only covered the abstraction-path `TidalIndexer`; the Lidarr-native indexer that 95% of users actually drive bypassed the gate entirely. After a 401 the indexer would just log a warning per query and re-enter the search loop on the next tier — the exact failure mode that previously got Qobuz users IP-banned. Now: `IsHealthy` / `TryAcquireProbeSlot` short-circuit at the top of `FetchReleases`; per-tier catch blocks classify via `LooksLikeAuthFailure` and signal `gate.Handler.HandleFailureAsync` on 401/403.
- **`TidalApiClient.GetStreamInfoAsync` no longer fabricates `DeliveredQuality`.** The previous shape `MapQualityFromString(dto.audioQuality ?? string.Empty)` resolved to `TidalQuality.High` on empty input, so any HiRes-requested track where Tidal omitted `audioQuality` (Tidal's API is inconsistent here) fired a spurious downgrade warning. Now: `DeliveredQuality` stays null when Tidal omits the field — matches `TidalQualityDowngradeDetector`'s "unknown — do not warn" contract.
- **Named HttpClient `TidalIndexer.Base` registered in `TidalModule` with the full handler chain.** Previous inline `new HttpClient(handler)` / `new HttpClient()` in `TidalIndexer`'s constructor bypassed `TidalRateLimitingHandler`, `ContentDecodingSnifferHandler`, and `AuthFailureDelegatingHandler`. Latent today (no caller drives `BaseStreamingIndexer.ExecuteRequestAsync` through this client) but a trap for any future base-class override. `TidalIndexer` constructor now takes optional `IHttpClientFactory?`; prefers `factory.CreateClient("TidalIndexer.Base")` when available, falls back to inline creation for test / non-DI callers.

### Common library bump

- Bumps `ext/Lidarr.Plugin.Common` from `431fe97` (between v1.7.1 and v1.8.0) all the way to **v1.9.1** — picks up the new `AuthFailureGate` surface (which Tidalarr already adopted on `feat/adopt-http-exception-classifier`), `SecureMemory`, `PagedResponseValidator`, `Conservative rate-limit profile`, `HttpExceptionClassifier` (which `TidalLidarrDownloadClient.Test()` consumes), TestKit-lifted plugin contracts, and `Lidarr.Plugin.*.dll` naming enforcement.

### Quality-downgrade detector + log redaction

- New `TidalQualityDowngradeDetector` warns (does not block) when Tidal delivered a lower quality tier than requested. Hooked from `TidalDownloadClient` and `TidalChunkStreamProvider`.
- Exception messages routed through `LogRedactor.Redact` / `RedactException` in `TidalLidarrIndexer.FetchReleases` so OAuth tokens / `?code=…` / `&state=…` in stack-traced URLs no longer leak into Lidarr's logs.

### Tests

- 130 targeted tests pass: `TidalIndexerAuthGate`, `TidalAuthFailureDelegatingHandlerWireUp`, `TidalQualityDowngradeDetector`, `TidalDownloadClientQualityDowngrade`, `TidalApiClient`, `TidalModule`, `TidalLogRedaction`, `TidalDownloadItemConcurrency`.

[Full diff](https://github.com/RicherTunes/Tidalarr/compare/v1.1.0...v1.2.0)

## [1.1.0] - 2026-05-23

### Phase 0 + Phase 1 — Ecosystem Alignment and Packaging Fix

#### Ecosystem version contract (Phase 0.3)

- Bumped `commonVersion` to `1.8.0` in `plugin.json` to align with Common v1.8.0.
- Parity-lint `VersionContract` check passes (`ecosystem-parity-lint.ps1 -Check VersionContract`).
- Plugin confirmed to target `net8.0` only; `net6.0` / `net7.0` absent from all manifests.

#### Phase 0 — packaging fatal fix

- Made packaging failure fatal in CI: the `packaging-gates.yml` workflow now blocks merges when the plugin ZIP fails assembly validation. Previously, packaging errors were advisory and slipped through.
- Regression test added for the packaging gate behavior.

#### Phase 0 — hardcoded credentials ADR

- `docs/decisions/0001-hardcoded-tidal-client-credentials.md` added: architecture decision record documenting the use of the public Tidal developer portal credentials and the migration path when per-user credentials become available.

#### Phase 0 — Docker smoke test fixes

- Docker smoke test corrected: mount path updated to the exact plugin directory, Abstractions assembly mounted separately, timeout increased for slower CI runners.
- `SkipHostBridge` build variant handled gracefully (test skips instead of failing with file-not-found).

#### Phase 1 — docs and security

- Security hardening backlog added: 10 findings, 2 High severity — see `docs/SECURITY_HARDENING_BACKLOG.md`.
- README augmented with Shared Infrastructure section (Common services consumed, version contract reference).
- Documentation section added to README with links to CHANGELOG, SECURITY, and docs/.
- Historical implementation plan files (`TIDALARR_*_PLAN*.md`) moved to `docs/archive/`.
