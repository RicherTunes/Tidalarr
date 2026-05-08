using System.Text.Json;
using Lidarr.Plugin.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Tidalarr.Core.Models;

namespace Tidalarr.Infrastructure.Storage;

/// <summary>
/// Idempotent helper that migrates the legacy plaintext <c>tidal_tokens.json</c> file (a security
/// gap — OAuth refresh tokens persisted unencrypted to disk) into common's encrypted token store.
/// </summary>
/// <remarks>
/// The legacy <c>Tidalarr.Infrastructure.Storage.FileTokenStore</c> wrote a flat
/// <c>System.Text.Json</c> serialization of <see cref="TidalTokens"/> with no protection at rest.
/// Common's <see cref="Lidarr.Plugin.Common.Services.Authentication.FileTokenStore{TSession}"/>
/// wraps the payload in a <c>ProtectedEnvelope</c> sealed by the platform's
/// <c>TokenProtectorFactory</c> (DPAPI on Windows, Keychain on macOS, libsecret on Linux,
/// <see cref="Microsoft.AspNetCore.DataProtection"/> as a portable fallback).
/// Common's loader has its own one-shot legacy-to-protected migration path that handles its own
/// historical schema (a <c>PersistedEnvelope</c> object with <c>session</c>/<c>expiresAt</c>/<c>metadata</c>),
/// but that path doesn't recognise Tidalarr's plugin-local plaintext layout — that layout serialises
/// the <see cref="TidalTokens"/> record directly at the file root. This helper bridges that gap.
/// </remarks>
public static class LegacyTokenMigration
{
    private const string LegacyFileName = "tidal_tokens.json";

    private static readonly JsonSerializerOptions LegacyJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Migrates a legacy plaintext token file in <paramref name="configPath"/> into the supplied
    /// encrypted <paramref name="store"/>, then deletes the legacy file.
    /// </summary>
    /// <remarks>
    /// Safe to call on every plugin/CLI startup:
    /// <list type="bullet">
    /// <item><description>No-ops when <paramref name="configPath"/> is null/empty.</description></item>
    /// <item><description>No-ops when the legacy file is missing.</description></item>
    /// <item><description>No-ops when the legacy file already looks like a common-format envelope
    /// (<c>{"v":2,...}</c> or <c>{"session":...}</c>) — common's <c>FileTokenStore</c> handles those itself.</description></item>
    /// <item><description>On parse failure or malformed payload, leaves the legacy file in place and
    /// logs a warning rather than corrupting state.</description></item>
    /// </list>
    /// On success the legacy file is deleted so subsequent boots are no-ops.
    /// </remarks>
    /// <param name="configPath">Directory containing the legacy <c>tidal_tokens.json</c>.</param>
    /// <param name="store">Encrypted token store to migrate the payload into.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns><see langword="true"/> when a legacy file was migrated; otherwise <see langword="false"/>.</returns>
    public static async Task<bool> MigrateIfPresentAsync(
        string? configPath,
        ITokenStore<TidalTokens> store,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);

        if (string.IsNullOrWhiteSpace(configPath))
        {
            return false;
        }

        string legacyPath = Path.Combine(configPath, LegacyFileName);
        if (!File.Exists(legacyPath))
        {
            return false;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(legacyPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Could not read legacy token file at {Path}; leaving in place", legacyPath);
            return false;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            // Empty file - nothing to migrate but safe to remove.
            TryDelete(legacyPath, logger);
            return false;
        }

        // Skip if the file is already in common's format (protected envelope or persisted envelope).
        // Common's FileTokenStore handles those formats itself on first load.
        if (IsCommonFormat(json))
        {
            return false;
        }

        TidalTokens? legacy;
        try
        {
            legacy = JsonSerializer.Deserialize<TidalTokens>(json, LegacyJsonOptions);
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Legacy token file at {Path} is unreadable; leaving in place for manual review", legacyPath);
            return false;
        }

        if (legacy is null)
        {
            TryDelete(legacyPath, logger);
            return false;
        }

        try
        {
            await store.SaveAsync(new TokenEnvelope<TidalTokens>(legacy, legacy.ExpiresAt), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Failed to migrate legacy plaintext token file at {Path}; leaving in place", legacyPath);
            return false;
        }

        // Common's FileTokenStore writes its protected blob atomically to the same path the legacy
        // plaintext used. Saving has already overwritten the plaintext in-place, so don't try to
        // delete the legacy file here - that would wipe the encrypted ciphertext we just wrote.
        // If a future deployment ever uses a separate path for the encrypted store, this method
        // will need a "delete legacyPath only when it's a distinct file" check.
        logger?.LogInformation("Migrated legacy plaintext OAuth tokens from {Path} into encrypted token store", legacyPath);
        return true;
    }

    private static bool IsCommonFormat(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            // Common's protected envelope: {"v":2,"alg":"...","payload":"..."}
            if (root.TryGetProperty("v", out JsonElement v) && v.ValueKind == JsonValueKind.Number &&
                root.TryGetProperty("payload", out _))
            {
                return true;
            }

            // Common's persisted envelope (pre-protection legacy of the common store):
            // {"session":{...},"expiresAt":"...","metadata":{...}}
            // The TidalTokens record is serialised at the root with PascalCase or camelCase,
            // so a top-level "session" property strongly indicates common's format.
            if (root.TryGetProperty("session", out _) || root.TryGetProperty("Session", out _))
            {
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void TryDelete(string path, ILogger? logger)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Failed to delete legacy token file at {Path} after migration", path);
        }
    }
}
