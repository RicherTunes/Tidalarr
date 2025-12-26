# Tidalarr Testing Adoption Plan (Qobuzarr Parity)

This document tracks test + tooling patterns proven in Qobuzarr that are worth adopting in Tidalarr to improve reliability, safety, and end-to-end confidence.

## Goals

- Keep unit tests deterministic and fast.
- Catch packaging / host-coupling regressions before runtime.
- Harden download validation so CDN/API failures don’t silently produce non-audio files.
- Provide repeatable local + CI Docker workflows to prove *search* and *download* work against Lidarr.

## Current State (Already Landed)

- [x] Packaging policy baseline + tests (`docs/PACKAGING_POLICY_BASELINE.md`, `tests/Tidalarr.Tests/Unit/Packaging/*`)
- [x] Host-version coupling guards (pins validated against extracted Lidarr assemblies)
- [x] Multi-plugin ecosystem planning (`docs/ECOSYSTEM_E2E_PLAN.md`, `docs/ECOSYSTEM_TEST_INFRA_ROADMAP.md`)

## Phase 1 — Download Validation Parity (Safety)

- [ ] Strict “text payload” sniff (HTML/JSON) before writing full downloads (both legacy + manifest download paths)
- [ ] Magic bytes validation for common containers:
  - FLAC (`fLaC`)
  - MP3 (`ID3` or MPEG sync)
  - OGG (`OggS`)
  - WAV (`RIFF`)
  - MP4/M4A (`ftyp` at offset 4)
- [ ] Filename safety:
  - Use `Lidarr.Plugin.Common.Utilities.FileNameSanitizer` consistently
  - Prevent multi-disc collisions (e.g., `D02T03 - ...`)
- [ ] Add unit tests that prove:
  - Text payloads are rejected
  - Container signatures match expected extension
  - Multi-disc filenames don’t collide

## Phase 2 — CLI & Local-Run Stability (Developer Experience)

Known issue class: CLI tests are often environment-dependent (networking, FFmpeg availability, credentials), which can create confusing “green but not actually tested” outcomes.

- [ ] Ensure CLI tests are explicitly gated by an env var (e.g., `RUN_REAL_CLI_TESTS=1`) and otherwise report as skipped.
- [ ] Add a local runner script that mirrors CI invocation (runsettings, filters, output paths) similar to Qobuzarr’s `run-tests.ps1` pattern.
- [ ] Introduce a “smoke” CLI run that does **not** require credentials (e.g., `--help`, diagnostics-only).
- [ ] Add runsettings hygiene:
  - sane `BlameHangTimeout`
  - stable results output directories

## Phase 3 — Docker E2E Proof (Search + Download)

Definition of Done should be staged:

- Basic: plugins show up in schema endpoints (load/discovery)
- Medium: configured indexer returns search results
- Full: download client produces files on disk (credential gated)

Work items:

- [ ] Add persistent Docker harness for Tidalarr (mirrors the Qobuzarr persisted-config runner):
  - persistent `/config` volume
  - incremental plugin rebuild + re-deploy
  - predictable port binding
- [ ] Add “search gate” script:
  - Create indexer via API
  - Trigger `AlbumSearch`
  - Assert non-empty `/api/v1/release` response
- [ ] Add “download gate” script (credential gated):
  - Trigger grab
  - Assert files exist and validate signatures on disk

## Phase 4 — Commonization (Ecosystem Scale)

Only extract what is genuinely shared across plugins:

- [ ] Shared download payload validator (magic bytes + text sniff) in `lidarr.plugin.common`
- [ ] Shared packaging test helpers:
  - strict-mode attribute (skip local / fail CI)
  - zip discovery utilities
- [ ] Shared host-version drift checker scripts

