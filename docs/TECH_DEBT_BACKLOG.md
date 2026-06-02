# Tech Debt Backlog

This document tracks actionable tech-debt items with acceptance criteria.

## Done

- Remove stray logs (out.txt, err.txt) and ignore entries
  - [x] Deleted committed files
  - [x] .gitignore updated
- Conditional CLI test skipping
  - [x] Introduced `CliFactAttribute` with `RUN_REAL_CLI_TESTS` gate
  - [x] Replaced hard Skips in CLI tests
- ~~Trim unused Polly packages~~ (resolved)
  - [x] Polly no longer referenced in `Tidalarr.csproj`.
  - [x] Stale `TidalResiliencePolicy` references cleaned up.
  - [x] Build/tests green.

## High Priority (next sprint)

1) HostBridge → core mapping tests

- Criteria:
  - [x] Unit tests cover `ToCore()` for all host settings + DI registration.
  - [ ] Include negative/edge cases (nulls/defaults) in a follow-up.

1) Path validation parity

- Criteria:
  - [ ] Add tests for UNC, long paths, invalid chars, relative paths.
  - [ ] Decide whether to move validation into Common.

1) Packaging dependency-closure CI gate

- Criteria:
  - [ ] CI job runs `build.ps1 -Package` for net8.0.
  - [ ] Fails if zip contains disallowed host assemblies (allowed: `Lidarr.Plugin.Tidalarr.dll` + `plugin.json`; `Lidarr.Plugin.Common.dll` must NOT be present — it is ILRepack-merged into the plugin DLL).

1) Reduce settings duplication

- Criteria:
  - [ ] Extract shared display metadata (labels/orders) as constants used by Core + HostBridge; or
  - [ ] Add a small source generator to emit HostBridge wrappers from Core definitions.
  - [ ] Build/tests green.

## Medium Priority

- Multi-target TFMs rationale
  - [x] Core now targets net8.0 to match the Lidarr plugins-branch host. See docs/TFM_RATIONALE.md (updated).
- Diagnostics JSON contract
  - [ ] Add snapshot tests for CFG000/IX200/DL100 shapes.
  - [ ] Document schema fields/ids in docs.
- CLI argument validation hardening
  - [ ] Improve errors for invalid enums/args; add tests.

## Low Priority

- Observability alignment in Common
  - [ ] Propose shared event IDs and minimal set of logging patterns.
- Enum mapping helpers
  - [ ] Utility to map host enum ↔ core enum without manual switch.
- Submodule pinning guard
  - [ ] CI step validates `ext/Lidarr.Plugin.Common` SHA vs. required version.
