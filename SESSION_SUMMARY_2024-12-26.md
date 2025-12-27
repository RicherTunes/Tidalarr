# Tidalarr Debug Session Summary - December 26, 2024

## Session Overview

This session focused on fixing critical issues preventing Tidalarr from functioning as a Lidarr plugin. The plugin now successfully searches Tidal and downloads albums with proper audio quality.

## Issues Fixed

### 1. "The 'tidal' scheme is not supported" Error
**File:** `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs`

**Problem:** Lidarr's `HttpIndexerBase` attempted to HTTP-fetch `tidal://search?query=...` placeholder URLs, which failed because `tidal://` is not a valid HTTP scheme.

**Solution:** Overrode `FetchReleases` method to bypass HTTP fetching entirely. Instead, the method extracts the search query from the placeholder URL and calls `TidalSearchService` directly via `PerformDirectSearchAsync`.

**Key Code Added:**
- `FetchReleases` override that intercepts request chain and extracts search queries
- `PerformDirectSearchAsync` method that calls the Tidal API directly
- Detailed logging for authentication status and API responses

### 2. JSON Deserialization - Numeric IDs
**File:** `src/Tidalarr/Core/Models/TidalDtos.cs`

**Problem:** Tidal API returns album/track/artist IDs as JSON numbers (e.g., `61799588`), but the DTOs expected strings. Error: `Cannot get the value of a token type 'Number' as a string`.

**Solution:** Added `JsonStringOrNumberConverter` class that handles both string and number JSON tokens, converting to string. Applied `[JsonConverter(typeof(JsonStringOrNumberConverter))]` attribute to all ID fields:
- `TidalArtistDto.id`
- `TidalAlbumDto.id`
- `TidalTrackDto.id`
- `TidalPlaybackInfoDto.trackId`
- `TidalTokenResponse.user_id`

### 3. "Album response missing primary artist" Exception
**File:** `src/Tidalarr/Domain/Api/TidalApiClient.cs`

**Problem:** `MapToTidalAlbumInfo` threw an exception when `dto.artist` was null. Some albums only populate the `artists[]` array, not the singular `artist` field.

**Solution:** Removed the exception throw. The existing code already gracefully builds the artist list from both `artist` (singular) and `artists[]` (array) fields, falling back to "Unknown Artist" if both are empty.

### 4. Frontend Protocol Error (ProtocolLabel.js)
**File:** `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs`

**Problem:** Lidarr frontend crashed with `TypeError: Cannot read properties of undefined (reading 'replace')` because `ReleaseInfo.DownloadProtocol` was not set.

**Solution:** Added `DownloadProtocol = nameof(TidalarrDownloadProtocol)` to all `ReleaseInfo` objects in both `ConvertToReleaseInfo` method instances (lines 301 and 557).

### 5. Size Showing "0B" in Search Results
**File:** `src/Tidalarr/Integration/LidarrNative/TidalLidarrIndexer.cs`

**Problem:** `EstimateAlbumSize` returned 0 when `album.Tracks` was an empty list (not null). The expression `album.Tracks?.Count ?? 12` returned 0 because `?.Count` on an empty list returns 0, not null.

**Solution:** Changed to explicit check:
```csharp
var trackCount = (album.Tracks?.Count ?? 0) > 0 ? album.Tracks!.Count : 12;
```

### 6. Manifest Parsing Failed (albumReplayGain)
**File:** `src/Tidalarr/Core/Models/TidalDtos.cs`

**Problem:** `TidalPlaybackInfoDto` defined replay gain fields as `int?`, but Tidal API returns them as floating-point numbers. Error: `The JSON value could not be converted to System.Nullable[System.Int32]. Path: $.albumReplayGain`.

**Solution:** Changed field types from `int?` to `double?`:
- `albumPeakAmplitude`
- `albumReplayGain`
- `trackPeakAmplitude`
- `trackReplayGain`

### 7. Plugin Assembly Loading
**File:** `src/Tidalarr/Tidalarr.csproj`

**Problem:** `Lidarr.Plugin.Abstractions.dll` was being deleted during ILRepack cleanup, causing plugin load failure: `Could not load file or assembly 'Lidarr.Plugin.Abstractions'`.

**Solution:** Added configuration to preserve the assembly:
```xml
<ItemGroup>
  <PluginPackagingAdditionalKeep Include="$(OutputPath)Lidarr.Plugin.Abstractions.dll" />
</ItemGroup>
```

Also added `<Private>true</Private>` to the Abstractions project reference.

## Files Modified

| File | Changes |
|------|---------|
| `TidalDtos.cs` | Added `JsonStringOrNumberConverter`, applied to ID fields, changed replay gain types to `double?` |
| `TidalApiClient.cs` | Removed artist null check exception, added comment explaining artist list building |
| `TidalLidarrIndexer.cs` | Added `FetchReleases` override, `PerformDirectSearchAsync`, fixed size estimation, added `DownloadProtocol` |
| `TidalLidarrDownloadClient.cs` | Added detailed logging for debugging async download flow |
| `TidalChunkStreamProvider.cs` | Added console logging for debugging manifest fetch and chunk download |
| `TidalModule.cs` | Added logging to orchestrator delegates (getTrackIds) |
| `Tidalarr.csproj` | Added `PluginPackagingAdditionalKeep` for Abstractions.dll, added Private=true to references |

## Current State

### Working Features
- Tidalarr indexer appears in Lidarr and can be configured
- OAuth 2.0 PKCE authentication works (tokens stored in `/config/tidalarr/tidal_tokens.json`)
- Search returns results with proper quality labels (FLAC 16bit, Hi-Res FLAC 24bit)
- Size estimation shows reasonable values (~360MB per album)
- Download client successfully downloads albums
- Chunked DASH streaming works (manifest parsing, chunk download, assembly)
- Files saved with correct track names (e.g., `01 - Burn the Witch.m4a`)

### Known Limitations / Future Work
1. **Metadata tagging not implemented**: Downloaded files do not have embedded ID3/M4A tags (artist, album, track info). Would need post-processing step using a library like TagLib#.

2. **Debug logging still present**: Console.WriteLine statements in `TidalChunkStreamProvider.cs` and `TidalModule.cs` should be removed or converted to proper NLog logging for production.

3. **Duplicate method in TidalModule.cs**: `getTrackIds_UNUSED` function exists from debugging (generates compiler warning CS8321).

4. **Lidarr warnings**: "Skipping provider of unknown type TidalLidarrDownloadClientSettings" appears in logs but does not affect functionality. Same warning appears for Qobuzarr.

5. **FLAC extraction**: The `ExtractFlac` setting exists but actual M4A-to-FLAC conversion is not implemented.

## Test Environment

- **Lidarr Version:** 3.1.1.4884 (plugins branch)
- **Container:** `lidarr-multi-plugin-persist`
- **Plugin Path:** `/config/plugins/RicherTunes/Tidalarr/`
- **Config Path:** `/config/tidalarr/`
- **Download Path:** `/downloads/tidalarr/`
- **API Key:** `5aefa49b428444b0973b52e7e13a26b2`
- **Port:** 8691

## How to Test

1. **Build:**
   ```bash
   dotnet build src/Tidalarr/Tidalarr.csproj --configuration Release
   ```

2. **Deploy:**
   ```bash
   cp bin/Lidarr.Plugin.Tidalarr.dll /path/to/plugins/Tidalarr/
   cp bin/Lidarr.Plugin.Abstractions.dll /path/to/plugins/Tidalarr/
   ```

3. **Restart container:**
   ```bash
   docker restart lidarr-multi-plugin-persist
   ```

4. **Test search via API:**
   ```bash
   curl "http://localhost:8691/api/v1/release?albumId=7" -H "X-Api-Key: 5aefa49b428444b0973b52e7e13a26b2"
   ```

5. **Check logs:**
   ```bash
   docker logs lidarr-multi-plugin-persist 2>&1 | grep -i tidal
   ```

## Download Flow (Working)

1. User clicks grab on Tidalarr result in Lidarr UI
2. `TidalLidarrDownloadClient.Download()` is called
3. Services initialized via `TidalModule.RegisterServices()` and `CreateOrchestrator()`
4. `SimpleDownloadOrchestrator.DownloadAlbumAsync()` called
5. `getTrackIds` delegate fetches album with tracks via `ITidalCore.GetAlbumWithTracksAsync()`
6. For each track:
   - `TidalChunkStreamProvider.GetStreamAsync()` called
   - `TidalStreamService.GetParsedManifestAsync()` fetches playback info and parses DASH manifest
   - `TidalChunkDownloader.DownloadAndAssembleAsync()` downloads all chunks and assembles into MemoryStream
   - Orchestrator writes stream to file with track name
7. Download completes with file count reported

## Related Files (Not Modified This Session)

- `TidalOAuthService.cs` - OAuth authentication (working)
- `TidalStreamService.cs` - Stream manifest fetching (working)
- `TidalChunkDownloader.cs` - Chunk download and assembly (working)
- `FileTokenStore.cs` - Token persistence (working)
- `TidalModelMapper.cs` - Model mapping between Tidal and common library types

## Architecture Notes

### Service Registration
The plugin uses two service containers:
1. **Indexer**: Initialized via `TidalLidarrIndexer.EnsureServicesInitialized()` with indexer settings
2. **Download Client**: Initialized via `TidalLidarrDownloadClient.EnsureServicesInitialized()` with download settings

Both share authentication via the same `ConfigPath` pointing to `/config/tidalarr/` where tokens are stored.

### Key Interfaces
- `ITidalCore` - Main API interface (implemented by `TidalApiClient`)
- `IAudioStreamProvider` - Stream provider interface (implemented by `TidalChunkStreamProvider`)
- `IStreamingDownloadOrchestrator` - Download orchestrator interface (implemented by `SimpleDownloadOrchestrator` in common library)

## Commit Information

- **Branch:** `fix/persistent-pkce`
- **Files Changed:** 8
- **Insertions:** ~316 lines
- **Deletions:** ~35 lines
