#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run local CI verification for Tidalarr.

.DESCRIPTION
    Thin caller that passes Tidalarr-specific configuration to the shared
    local-ci.ps1 runner from Lidarr.Plugin.Common.

.EXAMPLE
    pwsh scripts/verify-local.ps1                  # Full pipeline
    pwsh scripts/verify-local.ps1 -SkipExtract     # Fast rerun (cached assemblies)
    pwsh scripts/verify-local.ps1 -SkipTests       # Build + closure only
    pwsh scripts/verify-local.ps1 -NoRestore       # Skip restore (fast iteration)
    pwsh scripts/verify-local.ps1 -IncludeSmoke    # + Docker smoke test
#>
param(
    [switch]$SkipExtract,
    [switch]$SkipTests,
    [switch]$NoRestore,
    [switch]$IncludeSmoke
)

$ErrorActionPreference = 'Stop'

# Resolve repo root (one level up from scripts/)
$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot

try {
    $config = @{
        RepoName             = 'Tidalarr'
        SolutionFile         = 'Tidalarr.sln'
        PluginCsproj         = 'src/Tidalarr/Tidalarr.csproj'
        ManifestPath         = 'plugin.json'
        MainDll              = 'Lidarr.Plugin.Tidalarr.dll'
        HostAssembliesPath   = 'ext/Lidarr/_output/net8.0'
        CommonPath           = 'ext/Lidarr.Plugin.Common'
        LidarrDockerVersion  = 'nightly-3.1.3.4970'
        BuildFlags           = @('-p:LidarrAssembliesPath={HOST_PATH}', '-p:SkipHostBridge=false')
        TestProjects         = @(
            'tests/Tidalarr.Tests/Tidalarr.Tests.csproj',
            'tests/Tidalarr.Parity.Tests/Tidalarr.Parity.Tests.csproj'
        )
        ExpectedContentsFile = 'packaging/expected-contents.txt'
        WarningBudget        = 100
        WarningBudgetEnforce = $false
        RequireHermeticTests = $true
    }

    $runner = Join-Path $config.CommonPath 'scripts/local-ci.ps1'
    if (-not (Test-Path -LiteralPath $runner)) {
        Write-Host "ERROR: Shared runner not found at: $runner" -ForegroundColor Red
        Write-Host "  Ensure Common submodule is up to date:" -ForegroundColor Yellow
        Write-Host "  git submodule update --init ext/Lidarr.Plugin.Common" -ForegroundColor Yellow
        exit 1
    }

    # F4 dropout guard: every *.Tests.csproj in the repo must be either run or skip-listed.
    # Prevents parity/compliance projects from silently disappearing from CI.
    $dropoutGuard = Join-Path $config.CommonPath 'scripts/ci/verify-test-projects-gated.ps1'
    if (Test-Path -LiteralPath $dropoutGuard) {
        & $dropoutGuard -RepoRoot $repoRoot -RunProjects $config.TestProjects -CI
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    $runnerArgs = @{ Config = $config }
    if ($SkipExtract)  { $runnerArgs['SkipExtract']  = $true }
    if ($SkipTests)    { $runnerArgs['SkipTests']    = $true }
    if ($NoRestore)    { $runnerArgs['NoRestore']    = $true }
    if ($IncludeSmoke) { $runnerArgs['IncludeSmoke'] = $true }

    & $runner @runnerArgs
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
