# Porting Proven Qobuzarr Test Patterns to Tidalarr

This doc is a practical checklist for selectively adopting the most valuable test + tooling patterns from Qobuzarr into Tidalarr, without importing Qobuzarr-specific behavior/tests.

## Goals

- Make `dotnet test` a trustworthy gate (deterministic, no timing luck).
- Prevent host-version runtime failures (type identity + pinned host-coupled deps).
- Enforce packaging policy automatically (required/forbidden assemblies).
- Provide a repeatable local Docker workflow that proves "loads + searches + downloads" (credential-gated).
- Keep plugin-specific behaviors (Tidal vs Qobuz) tested locally; share only the infra patterns.

## Non-Goals

- Port Qobuzarr-only semantics (TitleGenerator edition heuristics, Qobuz ML optimizer behavior, Qobuz auth scraping).
- Force identical UX strings across plugins.

## Phase 0: Baseline Inventory (1–2h)

- Confirm current test categories and CI filters (`docs/TESTING.md` or workflows).
- Confirm current packaging baseline doc exists and is current (`docs/PACKAGING_POLICY_BASELINE.md`).
- Confirm current host assemblies location and extraction flow (Docker-based or checked-in `ext/`).

## Phase 1: “Always-On” Safety Nets (same day)

### 1) Packaging policy tests (already started)

Status: see `docs/PACKAGING_POLICY_BASELINE.md` and `tests/Tidalarr.Tests/*Packaging*`.

Checklist:
- Required assemblies test (plugin DLL + type-identity deps + any approved runtime deps).
- Forbidden assemblies test (host-provided `Lidarr.*`, `NzbDrone.*`, `System.Text.Json`, etc.).
- Size sanity test (bloat guardrail).
- Metadata sanity test (plugin.json ↔ zip contents).
- Strict mode behavior:
  - Local dev: skip when no package exists.
  - CI: fail if package is missing.

### 2) Host-version coupling checks (guard tests + script)

Port pattern:
- A script that reads versions from `ext/Lidarr/_output/*/` and compares to pinned package versions.
- Guard tests that fail if host-coupled packages drift.

Checklist:
- Decide Tidalarr’s host-coupled deps (typically `FluentValidation`, `NLog`, and any other boundary-crossing types).
- Add a `scripts/check-host-versions.ps1` variant (or reuse from Common if extracted).
- Add unit tests that:
  - Assert plugin output does NOT ship host-coupled assemblies.
  - Assert referenced versions match host assemblies.

### 3) Integration test skip semantics

Port pattern:
- Use runtime skip (yellow) rather than early-return “green passes” when env is not configured.

Checklist:
- Choose a single mechanism:
  - Prefer xUnit native skip if available in your version.
  - Otherwise use `Xunit.SkippableFact` only within `tests/Integration/`.
- Standardize traits:
  - All live tests must have `[Trait("Category", "Integration")]`.
- Add a lint script to enforce trait hygiene.

## Phase 2: Local Docker Proof (repeatable) (0.5–1 day)

### 1) Persistent local runner

Port Qobuzarr’s pattern:
- A PowerShell script that:
  - Persists Lidarr config to a local directory.
  - Rebuilds and replaces the plugin zip on demand.
  - Starts/updates a Docker container using a pinned Lidarr `pr-plugins-*` tag.
  - Prints the URL and key health/schema checks.

Suggested file:
- `scripts/test-tidalarr-persistent.ps1`

Inputs:
- `-Rebuild`, `-Clean`, `-KeepRunning`, `-LidarrTag`, `-Port`.

### 2) Docker smoke gates

Define incremental gates:
- Gate 1 (no credentials): Schema-only (indexer + download client appear).
- Gate 2 (credentials): Search returns releases.
- Gate 3 (credentials): Grab creates download tasks and writes files to disk.

Notes:
- Keep Gate 2/3 credential-gated and off-by-default.
- Avoid logging tokens; redact or log only “present/absent”.

## Phase 3: Shared Test Infrastructure Extraction (future, ecosystem)

If multiple plugins converge on identical helpers, extract shared infra into one of:

- `lidarr.plugin.common` (preferred): a small `Lidarr.Plugin.Common.Testing` library for:
  - `PackagingFactAttribute` strict-mode behavior
  - packaging zip discovery helpers
  - host version reader helpers
  - redacting log helpers for tests
- Or a shared “test infra” folder copied as a submodule (second-best).

Avoid extracting plugin-specific test cases; extract the helpers/framework only.

## “Definition of Done” (Tidalarr)

- `dotnet test tidalarr/Tidalarr.sln -c Release` passes in a clean environment (no Docker required).
- Gate 1 Docker smoke passes with no credentials.
- Gate 2/3 can be run locally with credentials and reliably produce results.
- Packaging policy is enforced in CI (and strict-mode fails on violations).

