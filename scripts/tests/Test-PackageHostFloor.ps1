param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [version]$ExpectedHostFloor = [version]'3.1.2.4913'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$packModule = Join-Path $repoRoot 'ext\Lidarr.Plugin.Common\tools\PluginPack.psm1'

if (-not (Test-Path -LiteralPath $packModule)) {
    throw "Common packaging module not found: $packModule"
}

Import-Module $packModule -Force

# This metadata-only gate enumerates every host reference named Lidarr or
# Lidarr.* while excluding plugin-owned Lidarr.Plugin.* assemblies. It also
# rejects a manifest floor newer than the selected host.
Assert-PluginPackageIdentity `
    -ZipPath $PackagePath `
    -ValidateHostRequirements `
    -HostVersion $ExpectedHostFloor

Write-Host "PASS: Package host references and manifest support floor $ExpectedHostFloor"
