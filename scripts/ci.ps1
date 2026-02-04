param(
    [switch]$SkipPackage,
    [switch]$IncludeCliTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path "$PSScriptRoot/.."
Push-Location $repoRoot
try {
    $commonScripts = Join-Path $repoRoot 'ext/Lidarr.Plugin.Common/scripts'
    $hostOutput = Join-Path $repoRoot 'ext/Lidarr/_output/net8.0'

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
    # SkipHostBridge excludes LidarrNative files that require Lidarr host assemblies
    & "$repoRoot/build.ps1" -Configuration Release -NoBuild:$false -SkipHostBridge

    # Produce package via shared PluginPack so CLI-scope packaging tests can validate the artifact
    try {
        Write-Host "Packaging plugin via PluginPack" -ForegroundColor Cyan
        $modulePath = Join-Path $repoRoot 'ext/Lidarr.Plugin.Common/tools/PluginPack.psm1'
        Import-Module $modulePath -Force
        $manifestPath = Join-Path $repoRoot 'plugin.json'
        $csproj = Join-Path $repoRoot 'src/Tidalarr/Tidalarr.csproj'
        $null = New-PluginPackage -Csproj $csproj -Manifest $manifestPath -Framework 'net8.0' -Configuration 'Release'
    } catch {
        Write-Warning "Packaging step failed: $_"
        if ($IncludeCliTests) { throw }
    }

    Write-Host "Running tests (Release configuration) via unified runner" -ForegroundColor Cyan

    # Use the unified test runner from Common
    $unifiedRunner = Join-Path $commonScripts 'test.ps1'
    if (-not (Test-Path $unifiedRunner)) {
        throw "Unified test runner not found at: $unifiedRunner"
    }

    $testProject = Join-Path $repoRoot 'tests/Tidalarr.Tests/Tidalarr.Tests.csproj'

    # Build test project separately with SkipHostBridge since unified runner doesn't pass
    # Properties to its build step (only to dotnet test)
    Write-Host "Building test project with SkipHostBridge..." -ForegroundColor Cyan
    dotnet build $testProject -c Release --no-restore -v minimal `
        -p:RunAnalyzersDuringBuild=false -p:EnableNETAnalyzers=false -p:TreatWarningsAsErrors=false `
        -p:SkipHostBridge=true -p:ExcludeHostBridge=true

    $testArgs = @{
        TestProject = $testProject
        Configuration = 'Release'
        CI = $true
        NoBuild = $true  # Already built above with SkipHostBridge
    }

    if ($IncludeCliTests) {
        Write-Host "Including CLI-scope tests (scope=cli)" -ForegroundColor Yellow
        # No additional filter - run all tests
    }
    else {
        Write-Host "Excluding CLI-scope tests (scope=cli) for PR/CI runs" -ForegroundColor Yellow
        $testArgs['AdditionalFilter'] = 'scope!=cli'
    }

    & $unifiedRunner @testArgs

    if (-not $SkipPackage) {
        $artifactsDir = Join-Path $repoRoot 'artifacts'
        if (-not (Test-Path $artifactsDir)) {
            New-Item -ItemType Directory -Path $artifactsDir | Out-Null
        }

        $manifest = Get-Content -Path 'plugin.json' -Raw | ConvertFrom-Json
        $packageName = "Tidalarr-$($manifest.version).zip"
        $packagePath = Join-Path $artifactsDir $packageName

        $outputDir = Join-Path $repoRoot 'src/Tidalarr/bin/Release/net8.0'
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




