# Test Parity & E2E Readiness Plan (Tidalarr)

Goal: keep Tidalarr “boringly green” while converging the ecosystem on a single, repeatable proof that **Qobuzarr + Tidalarr can coexist in one Lidarr Docker instance** (schema → configure → search → optional download).

This plan focuses on **test infrastructure parity** (what we should share/standardize) and on **removing hidden regression risk** (tests excluded via `ExcludeHostBridge`).

## Scope Boundaries (what belongs where)

Move to `lidarr.plugin.common` (shared test infra):
- Packaging strict/skip semantics (CI fails on missing artifact; local can skip).
- “Discover built package zip” helpers and metadata parsing helpers.
- Host-version check helpers/scripts (read host assembly versions; compare to pins).
- Multi-plugin Docker harness (schema/config/search gates) and orchestrator workflow.

Keep in Tidalarr (plugin-specific tests):
- Any title/format logic unique to Tidalarr.
- Any parsing/mapping specific to Tidal DTOs.
- Any behavior tied to Tidalarr settings / auth flows / download paths.

## Current Reality (important)

`tests/Tidalarr.Tests/Tidalarr.Tests.csproj` uses `ExcludeHostBridge=true` to **compile-remove** large sets of tests (including many `Tidal*.cs` tests). This keeps CI green but creates “dark debt”: excluded tests can rot without notice.

Definition of done for this plan: **CI runs a meaningful subset AND we have an explicit, repeatable way to run the full suite locally** (with clear skip semantics instead of compile-removes wherever possible).

## Phase 0 — Make “what CI runs” explicit (1–2 PRs)

1. Add a single documented command that matches CI exactly:
   - `dotnet test tests/Tidalarr.Tests/Tidalarr.Tests.csproj -c Release -p:ExcludeHostBridge=true --nologo`
2. Add/standardize test traits to replace most compile-removes:
   - Category recommendations:
     - `Category=Unit` (default, always runs)
     - `Category=Integration` (requires Lidarr host / Docker)
     - `Category=HostBridge` (requires `ext/Lidarr/_output` assemblies)
     - `Category=Packaging` (requires built package zip; strict in CI)
     - `Category=CLI` (pure CLI parsing/formatting; no network)
3. Replace “early return pass” patterns with real skip semantics for env-gated tests:
   - Use a single helper (e.g., `Skip.If(...)`) so dashboards show yellow “skipped”, not green “passed”.

## Phase 1 — Port high-leverage infra patterns from Qobuzarr (2–4 PRs)

1. JSON extraction helper (Qobuzarr’s `JsonExtractor` pattern):
   - Create `tests/Tidalarr.Tests/utils/JsonExtractor.cs` (or reuse from common if extracted).
   - Required-field getters throw a *skip* exception with endpoint + sanitized snippet.
2. Trait linting:
   - Add a script like `scripts/lint-test-traits.ps1`:
     - Any Docker/Lidarr-dependent test must be `Category=Integration`.
     - Any packaging test must be `Category=Packaging`.
3. Local “integration runner” script:
   - `scripts/run-integration-tests.ps1` that does:
     - extract host assemblies → build/package → docker smoke test → `dotnet test --filter Category=Integration`

## Phase 2 — Pay down the `ExcludeHostBridge` debt (incremental, safest path)

Approach: convert one excluded bucket at a time from “Compile Remove” into:
- `Category=...` + deterministic fakes, or
- `SkippableFact` (or similar) with a clear skip reason, or
- delete tests that assert obsolete behavior (replace with characterization tests first).

Recommended order:
1. **ArchitectValidationTests**: update assertions to reflect current DI registrations (these should be stable).
2. **File IO tests** (e.g., enhanced download tests): ensure unique temp dirs per test + deterministic cleanup to prevent file lock failures.
3. **Old `Tidal*.cs` integration-style tests**: either modernize to use fakes or explicitly mark as `Category=Integration`.

## Phase 3 — Ecosystem E2E Proof (depends on Lidarr host fix)

We already have the right gates; the blocker is Lidarr host stability when loading multiple plugins.

1. Gate 1 (uncredentialed): schema-only
   - Assert Qobuzarr + Tidalarr indexers/download clients appear in Lidarr schema endpoints.
2. Gate 2 (credential-gated): configure + search
   - POST indexer + download client config
   - Trigger an `AlbumSearch` command
   - Assert releases are returned
3. Gate 3 (optional, credential-gated): download produces a file

Tracking note: multi-plugin load currently depends on the upstream Lidarr `PluginLoader` fix (keeping `PluginLoadContext` alive during load).

## Parallel Work (safe to do anytime)

- Add `global.json` to Tidalarr to pin .NET 8 SDK (prevents “works on my dotnet 9” drift).
- Expand packaging policy tests if needed (host-provided vs shipped vs merged).
- Add/refresh a small “local release checklist” for building and inspecting plugin zips.

