# Tidalarr Testing Adoption Plan

Goal: make Tidalarr reliable in the ecosystem by enforcing packaging policy, preventing host-version drift, and proving plugin load + search + (optional) download in a Lidarr Docker instance.

## Principles

- Prefer **black-box** tests (package contents, Lidarr API behavior) over testing private implementation details.
- Default test runs must be **fast and deterministic**; credential-gated integration is **opt-in**.
- Treat anything that crosses the plugin boundary as **host-owned** (type identity): plugin must not ship duplicate copies.

## Phase 0 — Local Baseline (repeatable)

- Build + unit tests:
  - `dotnet test Tidalarr.sln -c Release --filter "Category!=Integration"`
- Package:
  - `pwsh ./build.ps1 -Package -Configuration Release`
- Inspect zip contents:
  - `src/Tidalarr/artifacts/packages/*.zip`

## Phase 1 — Packaging Policy (CI-safe)

Already implemented:
- Packaging policy tests: `tests/Tidalarr.Tests/Unit/Packaging/*`
- Baseline: `docs/PACKAGING_POLICY_BASELINE.md`

Policy (current):
- **Required (type identity)**: `Lidarr.Plugin.Abstractions.dll`, `Microsoft.Extensions.DependencyInjection.Abstractions.dll`, `Microsoft.Extensions.Logging.Abstractions.dll`
- **Required (plugin)**: `Lidarr.Plugin.Tidalarr.dll`
- **Optional**: `Lidarr.Plugin.Common.dll`
- **Forbidden**: host assemblies (`Lidarr.*.dll`, `NzbDrone.*.dll`), `System.Text.Json.dll`, `FluentValidation.dll`

Why `FluentValidation.dll` is forbidden:
- `DownloadClientBase<TSettings>.Test(List<ValidationFailure>)` uses host FluentValidation types; shipping a plugin-local FluentValidation creates type-identity mismatch and can crash plugin load with `TypeLoadException`.

## Phase 2 — Host Version Coupling Guards (CI-safe)

Already implemented:
- `tests/Tidalarr.Tests/Unit/Packaging/HostVersionCouplingTests.cs`

Next refinements:
- Decide which host-coupled assemblies should be checked (start minimal: `NLog.dll`, `FluentValidation.dll`).
- Ensure `ext/Lidarr/_output/net8.0` is refreshed when the baseline Lidarr Docker tag changes.

## Phase 3 — Docker Smoke Test (no provider creds)

Gate: “plugins load and appear in schema”
- Use the ecosystem harness in `Lidarr.Plugin.Common`:
  - `scripts/multi-plugin-docker-smoke-test.ps1`
  - Expected: Tidalarr indexer + download client show up in:
    - `/api/v1/indexer/schema`
    - `/api/v1/downloadclient/schema`

Recommended workflow wiring (when Actions billing is available):
- `workflow_dispatch` only.
- Upload artifacts on failure: `container.log`, `inspect.json`, staging dir.

## Phase 4 — Search Gate (credential-gated)

Gate: “search returns releases”
- Requires provider credentials.
- Flow:
  1. Configure indexer(s) via Lidarr API (`POST /api/v1/indexer`).
  2. Trigger `AlbumSearch` (`POST /api/v1/command`).
  3. Wait for completion (`GET /api/v1/command/{id}`).
  4. Assert non-empty results (`GET /api/v1/release?albumId=...`).

Notes:
- Treat this as opt-in in CI (workflow input).
- Prefer a stable, globally-available album as a fixture (document it once).

## Phase 5 — Download Gate (credential-gated, optional)

Gate: “download completes and file exists”
- This is the most expensive and most brittle gate; keep optional.
- Verify:
  - Lidarr queue item moves to completed
  - expected path exists on mounted volume

## Cleanup / Tech Debt Backlog

- Reduce ILRepack warnings (“Method reference is used with definition return type / parameter”) by ensuring the merge input set is complete and consistent.
- Standardize `plugin.json` fields (`minHostVersion`, `commonVersion`) to eliminate manifest warnings.
- Keep integration tests properly categorized and skippable (yellow) when env is missing, not “green early return”.

