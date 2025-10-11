# Tidalarr

Tidalarr is a Lidarr plugin that indexes and downloads lossless audio directly from the Tidal service while sharing key infrastructure with the Lidarr.Plugin.Common library.

## Getting Started
- `git submodule update --init --recursive` to sync the common library and CLI dependencies.
- `dotnet restore Tidalarr.sln` then `dotnet build Tidalarr.sln` to ensure the solution compiles cleanly.
- See `TidalCLI/` for manual verification helpers and CLI tooling support.
- Host integrators: see `docs/hostbridge-integration.md` for wiring host-only settings (with NzbDrone annotations) and mapping them to core via DI.
 - For framework choices, see `docs/TFM_RATIONALE.md` (core net6.0, CLI net9.0).

## Contributor Resources
- Read the [Repository Guidelines](AGENTS.md) for coding, testing, and review expectations.
- Review `docs/` for architecture, testing, and project status background.
- Check `CLAUDE.md` if you are coordinating with Claude Code or automation agents.

## Support & Questions
Open a GitHub issue with detailed logs and reproduction steps.


## Host vs. Core
- Core plugin (`src/Tidalarr`): hostless runtime used by CLI/tests; no NzbDrone/Lidarr references. Ships in the plugin zip.
- Host bridge (`src/Tidalarr.HostBridge`): host-only wrappers with NzbDrone annotations and pretty enum labels; translates host UI models to core settings via `IHostSettingsMapper`. Not shipped in the plugin zip.
- Start here for host wiring: `docs/hostbridge-integration.md`.
 - Framework rationale: `docs/TFM_RATIONALE.md`.
