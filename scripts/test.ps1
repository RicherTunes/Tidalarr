#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run Tidalarr tests with proper build configuration.

.DESCRIPTION
    This script ensures tests are built and run with PluginPackagingDisable=true
    to prevent ILRepack from internalizing types needed by test assertions.

.PARAMETER Filter
    Optional test filter expression (passed to dotnet test --filter).

.PARAMETER Configuration
    Build configuration (Debug or Release). Defaults to Debug.

.PARAMETER NoBuild
    Skip rebuilding before running tests.

.PARAMETER ExcludeHostBridge
    Exclude tests requiring full Lidarr assemblies (used in CI).

.PARAMETER Verbosity
    Test output verbosity (quiet, minimal, normal, detailed, diagnostic).
#>
param(
    [string]$Filter = "",
    [string]$Configuration = "Debug",
    [switch]$NoBuild,
    [switch]$ExcludeHostBridge,
    [string]$Verbosity = "normal"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path "$PSScriptRoot/.."
$testProject = Join-Path $repoRoot 'tests/Tidalarr.Tests/Tidalarr.Tests.csproj'

Push-Location $repoRoot
try {
    # IMPORTANT: Tests MUST be built with PluginPackagingDisable=true
    # Running 'dotnet test' directly will cause ILRepack to internalize types,
    # leading to MissingMethodException failures. Always use this script.
    Write-Host @"
================================================================================
  TIDALARR TEST RUNNER

  This script ensures PluginPackagingDisable=true is passed to prevent ILRepack
  from internalizing types needed by test assertions.

  DO NOT run 'dotnet test' directly - use this script instead.
================================================================================
"@ -ForegroundColor Yellow

    # MSBuild properties to pass for test builds
    # PluginPackagingDisable=true prevents ILRepack from making types internal
    $msbuildProps = @(
        "-p:PluginPackagingDisable=true"
    )

    if ($ExcludeHostBridge) {
        $msbuildProps += "-p:ExcludeHostBridge=true"
    }

    if (-not $NoBuild) {
        Write-Host "Building tests with PluginPackagingDisable=true..." -ForegroundColor Cyan
        $buildArgs = @(
            "build"
            $testProject
            "-c", $Configuration
            "-v", "minimal"
        ) + $msbuildProps

        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
    }

    Write-Host "Running tests..." -ForegroundColor Cyan
    $testArgs = @(
        "test"
        $testProject
        "-c", $Configuration
        "--no-build"
        "-v", $Verbosity
    )

    if ($Filter) {
        $testArgs += "--filter", $Filter
    }

    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
