#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Run the Tidalarr Docker E2E smoke harness against a real Lidarr container.

.DESCRIPTION
    Thin shim — delegates to the shared runner in Lidarr.Plugin.Common.

.PARAMETER SkipBuild
    Skip the verify-local.ps1 build prep step.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER Filter
    Optional xUnit filter override. Defaults to Category=DockerE2E.

.EXAMPLE
    pwsh scripts/e2e.ps1
    pwsh scripts/e2e.ps1 -SkipBuild
    pwsh scripts/e2e.ps1 -Filter 'FullyQualifiedName~Indexer_Test'
#>
param(
    [switch]$SkipBuild,
    [string]$Configuration = 'Release',
    [string]$Filter = 'Category=DockerE2E'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path "$PSScriptRoot/.."
Set-Location $repoRoot

& "$PSScriptRoot/../ext/Lidarr.Plugin.Common/scripts/e2e-local-runner.ps1" `
    -PluginName 'Tidalarr' `
    -TestProject 'tests/Tidalarr.Tests/Tidalarr.Tests.csproj' `
    -SkipBuild:$SkipBuild `
    -Configuration $Configuration `
    -Filter $Filter

exit $LASTEXITCODE
