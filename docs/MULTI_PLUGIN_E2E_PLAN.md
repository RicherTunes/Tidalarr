# Multi-Plugin Docker E2E Plan (Tidalarr + Qobuzarr)

## Goal
Prove that **Tidalarr** and **Qobuzarr** can co-exist in the same Lidarr Docker instance and:
1. Load successfully (no `TypeLoadException` / ALC unload issues).
2. Register schemas (indexer + download client).
3. Search returns releases (credential-gated).
4. Grab starts a download (credential-gated).
5. Download produces non-empty media files on disk (credential-gated, optional).

## Definitions Of Done
### Gate 1 (Always-On): Schema Load
- Lidarr starts with both plugin zips mounted.
- `/api/v1/indexer/schema` contains:
  - `TidalLidarrIndexer`
  - `QobuzIndexer`
- `/api/v1/downloadclient/schema` contains:
  - `TidalLidarrDownloadClient`
  - `QobuzDownloadClient`

### Gate 2 (Credential-Gated): Search
- Create indexers via API and trigger an `AlbumSearch`.
- `/api/v1/release?albumId={id}` returns at least 1 result for each provider.

### Gate 3 (Credential-Gated): Grab + Download
- Grab a release, queue transitions through downloading → completed.
- At least 1 output file exists and is non-empty.
- Prefer verifying a basic magic header when possible (e.g., `fLaC`, `ID3`, `OggS`), not only file length.

## Known External Dependency
- Multi-plugin load reliability is blocked by Lidarr host bugs if the host unloads plugin `AssemblyLoadContext` early.
- Track upstream Lidarr fix(es) and pin the Docker tag used by E2E runs to a version that includes them.

## What We Can Reuse (Ecosystem Patterns)
Adopt (or keep aligned with) Qobuzarr ecosystem infrastructure:
- Packaging policy tests (required/forbidden DLLs, size sanity).
- Host-version coupling guard script/tests (detect version drift for cross-boundary assemblies).
- Integration test traits + skip semantics:
  - `Category=Integration`
  - runtime skip (yellow) when environment is not configured.
- JSON extraction helper that fails fast with context (avoid `?? 0` masking broken API responses).

## Work Items (Tidalarr)
### 1) CI/Test Infrastructure Parity
- Ensure integration tests use `Category=Integration`.
- Ensure integration tests have a single “readiness gate” helper (skip/yellow, not early-return green).
- Prefer deterministic integration diagnostics (sanitized response snippets).

### 2) Packaging/Host Guards
- Keep packaging policy tests up-to-date with the ecosystem policy.
- Add/maintain host-coupling guards for any type-identity assemblies crossing the plugin boundary.

### 3) Download Robustness Diagnostics
When grab/download fails:
- Detect and surface “text/html/json response instead of media” early.
- Include safe diagnostics (host + content-type + capped/redacted snippet).
- Prefer fail-fast exceptions over “file length invalid” at the end of the pipeline.

### 4) Local Developer Loop
- Use a persistent Docker config directory for repeated local testing.
- Add a “basic schema smoke” local script that does not require provider credentials.
- Add an optional “credential run” local script that:
  - configures Lidarr API key auth,
  - injects provider credentials from environment variables,
  - runs Gate 2/3.

## Sequencing
1. Gate 1 stable (schema-only) with pinned Lidarr Docker tag.
2. Gate 2 search gate behind secrets (workflow_dispatch).
3. Gate 3 grab/download behind secrets (workflow_dispatch).
4. Promote gates into scheduled runs once stable.

## Parallel Work
While E2E gates are being stabilized:
- Continue hardening Tidalarr downloader diagnostics (same patterns as Qobuzarr).
- Keep packaging/host-guard tests strict in CI, skip-friendly locally.
- Track upstream Lidarr PRs affecting plugin loader stability and update the pinned Docker tag when available.
