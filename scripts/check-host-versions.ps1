param(
    [string]$HostAssembliesDir = "",
    [string]$PackagesPropsPath = "",
    [switch]$Strict
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Find-RepoRoot {
    $dir = Resolve-Path -Path $PSScriptRoot
    for ($i = 0; $i -lt 8; $i++) {
        $candidate = Join-Path $dir "Tidalarr.sln"
        if (Test-Path $candidate) {
            return $dir
        }
        $parent = Split-Path -Path $dir -Parent
        if ([string]::IsNullOrWhiteSpace($parent) -or $parent -eq $dir) {
            break
        }
        $dir = $parent
    }
    throw "Could not locate repo root (Tidalarr.sln) from '$PSScriptRoot'."
}

function Get-DefaultHostAssembliesDir([string]$repoRoot) {
    $paths = @(
        (Join-Path $repoRoot "ext/Lidarr/_output/net8.0"),
        (Join-Path $repoRoot "ext/Lidarr-docker/_output/net8.0")
    )
    foreach ($p in $paths) {
        if (Test-Path (Join-Path $p "Lidarr.dll")) {
            return $p
        }
    }
    return ""
}

function Normalize-Version([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) {
        return ""
    }

    $m = [System.Text.RegularExpressions.Regex]::Match($value, "(\d+\.\d+\.\d+)")
    if (-not $m.Success) {
        return $value.Trim()
    }
    return $m.Groups[1].Value
}

function Get-PinnedVersion([string]$packagesPropsFile, [string]$packageId) {
    [xml]$doc = Get-Content -Path $packagesPropsFile -Raw
    foreach ($node in $doc.Project.ItemGroup.PackageVersion) {
        if ($node.Include -eq $packageId) {
            return $node.Version
        }
    }
    return ""
}

function Get-HostFileVersion([string]$hostDir, [string]$fileName) {
    $path = Join-Path $hostDir $fileName
    if (-not (Test-Path $path)) {
        return ""
    }
    return ([System.Diagnostics.FileVersionInfo]::GetVersionInfo((Get-Item $path).FullName)).FileVersion
}

$repoRoot = Find-RepoRoot

if ([string]::IsNullOrWhiteSpace($PackagesPropsPath)) {
    $PackagesPropsPath = Join-Path $repoRoot "Directory.Packages.props"
}

if (-not (Test-Path $PackagesPropsPath)) {
    throw "Directory.Packages.props not found at '$PackagesPropsPath'."
}

if ([string]::IsNullOrWhiteSpace($HostAssembliesDir)) {
    $HostAssembliesDir = Get-DefaultHostAssembliesDir -repoRoot $repoRoot
}

if ([string]::IsNullOrWhiteSpace($HostAssembliesDir) -or -not (Test-Path $HostAssembliesDir)) {
    throw "Host assemblies directory not found. Provide -HostAssembliesDir, or ensure ext/Lidarr/_output/net8.0 exists."
}

$targets = @(
    @{ Name = "FluentValidation"; Dll = "FluentValidation.dll" },
    @{ Name = "NLog"; Dll = "NLog.dll" }
)

$rows = foreach ($t in $targets) {
    $pinned = Get-PinnedVersion -packagesPropsFile $PackagesPropsPath -packageId $t.Name
    $hostFileVersion = Get-HostFileVersion -hostDir $HostAssembliesDir -fileName $t.Dll
    [pscustomobject]@{
        Package = $t.Name
        Pinned = (Normalize-Version $pinned)
        Host   = (Normalize-Version $hostFileVersion)
        Match  = ((Normalize-Version $pinned) -eq (Normalize-Version $hostFileVersion))
    }
}

Write-Host ""
Write-Host "Host-version coupling check (Tidalarr)" -ForegroundColor Cyan
Write-Host "Repo root: $repoRoot" -ForegroundColor Gray
Write-Host "Host assemblies: $HostAssembliesDir" -ForegroundColor Gray
Write-Host "Pinned versions: $PackagesPropsPath" -ForegroundColor Gray
Write-Host ""
$rows | Format-Table -AutoSize

$mismatches = @($rows | Where-Object { -not $_.Match -or [string]::IsNullOrWhiteSpace($_.Pinned) -or [string]::IsNullOrWhiteSpace($_.Host) })
if ($mismatches.Count -gt 0) {
    Write-Host ""
    Write-Host "Mismatches detected:" -ForegroundColor Yellow
    $mismatches | Format-Table -AutoSize

    if ($Strict) {
        exit 1
    }
}

exit 0
