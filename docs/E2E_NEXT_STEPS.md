# Tidalarr E2E Next Steps (Local + CI)

This document is the short, actionable checklist for getting **Tidalarr** and **Qobuzarr** running together in a Lidarr Docker instance, with increasing confidence gates (schema → auth → search → grab).

## Current State (Baseline)

- Host image baseline: `ghcr.io/hotio/lidarr:pr-plugins-3.1.1.4884` (net8 host)
- Qobuzarr: single-plugin E2E (search + grab) works locally
- Tidalarr: plugin loads; OAuth flow requires browser; E2E gates pending OAuth completion
- Multi-plugin: blocked until Lidarr upstream ALC/WeakReference fix is available in a Docker tag

## Gate Definitions (What “Works” Means)

1. **Gate 0 — Build/Package**
   - `dotnet build -c Release` and packaging produces a plugin zip.
2. **Gate 1 — Schema Load**
   - `/api/v1/indexer/schema` contains plugin indexer.
   - `/api/v1/downloadclient/schema` contains plugin download client.
3. **Gate 2 — Auth**
   - Indexer `Test` succeeds after OAuth redirect URL is supplied.
4. **Gate 3 — Search**
   - Album search returns releases for a known album.
5. **Gate 4 — Grab**
   - Download client downloads an album and produces valid audio files on disk.

## Local E2E: Tidalarr (Persistent Docker)

### One-time setup

- Decide your container-persisted config path (recommended): `/config/tidalarr`
- Ensure Docker mounts exist:
  - `/config` persisted
  - `/downloads` persisted
  - `/music` persisted

### OAuth flow (manual browser)

1. In Lidarr UI: Settings → Indexers → Add → Tidalarr
2. Set `ConfigPath` to `/config/tidalarr` (must be writable)
3. Copy `Tidal Auth URL`, open in browser, login, authorize
4. Copy the redirect URL and paste into `OAuth Redirect URL`
5. Click `Test` (Gate 2)

### Search + Grab (after OAuth)

- Run a known album search (Gate 3) then `Grab` (Gate 4).
- If grab fails:
  - Inspect container logs and the downloaded file headers (HTML/JSON vs audio).

## Test Improvements to Port/Standardize

### Candidates to share via `lidarr.plugin.common` (high reuse)

- Docker smoke test harness scripts (schema gate, log capture, artifact bundle)
- Host-version drift checks (pin checks against extracted host assemblies)
- Packaging policy tests (required/forbidden DLL lists + size sanity)

### Keep plugin-local (low reuse / high coupling)

- OAuth/provider logic (Tidal PKCE, Qobuz dynamic creds)
- Service-specific parsing/title logic
- Download chunking/manifest quirks

## Fixing the Tidalarr Test Situation (Pre-existing failures)

When integration/DI tests are failing:

1. **Separate “broken unit tests” vs “real integration tests”.**
   - If a test fails because constructors/DI changed, fix it.
   - If a test requires host assemblies or network, categorize it and run it only under an explicit integration gate.
2. Prefer a default “green” suite:
   - Unit tests: always run locally/CI.
   - Integration tests: opt-in via `--filter Category=Integration` or workflow inputs.

## Next Small Roadmap (2–3 PRs)

1. **Land persistent PKCE changes** (Tidalarr PR created from `fix/persistent-pkce`)
   - Auth URL stable across restarts; redirect exchange works without copying tokens manually.
2. **Add a persistent Tidalarr runner script** (in `lidarr.plugin.common/scripts/`)
   - Mirrors the existing Qobuzarr persistent runner.
   - Supports Gate 1–4 for Tidalarr.
3. **Wire a workflow_dispatch E2E runner (credential-gated)** (in `lidarr.plugin.common`)
   - Runs schema gate always.
   - Runs auth/search/grab gates only when secrets are present.

