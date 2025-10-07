param(
    [switch]$SkipPackage
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path "$PSScriptRoot/.."
Push-Location $repoRoot
try {
    $commonScripts = Join-Path $repoRoot 'ext/Lidarr.Plugin.Common/scripts'
    $hostOutput = Join-Path $repoRoot 'ext/Lidarr/_output/net6.0'

    if (-not (Test-Path $commonScripts)) {
        throw "Lidarr.Plugin.Common scripts directory not found at $commonScripts"
    }

    $prepareStub = Join-Path $commonScripts 'prepare-host-stub.ps1'
    $verifyAssemblies = Join-Path $commonScripts 'verify-assemblies.ps1'
    $verifyPlugin = Join-Path $repoRoot 'scripts/verify-plugin.ps1'

    if (-not (Test-Path $hostOutput) -or -not (Get-ChildItem -Path $hostOutput -Filter *.dll -File -ErrorAction SilentlyContinue)) {
        Write-Host "Generating host stub assemblies at $hostOutput" -ForegroundColor Cyan
        & $prepareStub -OutputPath $hostOutput
    }

    Write-Host "Validating host assemblies" -ForegroundColor Cyan
    & $verifyAssemblies

    Write-Host "Verifying plugin manifest alignment" -ForegroundColor Cyan
    & $verifyPlugin

    Write-Host "Restoring solution" -ForegroundColor Cyan
    dotnet restore "$repoRoot/Tidalarr.sln"

    Write-Host "Building plugin (Release configuration)" -ForegroundColor Cyan
    & "$repoRoot/build.ps1" -Configuration Release

    Write-Host "Running tests (Release configuration)" -ForegroundColor Cyan
    dotnet test "$repoRoot/Tidalarr.sln" -c Release --no-build

    if (-not $SkipPackage) {
        $artifactsDir = Join-Path $repoRoot 'artifacts'
        if (-not (Test-Path $artifactsDir)) {
            New-Item -ItemType Directory -Path $artifactsDir | Out-Null
        }

        $manifest = Get-Content -Path 'plugin.json' -Raw | ConvertFrom-Json
        $packageName = "Tidalarr-$($manifest.version).zip"
        $packagePath = Join-Path $artifactsDir $packageName

        $outputDir = Join-Path $repoRoot 'src/Tidalarr/bin/Release/net6.0'
        $payload = @(
            Join-Path $outputDir 'Lidarr.Plugin.Tidalarr.dll'
            Join-Path $outputDir 'Lidarr.Plugin.Tidalarr.pdb'
            Join-Path $outputDir 'plugin.json'
        ) | Where-Object { Test-Path $_ }

        if ($payload.Count -eq 0) {
            throw "No build outputs found under $outputDir"
        }

        if (Test-Path $packagePath) {
            Remove-Item -Path $packagePath -Force
        }

        Write-Host "Creating artifact $packageName" -ForegroundColor Cyan
        Compress-Archive -Path $payload -DestinationPath $packagePath
    }
}
finally {
    Pop-Location
}




