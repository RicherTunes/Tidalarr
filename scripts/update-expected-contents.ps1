#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Update or verify packaging/expected-contents.txt from the latest Tidalarr package.
#>
param(
    [switch]$Update,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$sharedUpdater = Join-Path $repoRoot 'ext/Lidarr.Plugin.Common/scripts/update-plugin-expected-contents.ps1'

& $sharedUpdater `
    -RepoPath $repoRoot `
    -Csproj 'src/Tidalarr/Tidalarr.csproj' `
    -RequireCanonicalAbstractions `
    -Update:$Update `
    -Check:$Check

$lastExitCodeVariable = Get-Variable -Name LASTEXITCODE -Scope Global -ErrorAction SilentlyContinue
if ($lastExitCodeVariable -and $null -ne $lastExitCodeVariable.Value -and $lastExitCodeVariable.Value -ne 0) {
    exit $lastExitCodeVariable.Value
}
