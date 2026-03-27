# Deployment & Smoke Test Playbook

Use this checklist whenever you cut a new Tidalarr build. It targets the Lidarr plugins branch on .NET 8 (e.g., `ghcr.io/hotio/lidarr:pr-plugins-3.1.2.4913`). See CLAUDE.md for the current Docker image tag.

## 1. Run the unified CI pipeline locally (optional)

```powershell
./scripts/ci.ps1
```

This validates host assemblies, manifest metadata, Release build and packages `artifacts/Tidalarr-<version>.zip`.

## 2. Prepare your Lidarr container / host

1. Ensure the target Lidarr instance is a .NET 8 plugins branch build (pr-plugins-3.x).
2. Stop Lidarr or put it into maintenance mode.
3. Locate the plugin root:
   - Docker: `/config/plugins`
   - Windows service: `%ProgramData%\Lidarr\plugins`
   - Linux service: `/var/lib/lidarr/plugins`

## 3. Deploy the new plugin build

### Docker helper script (recommended)

```powershell
./scripts/deploy-plugin.ps1 `
  -ContainerName lidarr `
  -PluginZip artifacts/Tidalarr-1.0.1.zip `
  -PluginId tidalarr
```

The script copies the unpacked plugin into `/config/plugins/<PluginId>`, preserving a timestamped backup of any existing folder.

### Manual copy (alternative)

```powershell
Expand-Archive -Path artifacts/Tidalarr-1.0.1.zip -DestinationPath artifacts/deploy/Tidalarr -Force
# Remove the existing plugin folder on the host, then copy the extracted contents in.
```

## 4. Restart Lidarr & verify

1. Start the Lidarr container/service.
2. Tail the logs for assembly load errors:
   ```bash
   docker logs -f lidarr | grep -i tidal
   ```
3. Confirm the plugin shows up under `Settings → Plugins` and the version matches `1.0.1`.

## 5. Regression checks

- Run a search for a known album to confirm the indexer loads.
- Kick off a download to confirm the orchestrator still produces chunks and tags files correctly.
- Inspect Lidarr logs for any `Could not load file or assembly` or `ReflectionTypeLoadException` messages.

## 6. Rollback procedure

If anything fails:
1. Stop Lidarr.
2. Restore the backup folder saved by `deploy-plugin.ps1` (e.g. `tidalarr-backup-2025-09-30T16-50-00`).
3. Restart Lidarr and capture logs for troubleshooting.

## 7. Promote to production

Once the smoke test passes, attach the new `Tidalarr-<version>.zip` to the release notes alongside a short changelog and validation log excerpts.





