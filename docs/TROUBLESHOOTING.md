# Tidalarr - Troubleshooting Guide

Comprehensive troubleshooting guide for common Tidalarr issues.

---

## Quick Reference

If you're experiencing issues:

1. **Enable debug logging** in Lidarr Settings > General
2. **Check logs** in System > Logs for error messages
3. **Verify plugin version** and Lidarr compatibility
4. **Review common issues** below
5. **Search GitHub Issues** for similar problems

---

## Common Issues

### Authentication Problems

#### Issue: OAuth authorization fails

**Symptoms**:
- "Invalid state" error
- Token refresh fails
- Authorization URL not generated
- Plugin shows authentication error

**Solutions**:
1. **Generate fresh OAuth URL**:
   - Click **Test** to generate new authorization URL
   - Open URL in browser and complete OAuth flow
   - Copy ENTIRE redirect URL (including `code=` and `state=` parameters)
   - Paste into **OAuth Redirect URL** field
   - Click **Test** again to exchange code for tokens

2. **Use fresh redirect URL**:
   - OAuth redirect URLs are one-time use
   - If tokens expired, use NEW redirect URL (overwrite, don't clear)
   - Never reuse old redirect URLs

3. **Check PKCE state**:
   - Ensure `/pkce_state.json` exists and is readable
   - File should be created automatically when clicking Test

**Prevention**:
- Keep OAuth tokens refreshed
- Use fresh redirect URL for each authentication
- Don't modify redirect URL when pasting

---

#### Issue: OAuth Authorization URL field is empty

**Symptoms**:
- OAuth Authorization URL field appears blank in settings

**Solutions**:
1. **Verify plugin is loaded**:
   - Check API schema: `/api/v1/indexer/schema` for Tidalarr
   - Look for plugin errors in Lidarr logs

2. **Generate new state**:
   - Click **Test** to generate fresh PKCE state
   - Check Lidarr logs for generation errors

3. **Check ConfigPath**:
   - Save settings first to set ConfigPath
   - Plugin needs valid configuration path

4. **Refresh settings modal**:
   - Lidarr may not refresh computed fields live
   - Close/re-open settings modal if needed

---

#### Issue: "Invalid state" error during authentication

**Symptoms**:
- Token exchange fails with state mismatch
- OAuth callback fails validation

**Solutions**:
1. **Clear and retry**:
   - Generate completely new OAuth URL
   - Complete fresh authentication flow
   - Use new redirect URL

2. **Check timing**:
   - OAuth flows can timeout
   - Complete process quickly after generating URL

3. **Clear browser cache**:
   - Try incognito/private browsing mode
   - Clear cookies for tidal.com

---

### Download Issues

#### Issue: Slow downloads or rate limiting

**Symptoms**:
- Downloads stall
- 429 "Too Many Requests" errors
- Slow chunk downloads
- Timeouts during download

**Solutions**:
1. **Increase Chunk Delay**:
   - Set to 100-200ms in Download Client settings
   - This disables chunk parallelism but prevents rate limiting

2. **Reduce Concurrency**:
   - Set **Max Concurrent Track Downloads** to 1
   - Set **Max Concurrent Chunk Downloads** to 1-2

3. **Increase Request Delay** (Indexer):
   - Set Request Delay to 200-500ms in Indexer settings
   - Reduces API call frequency

**Prevention**:
- Start with conservative settings
- Monitor logs for 429 errors
- Gradually increase settings as needed

---

#### Issue: Downloads not starting

**Symptoms**:
- Download queue shows "Pending"
- No progress after adding album
- Download client shows error state

**Solutions**:
1. **Check OAuth status**:
   - Verify Indexer authentication successful
   - Check for token expiration errors

2. **Check quality settings**:
   - Ensure selected quality is available
   - Try different quality setting

3. **Check disk space**:
   - Verify adequate space for downloads
   - Check write permissions on download directory

4. **Check network**:
   - Verify internet connectivity
   - Check Tidal service status

---

#### Issue: Incomplete downloads

**Symptoms**:
- Partial files in download directory
- Missing tracks from album
- File size smaller than expected

**Solutions**:
1. **Check network stability**:
   - Monitor for dropped connections
   - Ensure uninterrupted internet during downloads

2. **Check disk space**:
   - Verify enough space for complete album
   - Check for storage device errors

3. **Try sequential downloads**:
   - Set Max Concurrent Track Downloads to 1
   - Disable chunk parallelism if needed

4. **Clear and retry**:
   - Delete incomplete files
   - Restart download attempt

---

### Plugin Loading Issues

#### Issue: Plugin not loading in Lidarr

**Symptoms**:
- Plugin doesn't appear in Settings
- Missing schemas
- Assembly load errors in logs

**Solutions**:
1. **Check Lidarr version**:
   - Must be v3.0.0.4855 or higher
   - Must be on `plugins` or `nightly` branch

2. **Verify plugin files**:
   - Check plugin directory contains required files:
     - `plugin.json`
     - `manifest.json`
     - `Lidarr.Plugin.Tidalarr.dll`
   - Ensure correct directory structure

3. **Restart Lidarr**:
   - Complete restart, not just service restart
   - Clear browser cache after restart

4. **Check logs**:
   - Look for assembly load errors
   - Check for dependency conflicts

---

#### Issue: Plugin schema errors

**Symptoms**:
- `/api/v1/*/schema` returns errors for Tidalarr
- Settings forms don't load properly

**Solutions**:
1. **Check API endpoints**:
   ```bash
   # Test indexer schema
   curl -X GET http://localhost:8686/api/v1/indexer/schema

   # Test download client schema
   curl -X GET http://localhost:8686/api/v1/downloadclient/schema
   ```

2. **Verify plugin registration**:
   - Check plugin appears in Installed Plugins list
   - Verify plugin is enabled

3. **Check configuration path**:
   - Ensure ConfigPath is correctly set
   - Plugin needs valid configuration directory

---

### Search Issues

#### Issue: No search results

**Symptoms**:
- Search returns no Tidalarr results
- Plugin not found in search results

**Solutions**:
1. **Verify Indexer configuration**:
   - Check OAuth authentication successful
   - Ensure Indexer is enabled and saved

2. **Test search manually**:
   - Try searching for popular albums
   - Check for search errors in logs

3. **Check API connectivity**:
   - Verify Tidal API is accessible
   - Check for network issues

4. **Request delay adjustment**:
   - Increase Request Delay if getting rate limited
   - Enable logging to see search calls

---

#### Issue: Search errors in logs

**Symptoms**:
- 429 rate limit errors
- Network timeouts
- API authentication errors

**Solutions**:
1. **Increase Request Delay**:
   - Set to 200-500ms in Indexer settings
   - Reduces API call frequency

2. **Enable logging**:
   - Set Enable Logging to true in Indexer settings
   - Monitor search calls and responses

3. **Check API limits**:
   - Tidal has rate limits on API calls
   - Adjust search frequency as needed

---

### Quality Issues

#### Issue: Downloads not in expected quality

**Symptoms**:
- Requested HiRes but getting Lossless
- Quality detection inconsistent

**Solutions**:
1. **Check availability**:
   - Not all tracks available in all qualities
   - Tidalarr automatically falls back to next available quality

2. **Enable logging**:
   - Set Enable Logging to true
   - Check logs for quality detection messages

3. **Try different quality**:
   - Test with Lossless setting
   - Verify track availability in Tidal web interface

---

### Performance Issues

#### Issue: High memory usage

**Symptoms**:
- Lidarr using excessive memory
- Performance degradation during downloads

**Solutions**:
1. **Reduce concurrency**:
   - Set Max Concurrent Track Downloads to 1
   - Set Max Concurrent Chunk Downloads to 1

2. **Monitor memory**:
   - Watch memory usage during downloads
   - Restart Lidarr if memory leaks detected

3. **Check for memory leaks**:
   - Monitor memory over time
   - Look for growing memory in logs

---

#### Issue: CPU usage spikes

**Symptoms**:
- High CPU usage during downloads
- System performance impacted

**Solutions**:
1. **Reduce parallel downloads**:
   - Lower Max Concurrent Track Downloads
   - Disable chunk parallelism (set Chunk Delay > 0)

2. **Check CPU monitoring**:
   - Monitor CPU during downloads
   - Adjust settings based on system capabilities

---

## Debug Logging

### Enabling Debug Logging

1. **Go to Settings > General**
2. **Set Log Level to Debug**
3. **Restart Lidarr**
4. **Reproduce the issue**
5. **Check System > Logs for detailed output**

### Key Log Patterns to Watch

| Pattern | Meaning |
|---------|---------|
| `[Tidalarr]` | Plugin status messages |
| `[TidalAuth]` | OAuth authentication |
| `[TidalApi]` | API calls and responses |
| `[TidalSearch]` | Search operations |
| `[TidalDownload]` | Download progress |
| `[429]` | Rate limiting errors |
| `[OAuth]` | Authentication errors |

### Log Analysis Tips

1. **Look for errors** in `[TidalAuth]` and `[TidalApi]` sections
2. **Check for 429 errors** indicating rate limiting
3. **Monitor download progress** for stalled operations
4. **Check OAuth tokens** for expiration
5. **Look for network timeouts** in API calls

---

## System Configuration Issues

### Docker Environment Issues

#### Volume Permissions
**Issue**: Plugin files not accessible
**Solution**:
```bash
# Check volume permissions
docker exec -it lidarr ls -la /config/plugins/RicherTunes/Tidalarr/

# Fix permissions if needed
docker exec -it lidarr chown -R 1000:1000 /config/plugins/RicherTunes/Tidalarr/
```

#### Environment Variables
**Issue**: Settings not applying
**Solution**:
```yaml
# In docker-compose.yml
environment:
  - TIDAL_REQUEST_DELAY_MS=200
  - TIDAL_CHUNK_DELAY_MS=100
```

### Linux Service Issues

#### Permission Issues
**Issue**: Plugin files not executable
**Solution**:
```bash
# Set proper permissions
sudo chmod -R 755 /var/lib/lidarr/plugins/RicherTunes/Tidalarr/
sudo chown -R lidarr:lidarr /var/lib/lidarr/plugins/RicherTunes/Tidalarr/
```

#### Service Restart
**Issue**: Changes not taking effect
**Solution**:
```bash
# Complete restart
sudo systemctl stop lidarr
sudo systemctl start lidarr
```

### Windows Issues

#### Service Permissions
**Issue**: Plugin files not accessible
**Solution**:
- Run Lidarr as service with proper permissions
- Ensure service account has access to plugin directory

#### Path Issues
**Issue**: Plugin files in wrong location
**Solution**:
- Verify correct path: `%ProgramData%\Lidarr\plugins\RicherTunes\Tidalarr\`
- Use forward slashes in paths if needed

---

## Advanced Troubleshooting

### API Testing

You can test the plugin API endpoints directly:

```bash
# Test indexer schema
curl -X GET http://localhost:8686/api/v1/indexer/schema | jq '.[] | select(.name | contains("Tidalarr"))'

# Test download client schema
curl -X GET http://localhost:8686/api/v1/downloadclient/schema | jq '.[] | select(.name | contains("Tidalarr"))'

# Test plugin status
curl -X GET http://localhost:8686/api/v1/plugin/status | jq '.[] | select(.name | contains("Tidalarr"))'
```

### Configuration Validation

Check plugin configuration through API:

```bash
# Get indexer configuration
curl -X GET http://localhost:8686/api/v1/indexer

# Get download client configuration
curl -X GET http://localhost:8686/api/v1/downloadclient
```

### File System Checks

Verify plugin files and permissions:

```bash
# Check plugin directory
ls -la /path/to/lidarr/plugins/RicherTunes/Tidalarr/

# Check required files
ls -la /path/to/lidarr/plugins/RicherTunes/Tidalarr/{plugin.json,manifest.json,Lidarr.Plugin.Tidalarr.dll}

# Check configuration path
ls -la /path/to/lidarr/config/
```

---

## Getting Help

### Before Asking for Help

1. **Enable debug logging** and reproduce the issue
2. **Check this troubleshooting guide** for similar issues
3. **Search GitHub Issues** for existing solutions
4. **Collect system information**:
   - Lidarr version
   - Plugin version
   - Operating system
   - Network setup
   - Tidal subscription type

### Creating a Good Issue Report

Include this information in your GitHub issue:

```markdown
## Description
[Clear description of the issue]

## Steps to Reproduce
1. [Step 1]
2. [Step 2]
3. [Step 3]

## Expected Behavior
[What should happen]

## Actual Behavior
[What actually happens]

## Environment
- Lidarr Version: [version]
- Plugin Version: [version]
- OS: [OS and version]
- Network: [Network type]
- Tidal Subscription: [Free/HFi/Family]

## Log Excerpts
[Relevant log entries with sensitive data redacted]

## Additional Information
[Any other relevant details]
```

### Log Redaction

Always redact sensitive information:
- OAuth tokens
- Redirect URLs
- IP addresses
- Personal information

---

## Common Error Codes

| Error Code | Meaning | Solution |
|------------|---------|----------|
| 429 | Rate limit | Increase request delay |
| 401 | Authentication | Re-authenticate OAuth |
| 403 | Forbidden | Check Tidal subscription |
| 404 | Not found | Verify track/album ID |
| 500 | Server error | Check Tidal status |
| 503 | Service unavailable | Try again later |

---

## Prevention Tips

1. **Monitor logs regularly** for early issue detection
2. **Use conservative settings** for always-on systems
3. **Keep Lidarr updated** to latest compatible version
4. **Monitor Tidal service status** for known issues
5. **Test new features** in non-production environments
6. **Back up configuration** before making changes
7. **Use appropriate quality** for your network capabilities

---

## Support Resources

- **Issues**: [GitHub Issues](https://github.com/RicherTunes/Tidalarr/issues)
- **Discussions**: [GitHub Discussions](https://github.com/RicherTunes/Tidalarr/discussions)
- **Documentation**: [User Guide](../wiki-content/User-Guide.md) and [Installation Guide](../wiki-content/Installation.md)
- **Architecture**: [Technical Documentation](ARCHITECTURE.md)

---

**Current Version**: v1.0.1 | **Last Updated**: January 2025