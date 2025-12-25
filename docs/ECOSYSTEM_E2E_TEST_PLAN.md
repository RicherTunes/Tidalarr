# Ecosystem E2E Test Plan (Lidarr + Qobuzarr + Tidalarr)

Goal: prove **Qobuzarr** and **Tidalarr** can coexist in a single Lidarr Docker instance and
perform the core user flows: **discover → search → download**.

This plan documents the gates, required tooling, and the work backlog to get there in a
repeatable way (local + CI).

## Current Status

- **Single-plugin schema gate**: works for each plugin individually (each shows up in
  `/api/v1/indexer/schema` + `/api/v1/downloadclient/schema`).
- **Multi-plugin schema gate**: currently blocked by a Lidarr plugins-branch host issue where
  plugin `AssemblyLoadContext` instances are unloaded during type discovery when multiple plugins
  are present. Track/fix upstream before treating multi-plugin failures as plugin regressions.
  - Upstream PR: https://github.com/Lidarr/Lidarr/pull/5662

## Gates (Definition of Done)

### Gate 0 — Build + Package (Per Plugin)

Output: a plugin `.zip` containing at least:
- `plugin.json`
- `Lidarr.Plugin.<PluginName>.dll`
- `Lidarr.Plugin.Abstractions.dll` (required for plugin discovery in the hotio plugins image)

Run:
- `pwsh ./build.ps1 -Package -Configuration Release`

### Gate 1 — Schema Gate (Multi-Plugin)

Definition: with both plugin zips mounted, Lidarr starts and both plugins appear in:
- `GET /api/v1/indexer/schema` (Qobuz + Tidal implementations)
- `GET /api/v1/downloadclient/schema` (Qobuz + Tidal implementations)

Run (from `lidarr.plugin.common`):
- `pwsh ./scripts/multi-plugin-docker-smoke-test.ps1 -PluginZip @('qobuzarr=...zip','tidalarr=...zip')`

### Gate 2 — Configuration Gate (API wiring)

Definition: create indexer + download client configurations via Lidarr API and successfully
validate them (the exact fields are schema-driven).

Notes:
- This gate becomes stable once the schema gate is green.
- Keep secrets out of logs and artifacts.

### Gate 3 — Search Gate (Indexer returns releases)

Definition:
- Seed an artist + album via lookup endpoints.
- Trigger `AlbumSearch`.
- Assert `GET /api/v1/release?albumId=...` returns non-empty results.
- Optional: assert releases include entries attributed to both indexers (Qobuz + Tidal) when both
  are configured.

### Gate 4 — Download Gate (Credential-gated)

Definition:
- Choose a small known album.
- Trigger a download via the download client.
- Assert a file (or folder) is created under the container `/downloads` mount.

This is optional in CI and should usually run only on `workflow_dispatch` with secrets.

## Local Workflow (Recommended)

1. Build packages:
   - Qobuzarr: `pwsh D:\Alex\github\qobuzarr\build.ps1 -Package -Configuration Release`
   - Tidalarr: `pwsh D:\Alex\github\tidalarr\build.ps1 -Package -Configuration Release`
2. Run schema gate (multi-plugin):
   - `pwsh D:\Alex\github\lidarr.plugin.common\scripts\multi-plugin-docker-smoke-test.ps1 -PluginZip @('qobuzarr=...','tidalarr=...')`
3. If schema gate passes, enable configuration/search gates with the required environment variables.

## CI Workflow (When GitHub Actions Is Available)

Preferred pattern:
- Always run **Gate 0** (build/package/unit tests) on PRs.
- Run **Gate 1** on PRs if it is stable on the selected Lidarr tag.
- Run **Gate 2–4** only on `workflow_dispatch` (credential-gated).

Artifacts on failure:
- container logs
- plugin staging directory manifest (file listing)
- TRX results (if integration tests run)

## Backlog (Action Items)

### High Priority

- Track Lidarr host fix for multi-plugin load contexts (PR 5662) and bump the pinned image tag once released.
- Add a local `scripts/run-ecosystem-smoke.ps1` wrapper in this repo that:
  - builds Tidalarr package
  - locates latest Qobuzarr package (or accepts a path)
  - invokes the common harness with consistent parameters

### Medium Priority

- Add a small set of **contract tests** that validate:
  - plugin manifests contain required fields (`id`, `minHostVersion`, `targetFramework`)
  - packages do not include host assemblies (Lidarr.* / NzbDrone.*)
  - host-coupled dependencies are aligned when applicable (documented in each repo)

### Low Priority / Tech Debt

- Standardize CLI test structure (TidalCLI + QobuzCLI) so “CLI smoke” tests run in a predictable,
  non-interactive mode.
- Convert any remaining timing-based tests to deterministic synchronization (no `Task.Delay` gates).

