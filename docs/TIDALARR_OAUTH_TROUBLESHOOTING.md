# Tidalarr OAuth Troubleshooting

This guide helps diagnose and resolve Tidal OAuth authentication issues when running E2E tests or configuring the plugin.

## Token File Locations

Tidalarr stores OAuth state in the Lidarr config directory:

| File | Purpose | Location |
|------|---------|----------|
| `tidal_tokens.json` | OAuth access/refresh tokens | `/config/tidalarr/tidal_tokens.json` |
| `tidal_pkce_state.json` | PKCE code verifier (temporary) | `/config/tidalarr/tidal_pkce_state.json` |

**Docker mount:** Ensure `/config` is mounted as a persistent volume, not ephemeral.

## Pre-Flight Checklist

Before running E2E tests with credentials:

- [ ] **ConfigPath is set** in indexer settings (default: `/config/tidalarr`)
- [ ] **Directory exists and is writable** by the Lidarr process
- [ ] **RedirectUrl matches** what's registered with your Tidal app
- [ ] **No stale token files** from previous failed attempts

```bash
# Check from inside container
docker exec lidarr-multi-plugin-persist ls -la /config/tidalarr/
docker exec lidarr-multi-plugin-persist cat /config/tidalarr/tidal_tokens.json 2>/dev/null || echo "No tokens"
```

## Common Failure Patterns

### 1. "AuthUrl empty" or No Auth URL Returned

**Causes:**
- `ConfigPath` field is empty or points to non-existent directory
- Directory is not writable (permission denied)
- PKCE state file couldn't be created

**Fix:**
```bash
# Create directory with proper permissions
docker exec lidarr-multi-plugin-persist mkdir -p /config/tidalarr
docker exec lidarr-multi-plugin-persist chmod 755 /config/tidalarr
```

### 2. `invalid_grant`

**Causes:**
- Authorization code expired (codes are single-use and short-lived)
- PKCE code_verifier mismatch (state file was deleted/corrupted mid-flow)
- Token refresh failed (refresh token expired or revoked)

**Fix:**
```bash
# Reset auth completely and start fresh
docker exec lidarr-multi-plugin-persist rm -f /config/tidalarr/tidal_tokens.json
docker exec lidarr-multi-plugin-persist rm -f /config/tidalarr/tidal_pkce_state.json
# Then re-initiate OAuth flow from Lidarr UI
```

### 3. `invalid_client`

**Causes:**
- Client ID/secret mismatch
- App not registered or deactivated in Tidal Developer Portal

**Fix:**
- Verify your Tidal app credentials in the Developer Portal
- Ensure the app is active and has proper permissions

### 4. `redirect_uri_mismatch`

**Causes:**
- RedirectUrl in plugin settings doesn't match the registered redirect URI in Tidal app
- Missing trailing slash or http vs https mismatch

**Fix:**
- Copy the exact redirect URI from your Tidal app registration
- Ensure protocol (http/https), host, port, and path match exactly

### 5. PKCE Mismatch / State Mismatch

**Causes:**
- `tidal_pkce_state.json` was deleted or corrupted between auth initiation and callback
- Multiple auth flows started simultaneously
- Container restarted mid-flow

**Fix:**
```bash
# Clean up and restart auth flow
docker exec lidarr-multi-plugin-persist rm -f /config/tidalarr/tidal_pkce_state.json
# Then initiate a fresh OAuth flow
```

## Safe Log Collection

When collecting logs for debugging, **redact these fields**:

| Field | Redaction |
|-------|-----------|
| `access_token` | `[REDACTED_ACCESS_TOKEN]` |
| `refresh_token` | `[REDACTED_REFRESH_TOKEN]` |
| `code_verifier` | `[REDACTED_PKCE_VERIFIER]` |
| `authorization_code` | `[REDACTED_AUTH_CODE]` |
| `client_secret` | `[REDACTED_CLIENT_SECRET]` |

**Collect these safely:**
```bash
# Container logs (last 100 lines)
docker logs --tail 100 lidarr-multi-plugin-persist 2>&1 | grep -i tidal

# Lidarr logs (redacted)
docker exec lidarr-multi-plugin-persist cat /config/logs/lidarr.txt | grep -i tidal | tail -50
```

## Clean Reset Procedure

To completely reset Tidalarr authentication:

```bash
# 1. Stop any pending auth flows
# 2. Remove token files
docker exec lidarr-multi-plugin-persist rm -f /config/tidalarr/tidal_tokens.json
docker exec lidarr-multi-plugin-persist rm -f /config/tidalarr/tidal_pkce_state.json

# 3. Verify cleanup
docker exec lidarr-multi-plugin-persist ls -la /config/tidalarr/

# 4. Restart Lidarr (optional but recommended)
docker restart lidarr-multi-plugin-persist

# 5. Re-configure indexer in Lidarr UI and initiate OAuth
```

## E2E Gate Behavior

When running E2E gates without valid OAuth:

| Gate | Behavior | Reason |
|------|----------|--------|
| Schema | PASS | No credentials needed |
| Search | SKIP | Credentials not configured |
| AlbumSearch | SKIP | Credentials not configured |
| Grab | SKIP | Credentials not configured |

The E2E runner detects missing credentials via:
- `CredentialAnyOfFieldNames = @("redirectUrl", "oauthRedirectUrl")` - at least one must be present
- Auth errors matching: `invalid_grant`, `invalid_client`, `unauthorized`, `forbidden`, etc.

## Diagnostic Commands

```powershell
# Run schema gate only (no creds needed)
pwsh scripts/e2e-runner.ps1 -Plugins 'Tidalarr' -Gate schema -LidarrUrl 'http://localhost:8691' -ContainerName 'lidarr-multi-plugin-persist' -ExtractApiKeyFromContainer

# Run all gates (will SKIP if creds missing)
pwsh scripts/e2e-runner.ps1 -Plugins 'Tidalarr' -Gate all -LidarrUrl 'http://localhost:8691' -ContainerName 'lidarr-multi-plugin-persist' -ExtractApiKeyFromContainer

# Check indexer configuration
curl -s "http://localhost:8691/api/v1/indexer" -H "X-Api-Key: YOUR_KEY" | jq '.[] | select(.implementation == "TidalLidarrIndexer")'
```

## Related Documentation

- [ECOSYSTEM_E2E_PLAN.md](./ECOSYSTEM_E2E_PLAN.md) - Overall E2E testing strategy
- [lidarr.plugin.common PERSISTENT_E2E_TESTING.md](../ext/Lidarr.Plugin.Common/docs/PERSISTENT_E2E_TESTING.md) - Shared E2E harness docs
