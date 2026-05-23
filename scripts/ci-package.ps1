<#
.SYNOPSIS
    Thin packaging helper: imports PluginPack and calls New-PluginPackage.

.DESCRIPTION
    Extracted from ci.ps1 so the packaging step can be unit-tested in isolation
    (ci-packaging-runtime.Tests.ps1) without requiring a full build environment.

    ci.ps1 dot-sources this script after building the plugin.  Any failure here
    propagates as a terminating error because $ErrorActionPreference = 'Stop' is
    inherited from the caller.

.PARAMETER RepoRoot
    Absolute path to the repository root.  Defaults to the parent of this script's
    directory (i.e. the standard layout).

.PARAMETER ModulePath
    Override the PluginPack module path (used by tests to inject a shadow module).
#>
param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot/.."),
    [string]$ModulePath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $ModulePath) {
    $ModulePath = Join-Path $RepoRoot 'ext/Lidarr.Plugin.Common/tools/PluginPack.psm1'
}

Write-Host "Packaging plugin via PluginPack ($ModulePath)" -ForegroundColor Cyan

Import-Module $ModulePath -Force

$manifestPath = Join-Path $RepoRoot 'plugin.json'
$csproj       = Join-Path $RepoRoot 'src/Tidalarr/Tidalarr.csproj'

$null = New-PluginPackage -Csproj $csproj -Manifest $manifestPath -Framework 'net8.0' -Configuration 'Release'

Write-Host "Packaging succeeded." -ForegroundColor Green
