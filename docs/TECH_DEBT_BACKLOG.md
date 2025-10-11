# Tech Debt Backlog

This document tracks actionable tech-debt items with acceptance criteria.

## Hot Fixes (done)
- Remove stray logs (out.txt, err.txt) and ignore entries
  - [x] Deleted committed files
  - [x] .gitignore updated
- Conditional CLI test skipping
  - [x] Introduced `CliFactAttribute` with `RUN_REAL_CLI_TESTS` gate
  - [x] Replaced hard Skips in CLI tests

## High Priority (next sprint)
1) Trim unused Polly packages (verify usage)
- Context: `TidalResiliencePolicy` still references Polly; runtime retries now use Common `ExecuteWithRetryAsync`.
- Criteria:
  - [ ] If `TidalResiliencePolicy` is kept, leave Polly refs and annotate the class `[Obsolete]` with rationale; or
  - [ ] If removed/migrated, delete `Polly` and `Polly.Extensions.Http` from `src/Tidalarr/Tidalarr.csproj` and update tests accordingly.
  - [ ] Build/tests green.

2) HostBridge → core mapping tests
- Criteria:
  - [x] Unit tests cover `ToCore()` for all host settings + DI registration.
  - [ ] Include negative/edge cases (nulls/defaults) in a follow-up.

3) Path validation parity
- Criteria:
  - [ ] Add tests for UNC, long paths, invalid chars, relative paths.
  - [ ] Decide whether to move validation into Common.

4) Packaging dependency-closure CI gate
- Criteria:
  - [ ] CI job runs `build.ps1 -Package` for net6.0.
  - [ ] Fails if zip contains disallowed host assemblies (allowlist: `Lidarr.Plugin.Tidalarr.dll`, `Lidarr.Plugin.Common.dll`).

5) Reduce settings duplication
- Criteria:
  - [ ] Extract shared display metadata (labels/orders) as constants used by Core + HostBridge; or
  - [ ] Add a small source generator to emit HostBridge wrappers from Core definitions.
  - [ ] Build/tests green.

## Medium Priority
- Multi-target TFMs rationale
  - [ ] Document current choice (Core net6.0, CLI net9.0) in docs, or align TFMs.
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
