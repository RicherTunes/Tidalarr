param(
  [int]$PrNumber = 16,
  [int]$PollIntervalSeconds = 30
)

$ErrorActionPreference = 'Stop'

Write-Host "🔎 Watching PR #$PrNumber for merge... (Ctrl+C to cancel)" -ForegroundColor Cyan

while ($true) {
  try {
    $json = gh pr view $PrNumber --json state,mergedAt,mergeStateStatus,headRefName,baseRefName,url | ConvertFrom-Json
  } catch {
    Write-Host "⚠️ gh pr view failed: $_" -ForegroundColor Yellow
    Start-Sleep -Seconds $PollIntervalSeconds
    continue
  }

  $state = $json.state
  $mergedAt = $json.mergedAt
  Write-Host ("  • State: {0} | MergeState: {1} | Head: {2} -> Base: {3}" -f $state,$json.mergeStateStatus,$json.headRefName,$json.baseRefName)

  if ($state -eq 'MERGED' -or $mergedAt) {
    Write-Host "✅ PR merged at $mergedAt" -ForegroundColor Green

    # Update submodule to latest merged commit
    Write-Host "🔄 Updating Common submodule..." -ForegroundColor Cyan
    git submodule update --remote --merge ext/Lidarr.Plugin.Common | Out-Host

    Write-Host "🔨 Building solution..." -ForegroundColor Cyan
    dotnet build Tidalarr.sln -c Release -v minimal | Out-Host

    Write-Host "🧪 Running tests..." -ForegroundColor Cyan
    dotnet test Tidalarr.sln -c Release --no-build --logger trx --results-directory tests/Tidalarr.Tests/TestResults | Out-Host

    Write-Host "🎉 Update complete." -ForegroundColor Green
    exit 0
  }

  Start-Sleep -Seconds $PollIntervalSeconds
}

