# Qobuzarr Streaming Module Adoption (2025-09-26)

- Qobuzarr now imports the shared `PluginPackaging.targets` file and resolves `VERSION` via `$(MSBuildProjectDirectory)`, so `dotnet build Qobuzarr.csproj` works from the repository root without bespoke ILRepack wiring.
- Dependency injection is centralised in `QobuzModule : StreamingPluginModule`; CLI and integration tests call `QobuzModule.BuildServiceProvider(...)` instead of hand-built `ServiceCollection` scaffolding.

