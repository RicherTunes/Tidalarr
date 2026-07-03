param()

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$fixturePath = Join-Path $repoRoot 'tests\Tidalarr.Tests\Runtime\LidarrContainerFixture.cs'
$resolverPath = Join-Path $repoRoot 'ext\Lidarr.Plugin.Common\testkit\Hosting\PluginArtifactResolver.cs'
$paritySpecPath = Join-Path $repoRoot 'ext\Lidarr.Plugin.Common\scripts\parity-spec.json'
$failures = New-Object System.Collections.Generic.List[string]

function Assert-Condition {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        $script:failures.Add($Message)
    }
}

function Invoke-ResolverBehaviorProbe {
    param(
        [string]$ResolverPath,
        [string]$PluginDllFileName,
        [string]$RawFallbackCandidate,
        [string]$ProjectPublishCandidate = ''
    )

    $probeRoot = Join-Path ([IO.Path]::GetTempPath()) ("artifact-resolver-probe-" + [Guid]::NewGuid().ToString("N"))
    $sourceDir = Join-Path $probeRoot 'source'
    $scenarioRoot = Join-Path $probeRoot 'scenario'
    $projectPath = Join-Path $probeRoot 'ArtifactResolverProbe.csproj'
    $programPath = Join-Path $probeRoot 'Program.cs'

    try {
        New-Item -ItemType Directory -Path $sourceDir -Force | Out-Null
        New-Item -ItemType Directory -Path $scenarioRoot -Force | Out-Null
        Copy-Item -LiteralPath $ResolverPath -Destination (Join-Path $sourceDir 'PluginArtifactResolver.cs') -Force

        @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="source\PluginArtifactResolver.cs" />
  </ItemGroup>
</Project>
'@ | Set-Content -LiteralPath $projectPath -Encoding UTF8

        @'
using System;
using System.IO;
using Lidarr.Plugin.Common.TestKit.Hosting;

static void CleanDirectory(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, recursive: true);
    }

    Directory.CreateDirectory(path);
}

static string TouchRelative(string root, string relativePath)
{
    var path = Path.GetFullPath(Path.Combine(root, relativePath));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, "placeholder");
    return path;
}

static void AssertPath(string expected, string? actual, string message)
{
    if (!string.Equals(Path.GetFullPath(expected), actual is null ? null : Path.GetFullPath(actual), StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"{message}. Expected '{expected}', got '{actual ?? "<null>"}'.");
    }
}

static void AssertNull(string? actual, string message)
{
    if (actual is not null)
    {
        throw new InvalidOperationException($"{message}. Got '{actual}'.");
    }
}

var scenarioRoot = args[0];
var pluginDllFileName = args[1];
var projectPublishCandidate = args[2];
var rawFallbackCandidate = args[3];
var canonicalPublishCandidate = Path.Combine("artifacts", "publish", "net8.0", "Release", pluginDllFileName);

CleanDirectory(scenarioRoot);
var canonicalPublishPath = TouchRelative(scenarioRoot, canonicalPublishCandidate);
TouchRelative(scenarioRoot, rawFallbackCandidate);
var resolved = PluginArtifactResolver.FindPluginDll(scenarioRoot, pluginDllFileName, rawFallbackCandidate);
AssertPath(canonicalPublishPath, resolved, "Canonical publish artifact must win over raw fallback");

CleanDirectory(scenarioRoot);
TouchRelative(scenarioRoot, canonicalPublishCandidate);
TouchRelative(scenarioRoot, Path.Combine("artifacts", "publish", "net8.0", "Release", "FluentValidation.dll"));
TouchRelative(scenarioRoot, rawFallbackCandidate);
resolved = PluginArtifactResolver.FindPluginDll(scenarioRoot, pluginDllFileName, rawFallbackCandidate);
AssertNull(resolved, "Dirty canonical publish artifact must fail closed instead of falling through");

if (!string.IsNullOrWhiteSpace(projectPublishCandidate))
{
    CleanDirectory(scenarioRoot);
    var projectPublishPath = TouchRelative(scenarioRoot, projectPublishCandidate);
    TouchRelative(scenarioRoot, rawFallbackCandidate);
    resolved = PluginArtifactResolver.FindPluginDll(scenarioRoot, pluginDllFileName, projectPublishCandidate, rawFallbackCandidate);
    AssertPath(projectPublishPath, resolved, "Nested project publish artifact must win over raw fallback");
}
'@ | Set-Content -LiteralPath $programPath -Encoding UTF8

        $output = & dotnet run --project $projectPath --verbosity quiet -- $scenarioRoot $PluginDllFileName $ProjectPublishCandidate $RawFallbackCandidate 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw (($output | Out-String).Trim())
        }
    } finally {
        if (Test-Path -LiteralPath $probeRoot) {
            Remove-Item -LiteralPath $probeRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

Assert-Condition (Test-Path -LiteralPath $fixturePath) "Missing Docker E2E fixture: $fixturePath"
Assert-Condition (Test-Path -LiteralPath $resolverPath) "Missing Common TestKit artifact resolver: $resolverPath"
Assert-Condition (Test-Path -LiteralPath $paritySpecPath) "Missing Common parity spec: $paritySpecPath"

if ($failures.Count -eq 0) {
    $fixtureContent = Get-Content -LiteralPath $fixturePath -Raw
    $resolverContent = Get-Content -LiteralPath $resolverPath -Raw
    $forbiddenSidecars = @((Get-Content -LiteralPath $paritySpecPath -Raw | ConvertFrom-Json).versionContract.forbiddenPackageContents)

    $publishCandidate = 'Path.Combine(repoRoot, "artifacts", "publish", "net8.0", "Release", pluginDllFileName)'
    $packageCandidate = 'Path.Combine(repoRoot, "package", pluginDllFileName)'
    $projectPublishCandidate = 'Path.Combine("src", "Tidalarr", "artifacts", "publish", "net8.0", "Release", "Lidarr.Plugin.Tidalarr.dll")'
    $rawBinCandidate = 'Path.Combine("src", "Tidalarr", "bin", "Lidarr.Plugin.Tidalarr.dll")'

    $publishIndex = $resolverContent.IndexOf($publishCandidate, [StringComparison]::Ordinal)
    $packageIndex = $resolverContent.IndexOf($packageCandidate, [StringComparison]::Ordinal)
    $projectPublishIndex = $fixtureContent.IndexOf($projectPublishCandidate, [StringComparison]::Ordinal)
    $rawBinIndex = $fixtureContent.IndexOf($rawBinCandidate, [StringComparison]::Ordinal)

    Assert-Condition ($fixtureContent -match 'PluginArtifactResolver\.FindPluginDll') `
        'Docker E2E fixture must delegate artifact selection to Common TestKit.'
    Assert-Condition ($fixtureContent -notmatch 'HasForbiddenHostBoundarySidecars') `
        'Docker E2E fixture must not duplicate Common TestKit sidecar policy.'
    Assert-Condition ($publishIndex -ge 0) `
        'Docker E2E resolver must consider the canonical packaged publish artifact.'
    Assert-Condition ($packageIndex -ge 0) `
        'Docker E2E resolver must consider the legacy package/ artifact.'
    Assert-Condition ($projectPublishIndex -ge 0) `
        'Docker E2E fixture must check the nested project publish artifact before raw bin output.'
    Assert-Condition ($rawBinIndex -ge 0) `
        'Docker E2E resolver may keep raw bin as a last-resort candidate.'

    if ($publishIndex -ge 0 -and $packageIndex -ge 0) {
        Assert-Condition ($publishIndex -lt $packageIndex) `
            'Canonical packaged publish artifact must be preferred over legacy package/ artifact.'
    }

    if ($projectPublishIndex -ge 0 -and $rawBinIndex -ge 0) {
        Assert-Condition ($projectPublishIndex -lt $rawBinIndex) `
            'Nested project publish artifact must be preferred over raw bin output.'
    }

    foreach ($forbiddenSidecar in $forbiddenSidecars) {
        Assert-Condition ($resolverContent.Contains($forbiddenSidecar)) `
            "Docker E2E resolver must reject plugin directories containing $forbiddenSidecar."
    }

    try {
        Invoke-ResolverBehaviorProbe `
            -ResolverPath $resolverPath `
            -PluginDllFileName 'Lidarr.Plugin.Tidalarr.dll' `
            -ProjectPublishCandidate 'src/Tidalarr/artifacts/publish/net8.0/Release/Lidarr.Plugin.Tidalarr.dll' `
            -RawFallbackCandidate 'src/Tidalarr/bin/Lidarr.Plugin.Tidalarr.dll'
    } catch {
        $failures.Add("Docker E2E resolver behavioral probe failed: $_")
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'FAIL: Docker E2E artifact contract'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }
    exit 1
}

Write-Host 'PASS: Docker E2E artifact contract'
