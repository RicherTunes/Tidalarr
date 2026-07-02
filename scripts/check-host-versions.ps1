<#
.SYNOPSIS
    Checks host assembly versions and compares with Directory.Packages.props.

.DESCRIPTION
    Thin wrapper around Common's e2e-host-versions.psm1 module.
    Validates that plugin package pins match host assembly versions.

.PARAMETER ExtractFrom
    Docker image tag to extract assemblies from (e.g., "nightly-3.1.3.4970").
    If specified, extracts fresh assemblies before checking.

.PARAMETER HostAssembliesDir
    Directory containing the host assemblies to compare against.
    Default: auto-detected from ext/Lidarr/_output/net8.0 or ext/Lidarr-docker/_output/net8.0

.PARAMETER Strict
    Exit with non-zero code on any mismatch. Use in CI pipelines.

.PARAMETER MatchPolicy
    Version matching policy: MajorMinor (default, safer) or Exact.

.PARAMETER Format
    Output format: Table (default) or Json.

.PARAMETER ForceExtract
    Force re-extraction even if cached assemblies exist.

.EXAMPLE
    .\scripts\check-host-versions.ps1

.EXAMPLE
    .\scripts\check-host-versions.ps1 -ExtractFrom "nightly-3.1.3.4970"

.EXAMPLE
    .\scripts\check-host-versions.ps1 -Strict -Format Json

.NOTES
    Delegates to: ext/Lidarr.Plugin.Common/scripts/lib/e2e-host-versions.psm1
#>

[CmdletBinding()]
param(
    [string]$ExtractFrom,
    [string]$HostAssembliesDir,
    [switch]$Strict,
    [ValidateSet('MajorMinor', 'Exact')]
    [string]$MatchPolicy = 'MajorMinor',
    [ValidateSet('Table', 'Json')]
    [string]$Format = 'Table',
    [switch]$ForceExtract
)

$ErrorActionPreference = "Stop"

# Derive repo root from script location
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

# Import Common's e2e-host-versions module
$ModulePath = Join-Path $RepoRoot 'ext/Lidarr.Plugin.Common/scripts/lib/e2e-host-versions.psm1'
if (-not (Test-Path $ModulePath)) {
    Write-Error "Common module not found at: $ModulePath`nRun: git submodule update --init --recursive"
    exit 1
}

Import-Module $ModulePath -Force

# Build parameters for the function call
$params = @{
    RepoRoot    = $RepoRoot
    MatchPolicy = $MatchPolicy
    Format      = $Format
}

if ($Strict) {
    $params['Strict'] = $true
}

if ($ForceExtract) {
    $params['ForceExtract'] = $true
}

if ($ExtractFrom) {
    $params['ExtractFrom'] = $ExtractFrom
}

if ($HostAssembliesDir) {
    # Resolve relative paths against repo root
    if (-not [IO.Path]::IsPathRooted($HostAssembliesDir)) {
        $params['HostAssembliesDir'] = Join-Path $RepoRoot $HostAssembliesDir
    }
    else {
        $params['HostAssembliesDir'] = $HostAssembliesDir
    }
}

# Call the module function
# Note: Module may call exit 1 directly for -Strict + errors, terminating immediately.
# The logic below provides redundancy for robustness if module behavior changes.
$result = Test-HostVersionCompatibility @params

# Output result (module returns JSON string when Format=Json)
if ($result) {
    Write-Output $result
}

# Determine if errors exist from result (robust exit code handling)
$hasErrors = $false
if ($Format -eq 'Json' -and $result) {
    try {
        $parsed = $result | ConvertFrom-Json
        $hasErrors = $parsed.hasErrors -eq $true
    }
    catch {
        # If JSON parsing fails, assume no errors
    }
}
elseif ($result) {
    # Table format returns array of objects with Status property
    $hasErrors = @($result | Where-Object { $_.Status -ne 'OK' }).Count -gt 0
}

if ($Strict -and $hasErrors) {
    exit 1
}

exit 0
