param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$Deploy,
    [string]$DeployPath = "",

    [switch]$Clean,
    [switch]$Restore,
    [switch]$NoBuild,
    [switch]$Package,
    [switch]$VerboseOutput,
    [switch]$UsePrebuiltAssemblies,
    [string]$LidarrVersion = "2.13.2.4685",
    [switch]$Help
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $PSCommandPath
Push-Location $scriptRoot

function Show-Help {
    Write-Host "🔨 Tidalarr Build Script" -ForegroundColor Green
    Write-Host ""
    Write-Host "USAGE:" -ForegroundColor Cyan
    Write-Host "  .\build.ps1 [options]" -ForegroundColor White
    Write-Host ""
    Write-Host "CONFIGURATIONS:" -ForegroundColor Cyan
    Write-Host "  Debug                 Debug build with symbols (default)" -ForegroundColor White
    Write-Host "  Release               Optimized release build" -ForegroundColor White
    Write-Host ""
    Write-Host "OPTIONS:" -ForegroundColor Cyan
    Write-Host "  -Deploy               Auto-deploy to test Lidarr instance" -ForegroundColor White
    Write-Host "  -DeployPath <path>    Custom deployment path" -ForegroundColor White
    Write-Host "  -Clean                Clean before building" -ForegroundColor White
    Write-Host "  -Restore              Force restore packages" -ForegroundColor White
    Write-Host "  -NoBuild              Skip build (use with -Clean/-Restore)" -ForegroundColor White
    Write-Host "  -VerboseOutput        Show detailed build output" -ForegroundColor White
    Write-Host "  -Package             Create a distributable zip using shared tooling" -ForegroundColor White
    Write-Host "  -UsePrebuiltAssemblies Build against pre-built Lidarr binaries" -ForegroundColor White
    Write-Host "  -LidarrVersion        Target Lidarr version when overriding assemblies" -ForegroundColor White
    Write-Host "  -Help                 Show this help" -ForegroundColor White
    Write-Host ""
    Write-Host "EXAMPLES:" -ForegroundColor Cyan
    Write-Host "  .\build.ps1                          # Debug build" -ForegroundColor Gray
    Write-Host "  .\build.ps1 Release                  # Release build" -ForegroundColor Gray
    Write-Host "  .\build.ps1 -Deploy                  # Debug build + auto-deploy" -ForegroundColor Gray
    Write-Host "  .\build.ps1 Release -Deploy          # Release build + deploy" -ForegroundColor Gray
    Write-Host "  .\build.ps1 -Clean -Restore          # Clean, restore, and build" -ForegroundColor Gray
    Write-Host "  .\build.ps1 -DeployPath C:\Test      # Deploy to custom location" -ForegroundColor Gray
    Write-Host "  .\build.ps1 -UsePrebuiltAssemblies   # Use CI approach" -ForegroundColor Gray
    Write-Host ""
    Write-Host "DEFAULT DEPLOY PATH:" -ForegroundColor Cyan
    Write-Host "  $defaultDeployPath" -ForegroundColor Gray
    Write-Host ""
}

if ($Help) {
    Show-Help
    Pop-Location
    exit 0
}

if (-not (Test-Path "Tidalarr.sln")) {
    Write-Host "❌ Error: Please run this script from the Tidalarr repository root" -ForegroundColor Red
    Write-Host "   Current directory: $(Get-Location)" -ForegroundColor Yellow
    Pop-Location
    exit 1
}

$pluginProject = "src/Tidalarr/Tidalarr.csproj"
# Default to folder name that matches plugin 'id' under the container's plugins root mapping.
# Hotio/Lidarr expects '/config/plugins/<plugin-id>' (case-sensitive on Linux).
# Default to the path used in our hotio test container mapping
# Keep this aligned with your local volume mapping
# Use a folder exactly matching the manifest id for best compatibility
$defaultDeployPath = "X:/lidarr-hotio-plugins-test/plugins/tidalarr"

# Read plugin id and name from manifest to normalize deploy paths
$manifestPath = Join-Path $scriptRoot 'plugin.json'
$pluginId = "tidalarr"
$pluginName = "Tidalarr"
try {
    if (Test-Path $manifestPath) {
        $mf = Get-Content $manifestPath -Raw | ConvertFrom-Json
        if ($mf.id) { $pluginId = "$($mf.id)" }
        if ($mf.name) { $pluginName = "$($mf.name)" }
    }
}
catch { }

function Resolve-DeployPath([string] $inputPath) {
    if ([string]::IsNullOrWhiteSpace($inputPath)) { return $defaultDeployPath }
    $leaf = Split-Path -Path $inputPath -Leaf
    if ($leaf -ieq 'plugins') { return Join-Path $inputPath $pluginId }
    if ($leaf -ieq 'RicherTunes') { return Join-Path $inputPath $pluginName }
    if ($leaf -ieq $pluginId -or $leaf -ieq $pluginName) { return $inputPath }
    return Join-Path $inputPath $pluginId
}

Write-Host "🔨 Building Tidalarr Plugin" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan

if ($Clean) {
    Write-Host ""
    Write-Host "🧹 Cleaning solution..." -ForegroundColor Blue
    try {
        dotnet clean src/Tidalarr/Tidalarr.csproj --configuration  $Configuration --verbosity minimal 
        Write-Host "✅ Clean complete" -ForegroundColor Green
    }
    catch {
        Write-Host "⚠️ Clean failed: $_" -ForegroundColor Yellow
    }
}

if ($Restore -or -not (Test-Path "packages.lock.json")) {
    Write-Host ""
    Write-Host "📦 Restoring packages..." -ForegroundColor Blue
    try {
        dotnet restore Tidalarr.sln --verbosity minimal
        Write-Host "✅ Restore complete" -ForegroundColor Green
    }
    catch {
        Write-Host "❌ Restore failed: $_" -ForegroundColor Red
        Pop-Location
        exit 1
    }
}

if (-not $NoBuild) {
    Write-Host ""
    Write-Host "🔧 Preparing build" -ForegroundColor Blue

    if (-not $UsePrebuiltAssemblies -and (Test-Path "ext/Lidarr-source/src/Directory.Build.props")) {
        Write-Host "🔧 Lidarr sources detected. Target assembly version: $LidarrVersion" -ForegroundColor Blue
    }
    elseif ($UsePrebuiltAssemblies) {
        Write-Host "📦 Using pre-built Lidarr assemblies" -ForegroundColor Blue
    }

    $buildParams = @(
        $pluginProject,
        "--configuration", $Configuration,
        "--no-restore",
        "-p:RunAnalyzersDuringBuild=false",
        "-p:EnableNETAnalyzers=false",
        "-p:TreatWarningsAsErrors=false"
    )

    if (-not $UsePrebuiltAssemblies -and (Test-Path "ext/Lidarr-source/src/Directory.Build.props")) {
        $buildParams += "-p:LidarrAssemblyVersion=$LidarrVersion"
    }

    if ($Deploy) {
        $requestedPath = if ([string]::IsNullOrWhiteSpace($DeployPath)) { $defaultDeployPath } else { $DeployPath }
        $deployTarget = Resolve-DeployPath $requestedPath
        # Validate that the target root exists to avoid silent mis-deploys
        try { $targetRoot = Split-Path -Path $deployTarget -Qualifier } catch { $targetRoot = $null }
        if (-not [System.IO.Path]::IsPathRooted($deployTarget) -or [string]::IsNullOrWhiteSpace($targetRoot) -or -not (Test-Path $targetRoot)) {
            Write-Host "? Deploy path appears invalid or the drive/share is unavailable:" -ForegroundColor Red
            Write-Host "   $deployTarget" -ForegroundColor Yellow
            Write-Host "   Tip: mount the drive/share, or pass -DeployPath to override." -ForegroundColor Yellow
            Pop-Location
            exit 1
        }
        $buildParams += "-p:EnablePluginDeployment=true"
        $buildParams += "-p:LidarrPluginDeployPath=\"$deployTarget\""
        Write-Host "🚀 Plugin deployment enabled" -ForegroundColor Cyan
        Write-Host "📁 Deploy path: $deployTarget" -ForegroundColor Cyan
    }

    if ($VerboseOutput) {
        $buildParams += "--verbosity", "normal"
    }
    else {
        $buildParams += "--verbosity", "minimal"
    }

    Write-Host ""
    Write-Host "🔨 Building..." -ForegroundColor Blue
    try {
        & dotnet build @buildParams
        if ($LASTEXITCODE -ne 0) {
            Write-Host "❌ Build failed" -ForegroundColor Red
            Write-Host "💡 Try running with -VerboseOutput for more details" -ForegroundColor Yellow
            Pop-Location
            exit 1
        }
    }
    catch {
        Write-Host "❌ Build failed: $_" -ForegroundColor Red
        Pop-Location
        exit 1
    }

    Write-Host ""
    Write-Host "✅ Build successful" -ForegroundColor Green
    Write-Host "📍 Output: src\Tidalarr\bin\$Configuration" -ForegroundColor Gray
    
    if ($Package) {
        Write-Host "📦 Packaging plugin..." -ForegroundColor Blue
        $modulePath = Join-Path $scriptRoot 'ext/Lidarr.Plugin.Common/tools/PluginPack.psm1'
        Import-Module $modulePath -Force
        $manifestPath = Join-Path $scriptRoot 'plugin.json'
        $packagePath = New-PluginPackage -Csproj $pluginProject -Manifest $manifestPath -Framework 'net6.0' -Configuration $Configuration
        Write-Host "✅ Package created: $packagePath" -ForegroundColor Green

        try {
            $pluginAssemblyPath = Join-Path $scriptRoot "src/Tidalarr/bin/$Configuration/net6.0/Lidarr.Plugin.Tidalarr.dll"
            if (Test-Path $pluginAssemblyPath) {
                try { Add-Type -Path $pluginAssemblyPath -ErrorAction Stop } catch {}
            }
            $metadata = [Tidalarr.Integration.PackagingHelper]::WritePackagingMetadata($packagePath, 'net6.0', $Configuration)
            Write-Host "Package metadata: $($metadata.MetadataPath)" -ForegroundColor Gray
            Write-Host "Package hash: $($metadata.HashPath)" -ForegroundColor Gray
        }
        catch {
            Write-Host "Failed to write packaging metadata: $_" -ForegroundColor Yellow
        }

    }

    if ($Deploy) {
        Write-Host "🚀 Plugin deployed; restart Lidarr to load the update" -ForegroundColor Green
    }
}
else {
    Write-Host "⚙️ Build skipped (-NoBuild)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🎉 Build script completed" -ForegroundColor Green

if (-not $Deploy -and -not $NoBuild) {
    Write-Host ""
    Write-Host "💡 Next steps:" -ForegroundColor Cyan
    Write-Host "  To deploy automatically: .\build.ps1 $Configuration -Deploy" -ForegroundColor White
    Write-Host "  Plugin binaries: src\Tidalarr\bin\$Configuration" -ForegroundColor White
    Write-Host "  Manual deploy: copy the build output to your Lidarr plugins folder" -ForegroundColor White
}

if ($Deploy) {
    Write-Host "?? Plugin deployment requested; copying packaged outputs" -ForegroundColor Blue
    $outputDir = Join-Path $scriptRoot "src/Tidalarr/bin/$Configuration/net6.0"
    $dll = Join-Path $outputDir "Lidarr.Plugin.Tidalarr.dll"
    $pdb = Join-Path $outputDir "Lidarr.Plugin.Tidalarr.pdb"
    $manifest = Join-Path $outputDir "plugin.json"
    if (-not (Test-Path $dll)) { Write-Host "? Packaged assembly not found: $dll" -ForegroundColor Red; Pop-Location; exit 1 }
    try { [System.IO.Directory]::CreateDirectory($deployTarget) | Out-Null } catch {}

    # If Common remains in output (merge failed), include deps side-by-side
    $common = Join-Path $outputDir "Lidarr.Plugin.Common.dll"
    $sideBySide = Test-Path $common

    Copy-Item $dll $deployTarget -Force
    if (Test-Path $pdb) { Copy-Item $pdb $deployTarget -Force }
    if (Test-Path $manifest) { Copy-Item $manifest $deployTarget -Force }

    if ($sideBySide) {
        $depNames = @('Lidarr.Plugin.Common.dll','FluentValidation.dll','Polly.dll','Polly.Extensions.Http.dll','System.Text.Json.dll')
        foreach ($name in $depNames) {
            $path = Join-Path $outputDir $name
            if (Test-Path $path) { Copy-Item $path $deployTarget -Force }
        }
        Get-ChildItem -Path $outputDir -Filter 'Microsoft.Extensions.*.dll' -ErrorAction SilentlyContinue | ForEach-Object { Copy-Item $_.FullName $deployTarget -Force }
        Get-ChildItem -Path $outputDir -Filter 'TagLibSharp*.dll' -ErrorAction SilentlyContinue | ForEach-Object { Copy-Item $_.FullName $deployTarget -Force }
        $abstractions = Join-Path $deployTarget 'Lidarr.Plugin.Abstractions.dll'
        if (Test-Path $abstractions) { Remove-Item $abstractions -Force }
    }
}

Pop-Location






