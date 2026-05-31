# Repository Guidelines

## Project Structure & Module Organization

Tidalarr implements the Lidarr streaming plugin as a layered .NET solution.

- `src/Tidalarr` houses Core models/interfaces/constants, Application services, Domain models, Infrastructure adapters, and Integration clients; add new code in the matching layer.
- `tests/Tidalarr.Tests` contains xUnit suites; `Unit/` is pure unit coverage, while root files exercise integration flows; `TestResults/` holds generated `.trx` and coverage artifacts.
- `ext/Lidarr.Plugin.Common` is a git submodule that supplies shared abstractions; update with `git submodule update --remote --merge` when needed.
- `TidalCLI/` offers the CLI harness used by integration tests and local manual verification.
- `docs/` captures architecture decisions and coverage plans; consult before introducing cross-cutting changes.

## Build, Test, and Development Commands

- `dotnet restore Tidalarr.sln` restores dependencies for the plugin, CLI, and shared library.
- `dotnet build Tidalarr.sln -c Release` compiles all projects with warnings treated as errors.
- `dotnet test Tidalarr.sln -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings --logger trx --results-directory tests/Tidalarr.Tests/TestResults` runs xUnit suites and emits Cobertura coverage into `coverage.cobertura.xml`.
- `dotnet format Tidalarr.sln` applies the repo style guardrails; run it before sending a review.
- `git submodule update --init --recursive` ensures the common library is aligned before building.

## Coding Style & Naming Conventions

- Default to four-space indentation, file-scoped namespaces, and `var` only when the type is obvious.
- Use PascalCase for types and public members, camelCase for locals, and prefix interfaces with `I`.
- Keep asynchronous APIs suffixed with `Async` and prefer cancellation tokens on long-running operations.
- Maintain nullable reference annotations and eliminate compiler warnings locally before pushing.
- Place configuration objects under `Integration` and guard new options with validation in `Infrastructure/Storage`.

## Testing Guidelines

- Write xUnit `[Fact]` tests for deterministic behaviors and `[Theory]` for matrix scenarios; reuse helpers in `tests/Tidalarr.Tests/utils`.
- Mirror the existing naming format `ComponentScenarioExpectedResult` for both files and test methods.
- Aim for the documented 100 percent line and branch coverage; investigate any drop shown in `test.trx` or Coverlet output.
- Prefer deterministic fixtures and mock external services; avoid calling live Tidal APIs during automated runs.
- Export data artifacts alongside tests when necessary and keep them under version control for reproducibility.

## Commit & Pull Request Guidelines

- Follow the Conventional Commits style seen in history (`chore(submodule): ...`, `feat(common): ...`); keep subject lines under 72 characters.
- Squash noisy fixups before pushing and ensure each commit builds and passes tests.
- Pull requests must include a short summary, linked issues using `Fixes #ID`, screenshots or logs for user-visible changes, and the latest test command output.
- Request reviews from maintainers of the touched layer and note any coverage deviations in the description.

## Security & Configuration Tips

- Never commit real Tidal credentials or tokens; consume them through secure environment configuration.
- Do not remove the `OAuth Authorization URL` settings field (`OAuthAuthUrl`); it reduces OAuth setup friction and is used as a quick "plugin loaded" triage signal. See `tidalarr/CLAUDE.md` for the exact UX constraints and expected user flow.
- Update `plugin.json` metadata, `CHANGELOG.md`, and release notes together whenever behavior changes ship.
- Validate new network endpoints through the resilience policies under `Infrastructure/Resilience` before exposing them in the module.

## Architecture Debt: TidalarrPlugin vs StreamingPlugin<,>

**Status**: TidalarrPlugin manually implements `IPlugin` instead of inheriting from `StreamingPlugin<TidalModule, TidalarrSettings>`.

**Blocker**: The CLI contract. TidalarrPlugin exposes custom diagnostics methods:

- `ValidateSettingsWithDiagnostics()` → returns `PluginOperationResult<Dictionary<string, string>>` with CFG* codes
- `ApplySettingsWithDiagnostics()` → same return type

These differ from StreamingPlugin's base `PluginValidationResult`. The CLI (Program.cs:1107-1110) directly calls these methods.

**Safe Refactor Path** (do not attempt without following this sequence):

1. Introduce `IDiagnosticsSettingsProvider` interface in Common that defines the CFG* contract
2. Make StreamingPlugin optionally implement it via adapter pattern
3. Migrate CLI to use the interface (not concrete TidalarrPlugin)
4. Switch TidalarrPlugin to inherit from StreamingPlugin<,>
5. Delete the manual IPlugin implementation

**Tripwire**: `TidalModuleSurfaceTests` asserts that deleted methods don't reappear. If this test fails after a merge, someone re-added dead code.
