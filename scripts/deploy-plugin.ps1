param(
    [Parameter(Mandatory = $true)]
    [string]$ContainerName,

    [Parameter(Mandatory = $true)]
    [string]$PluginZip,

    [string]$PluginId = 'tidalarr',
    [string]$PluginRoot = '/config/plugins'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $PluginZip)) {
    throw "Plugin archive not found at $PluginZip"
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI is required for deployment"
}

$timestamp = (Get-Date).ToString('yyyy-MM-ddTHH-mm-ss')
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("tidalarr-deploy-" + [System.Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    Expand-Archive -Path $PluginZip -DestinationPath $tempDir -Force
    $payload = Get-ChildItem -Path $tempDir -File -Recurse
    if ($payload.Count -eq 0) {
        throw "Plugin archive appears empty"
    }

    $targetPath = "$PluginRoot/$PluginId"
    $backupPath = "$PluginRoot/$PluginId-backup-$timestamp"

    Write-Host "Backing up existing plugin folder (if present)" -ForegroundColor Cyan
    docker exec $ContainerName sh -c "if [ -d '$targetPath' ]; then rm -rf '$backupPath' && mv '$targetPath' '$backupPath'; fi"

    Write-Host "Copying new plugin contents" -ForegroundColor Cyan
    docker exec $ContainerName mkdir -p "$targetPath"
    foreach ($file in Get-ChildItem -Path $tempDir -Recurse -File) {
        $relative = $file.FullName.Substring($tempDir.Length).TrimStart('\\','/')
        $destination = "$targetPath/$relative"
        $destinationDir = [System.IO.Path]::GetDirectoryName($destination).Replace('\','/')
        if (-not [string]::IsNullOrWhiteSpace($destinationDir)) {
            docker exec $ContainerName mkdir -p "$destinationDir" | Out-Null
        }
        docker cp $file.FullName "$ContainerName:$destination"
    }

    Write-Host "Deployment completed. Restart Lidarr to load the plugin." -ForegroundColor Green
}
finally {
    if (Test-Path $tempDir) {
        Remove-Item -Path $tempDir -Recurse -Force
    }
}

