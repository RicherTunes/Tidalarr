param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path "$PSScriptRoot/.."
$manifestPath = Join-Path $repoRoot 'plugin.json'
$modulePath = Join-Path $repoRoot 'src/Tidalarr/Integration/TidalModule.cs'
$commonCsproj = Join-Path $repoRoot 'ext/Lidarr.Plugin.Common/src/Lidarr.Plugin.Common.csproj'

if (-not (Test-Path $manifestPath)) {
    throw "plugin.json not found at $manifestPath"
}
if (-not (Test-Path $modulePath)) {
    throw "TidalModule.cs not found at $modulePath"
}
if (-not (Test-Path $commonCsproj)) {
    throw "Lidarr.Plugin.Common.csproj not found at $commonCsproj"
}

$manifest = Get-Content -Path $manifestPath -Raw | ConvertFrom-Json

$commonVersionMatch = Select-String -Path $commonCsproj -Pattern '<Version>(?<ver>[^<]+)</Version>' | Select-Object -First 1
if (-not $commonVersionMatch) {
    throw "Unable to locate <Version> element in Lidarr.Plugin.Common.csproj"
}
$commonVersion = $commonVersionMatch.Matches[0].Groups['ver'].Value.Trim()

$moduleVersionMatch = Select-String -Path $modulePath -Pattern 'public new const string Version = "(?<ver>[^"\\]+)"' | Select-Object -First 1
if (-not $moduleVersionMatch) {
    throw "Unable to read Version constant from TidalModule.cs"
}
$moduleVersion = $moduleVersionMatch.Matches[0].Groups['ver'].Value.Trim()

$hostVersionTarget = '3.0.0.4855'
$apiMajorPattern = '^1\.x$'

$errors = @()
if (-not $manifest.version) {
    $errors += 'plugin.json missing version field.'
}
elseif ($manifest.version -ne $moduleVersion) {
    $errors += "plugin.json version '$($manifest.version)' does not match TidalModule.Version '$moduleVersion'."
}

if (-not $manifest.commonVersion) {
    $errors += 'plugin.json missing commonVersion field.'
}
elseif ($manifest.commonVersion -ne $commonVersion) {
    $errors += "plugin.json commonVersion '$($manifest.commonVersion)' does not match Lidarr.Plugin.Common.csproj version '$commonVersion'."
}

if (-not $manifest.apiVersion) {
    $errors += 'plugin.json missing apiVersion field.'
}
elseif ($manifest.apiVersion -notmatch $apiMajorPattern) {
    $errors += "plugin.json apiVersion '$($manifest.apiVersion)' does not satisfy pattern $apiMajorPattern."
}

$minHost = $manifest.minHostVersion
if (-not $minHost) {
    $errors += 'plugin.json missing minHostVersion field.'
}
elseif ($minHost -ne $hostVersionTarget) {
    $errors += "plugin.json minHostVersion '$minHost' expected '$hostVersionTarget'."
}

# `minimumVersion` is deprecated since 2026-03-01 in favor of `minHostVersion`
# (Common's packaging-gates MAN004 lint hard-bans it). Reject it here too so the
# script and the gate agree.
$legacyMin = $manifest.PSObject.Properties['minimumVersion']
if ($null -ne $legacyMin) {
    $errors += "plugin.json contains deprecated key 'minimumVersion'; use 'minHostVersion' instead (MAN004)."
}

if ($errors.Count -gt 0) {
    Write-Error ("Plugin manifest verification failed:`n - " + ($errors -join "`n - "))
    exit 1
}

Write-Host "Manifest validation succeeded: version $($manifest.version), common $($manifest.commonVersion), host $hostVersionTarget" -ForegroundColor Green

