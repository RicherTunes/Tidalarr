# Packaging Closure Workflow

This workflow builds and packages the Tidalarr plugin and verifies that the produced zip contains no unintended host assemblies.

## Why it exists

- Prevents shipping assemblies from the host application (e.g., other `Lidarr.*` DLLs)
- Provides a minimal, repeatable packaging gate on PRs and `main` pushes

## Current workflow

The packaging closure checks are run via `.github/workflows/packaging-gates.yml`, which delegates to Common's reusable packaging gates workflow (`Lidarr.Plugin.Common/.github/workflows/packaging-gates.yml`).

## Key practices

- Actions pinned to SHAs for supply‑chain safety
  - checkout: actions/checkout@08eba0b27e820071cde6df949e0beb9ba4906955 (v4.3.0)
  - setup-dotnet: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 (v4.3.1)
  - upload-artifact: actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02 (v4.6.2)
- Least‑privilege permissions: `permissions: { contents: read }`
- Concurrency guard: cancels overlapping runs on the same ref
- Submodule auth via HTTPS using `GITHUB_TOKEN`, scoped to repo (`git config --local`)
- Cleanup of URL rewrites after submodule init

## NuGet caching

The workflow enables `setup-dotnet` caching with `cache: true` and `cache-dependency-path: **/*.csproj` to speed up restores in CI.

## Formatting check

We run a non‑blocking formatting verification limited to this repo:

```
dotnet format whitespace Tidalarr.sln --verify-no-changes -v minimal --exclude ext --exclude temp --exclude 'src/Tidalarr/Integration/LidarrNative'
```

The check excludes the `ext/` submodule, `temp/` content, and specific test files to avoid false positives from external sources and generated code. If you want to gate on formatting, remove `continue-on-error: true` from the CI workflow step.

## Submodule Pin

- The file `ext-common-sha.txt` contains a full 40-character commit SHA (the submodule HEAD). The Submodule Pinning workflow (`.github/workflows/submodule-pin.yml`) verifies this matches the recorded submodule gitlink and fails CI if they drift. Update it via `pwsh ext/Lidarr.Plugin.Common/scripts/repin-common-submodule.sh --sha-from-submodule --stage`.
