# Changelog

All notable changes to Tidalarr will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-05-23

### Phase 0 + Phase 1 — Ecosystem Alignment and Packaging Fix

#### Ecosystem version contract (Phase 0.3)

- Bumped `commonVersion` to `1.8.0` in `plugin.json` to align with Common v1.8.0.
- Parity-lint `VersionContract` check passes (`ecosystem-parity-lint.ps1 -Check VersionContract`).
- Plugin confirmed to target `net8.0` only; `net6.0` / `net7.0` absent from all manifests.

#### Phase 0 — packaging fatal fix

- Made packaging failure fatal in CI: the `packaging-gates.yml` workflow now blocks merges when the plugin ZIP fails assembly validation. Previously, packaging errors were advisory and slipped through.
- Regression test added for the packaging gate behavior.

#### Phase 0 — hardcoded credentials ADR

- `docs/decisions/0001-hardcoded-tidal-client-credentials.md` added: architecture decision record documenting the use of the public Tidal developer portal credentials and the migration path when per-user credentials become available.

#### Phase 0 — Docker smoke test fixes

- Docker smoke test corrected: mount path updated to the exact plugin directory, Abstractions assembly mounted separately, timeout increased for slower CI runners.
- `SkipHostBridge` build variant handled gracefully (test skips instead of failing with file-not-found).

#### Phase 1 — docs and security

- Security hardening backlog added: 10 findings, 2 High severity — see `docs/SECURITY_HARDENING_BACKLOG.md`.
- README augmented with Shared Infrastructure section (Common services consumed, version contract reference).
- Documentation section added to README with links to CHANGELOG, SECURITY, and docs/.
- Historical implementation plan files (`TIDALARR_*_PLAN*.md`) moved to `docs/archive/`.
