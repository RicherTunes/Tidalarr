param(
    [switch]$SkipPackage,
    [switch]$IncludeCliTests,
    # When set (CI extracts real Lidarr host assemblies via Docker first, matching the
    # qobuz/apple/brainarr approach), build the FULL solution including the host-bridge
    # (LidarrNative) classes. Without it, fall back to the host stub + -SkipHostBridge for
    # local/Docker-less runs, which excludes LidarrNative.
    [switch]$UseRealHostAssemblies
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    $global:LASTEXITCODE = 0
    & $Command
    if ($global:LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $global:LASTEXITCODE"
    }
}

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

    $skipHostBridge = -not $UseRealHostAssemblies
    if ($UseRealHostAssemblies) {
        Write-Host "Using real Lidarr host assemblies; building full solution (incl. host-bridge/LidarrNative)" -ForegroundColor Cyan
        if (-not (Test-Path $hostOutput) -or -not (Get-ChildItem -Path $hostOutput -Filter *.dll -File -ErrorAction SilentlyContinue)) {
            throw "-UseRealHostAssemblies set but no host assemblies found at $hostOutput (extract them first, e.g. via Docker)."
        }
    }
    else {
        Write-Host "Generating host stub assemblies at $hostOutput (host-bridge/LidarrNative excluded)" -ForegroundColor Cyan
        & $prepareStub -OutputPath $hostOutput
    }

    if ($UseRealHostAssemblies) {
        # verify-assemblies enforces FileVersion == AssemblyVersion, which holds for the
        # single generated stub but NOT for real Lidarr assemblies (392 DLLs incl. third-party
        # deps legitimately differ). Skip the stub-integrity check on the real-assembly path.
        Write-Host "Skipping stub FileVersion==AssemblyVersion check (using real host assemblies)" -ForegroundColor Cyan
    }
    else {
        Write-Host "Validating host stub assembly" -ForegroundColor Cyan
        $stubAssembly = Join-Path $hostOutput 'Lidarr.HostStub.dll'
        if (-not (Test-Path $stubAssembly)) {
            throw "Host stub assembly was not generated at $stubAssembly"
        }

        $fileVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($stubAssembly).FileVersion
        $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($stubAssembly).Version.ToString()
        if ([string]::IsNullOrWhiteSpace($fileVersion) -or $fileVersion -ne $assemblyVersion) {
            throw "Host stub version mismatch at ${stubAssembly}: FileVersion=$fileVersion AssemblyVersion=$assemblyVersion"
        }
    }

    Write-Host "Verifying plugin manifest alignment" -ForegroundColor Cyan
    & $verifyPlugin

    Write-Host "Restoring solution" -ForegroundColor Cyan
    Invoke-Checked "Solution restore" { dotnet restore "$repoRoot/Tidalarr.sln" }

    if ($skipHostBridge) {
        Write-Host "Restoring hostless plugin assets" -ForegroundColor Cyan
        Invoke-Checked "Hostless plugin restore" {
            dotnet restore "$repoRoot/src/Tidalarr/Tidalarr.csproj" -p:SkipHostBridge=true
        }
    }

    Write-Host "Building plugin (Release configuration; SkipHostBridge=$skipHostBridge)" -ForegroundColor Cyan
    if ($skipHostBridge) {
        # Stub mode: exclude LidarrNative files that require real Lidarr host assemblies
        Invoke-Checked "Plugin build" {
            & "$repoRoot/build.ps1" -Configuration Release -NoBuild:$false -SkipHostBridge
        }
    }
    else {
        # Real host assemblies present: full build including host-bridge (LidarrNative)
        Invoke-Checked "Plugin build" {
            & "$repoRoot/build.ps1" -Configuration Release -NoBuild:$false
        }
    }

    # Produce package via shared PluginPack so CLI-scope packaging tests can validate the artifact
    $pluginPackagePath = $null
    try {
        Write-Host "Packaging plugin via PluginPack" -ForegroundColor Cyan
        $modulePath = Join-Path $repoRoot 'ext/Lidarr.Plugin.Common/tools/PluginPack.psm1'
        Import-Module $modulePath -Force
        $manifestPath = Join-Path $repoRoot 'plugin.json'
        $csproj = Join-Path $repoRoot 'src/Tidalarr/Tidalarr.csproj'
        $pluginPackagePath = New-PluginPackage -Csproj $csproj -Manifest $manifestPath -Framework 'net8.0' -Configuration 'Release'
    } catch {
        throw "Packaging step failed: $_"
    }

    Write-Host "Running tests (Release configuration) via unified runner" -ForegroundColor Cyan

    # Use the unified test runner from Common
    $unifiedRunner = Join-Path $commonScripts 'test.ps1'
    if (-not (Test-Path $unifiedRunner)) {
        throw "Unified test runner not found at: $unifiedRunner"
    }

    $testProject = Join-Path $repoRoot 'tests/Tidalarr.Tests/Tidalarr.Tests.csproj'

    # Build test project separately with SkipHostBridge since unified runner doesn't pass
    # Properties to its build step (only to dotnet test).
    # Build hardening: -m:1 prevents parallel MSBuild nodes from file-locking shared obj/ files.
    $env:DOTNET_CLI_DISABLE_BUILD_SERVERS = "1"
    $env:MSBUILDDISABLENODEREUSE = "1"
    $hb = if ($skipHostBridge) { 'true' } else { 'false' }
    Write-Host "Restoring test project (SkipHostBridge=$hb)..." -ForegroundColor Cyan
    Invoke-Checked "Test project restore" {
        dotnet restore $testProject -p:SkipHostBridge=$hb -p:ExcludeHostBridge=$hb
    }

    Write-Host "Building test project (SkipHostBridge=$hb) + build hardening..." -ForegroundColor Cyan
    Invoke-Checked "Test project build" {
        dotnet build $testProject -c Release --no-restore -v minimal `
            -p:RunAnalyzersDuringBuild=false -p:EnableNETAnalyzers=false -p:TreatWarningsAsErrors=false `
            -p:SkipHostBridge=$hb -p:ExcludeHostBridge=$hb `
            /m:1 /p:BuildInParallel=false /p:UseSharedCompilation=false
    }

    $additionalFilters = @('Category!=ReleaseE2E')
    if ($skipHostBridge) {
        $additionalFilters += @('Category!=Docker', 'Category!=DockerE2E')
    }
    if (-not $IncludeCliTests) {
        $additionalFilters += 'scope!=cli'
    }

    $testArgs = @{
        TestProject = $testProject
        Configuration = 'Release'
        CI = $true
        NoBuild = $true  # Already built above with SkipHostBridge
    }

    if ($IncludeCliTests) {
        Write-Host "Including CLI-scope tests (scope=cli)" -ForegroundColor Yellow
    }
    else {
        Write-Host "Excluding CLI-scope tests (scope=cli) for PR/CI runs" -ForegroundColor Yellow
    }

    if ($additionalFilters.Count -gt 0) {
        $testArgs['AdditionalFilter'] = ($additionalFilters -join '&')
    }

    Invoke-Checked "Unified tests" {
        & $unifiedRunner @testArgs
    }

    if (-not $SkipPackage) {
        $artifactsDir = Join-Path $repoRoot 'artifacts'
        if (-not (Test-Path $artifactsDir)) {
            New-Item -ItemType Directory -Path $artifactsDir | Out-Null
        }

        if (-not $pluginPackagePath -or -not (Test-Path $pluginPackagePath)) {
            throw "Canonical package was not produced by New-PluginPackage."
        }

        $manifest = Get-Content -Path 'plugin.json' -Raw | ConvertFrom-Json
        $packageName = "Lidarr.Plugin.Tidalarr-v$($manifest.version).net8.0.zip"
        $packagePath = Join-Path $artifactsDir $packageName

        if (Test-Path $packagePath) {
            Remove-Item -Path $packagePath -Force
        }

        Write-Host "Creating artifact $packageName from $pluginPackagePath" -ForegroundColor Cyan
        Copy-Item -Path $pluginPackagePath -Destination $packagePath -Force
    }
}
finally {
    Pop-Location
}




