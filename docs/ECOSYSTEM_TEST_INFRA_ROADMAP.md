> ⚠️ Historical (flagged 2026-05-31): describes a past state; some details below no longer match the current code.

<!-- docval:ignore-script-refs — this file is a forward-looking roadmap; scripts referenced under unchecked [ ] items are proposed work that does not exist yet -->

# Ecosystem Test & CI Adoption Roadmap (Tidalarr)

Goal: keep Tidalarr “boringly green” under Lidarr plugin hosting while making failures diagnosable and local validation easy.

## Current Baseline (already landed)
- [x] Packaging policy tests + baseline doc (`docs/PACKAGING_POLICY_BASELINE.md`)
- [x] Host-version coupling guards (pin/validate host-provided dependencies)

## Phase 1 — Local Ergonomics (developer workflow)
- [ ] Add `scripts/check-host-versions.ps1` (extract/read host assembly versions; fail-fast mode for CI)
- [ ] Add `scripts/docker-smoke-test.ps1`
  - Starts Lidarr container, deploys built plugin, validates module types appear in API schema
  - Uses X-Api-Key from `config.xml` (Docker bridge networking makes “local address bypass” unreliable)
- [ ] Add `scripts/run-integration-tests.ps1`
  - Mirrors CI: extract host assemblies → build/package → smoke test → `dotnet test --filter Category=Integration`
- [ ] Add `tests/*.runsettings` (fast + full)
  - `BlameHangTimeout` ≥ 60s (fast) / ≥ 120s (full)
  - Standardized TRX output folder(s)

## Phase 2 — CI Wiring (when GitHub Actions is available)
- [ ] Add `workflow_dispatch` job: “integration + smoke”
  - Inputs: `lidarr_tag`, `schema_timeout_seconds`, `force_extract`
  - Steps: host version check (strict) → docker smoke test → integration tests
  - Always upload artifacts on failure (TRX, smoke logs, container logs, extracted assemblies manifest)
- [ ] Cache host assemblies by `lidarr_tag` (instant re-runs)

## Phase 3 — Test Semantics & Diagnostics
- [ ] Standardize test categories (`Category=Integration`, `Category=Packaging`, `Category=Performance`, etc.)
- [ ] Add trait linter (PowerShell) to enforce category hygiene (e.g. any Docker/Lidarr-dependent tests must be `Integration`)
- [ ] Introduce a JSON extraction helper (like `JsonExtractor`) if/when integration tests parse API JSON
  - Required-field helpers should throw a *skip* exception with endpoint + sanitized response snippet

## Phase 4 — Reduce Ecosystem Drift (shared infrastructure)
- [ ] Extract shared test infra into `lidarr.plugin.common` (or a small “Testing” package):
  - Packaging strict/skip semantics (CI fails, local skips)
  - Host-version check script + test helpers
  - Common “package discovery” utilities
- [ ] Adopt the same “packaging policy” assertions in all plugins (Brainarr/Qobuzarr/Tidalarr)

## CLI Stability (what to do when CLI tests fail)
1. Reproduce with one failing test only:
   - `dotnet test tests/Tidalarr.Tests/Tidalarr.Tests.csproj -c Release --filter FullyQualifiedName~CLI --nologo`
2. Decide the failure class:
   - Parsing/validation mismatch → adjust CLI parsing (prefer `TheoryData<>` and explicit expected error messages)
   - Timing/network dependency → replace `Task.Delay` with awaited signals (tasks/events) and deterministic fakes
   - Host-coupling mismatch → run `scripts/check-host-versions.ps1` and fix pins/packaging policy
3. Lock the fix in with a characterization test (TDD):
   - First add the failing test that reproduces the bug
   - Implement minimal change to pass
   - Add at least one regression edge case (null/empty/invalid input)

