using System.Text.Json;
using Tidalarr.Core.Models;

namespace Tidalarr.Infrastructure.Storage;

/// <summary>
/// File-backed token store aligned with the shared library's naming.
/// Drop-in replacement for JsonTokenStorage; keeps the same ITokenStorage surface.
/// </summary>
public class FileTokenStore : ITokenStorage
{
    private readonly string _storagePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public FileTokenStore(string? storagePath = null)
    {
        this._storagePath = storagePath ?? GetDefaultStoragePath();
        EnsureStorageDirectoryExists();
    }

    public async Task SaveTokensAsync(TidalTokens tokens)
    {
        try
        {
            string json = JsonSerializer.Serialize(tokens, JsonOptions);
            await File.WriteAllTextAsync(this._storagePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save tokens: {ex.Message}", ex);
        }
    }

    public async Task<TidalTokens?> LoadTokensAsync()
    {
        try
        {
            if (!File.Exists(this._storagePath))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(this._storagePath);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<TidalTokens>(json, JsonOptions);
        }
        catch
        {
            return null; // Treat corruption as no session
        }
    }

    public Task DeleteTokensAsync()
    {
        try
        {
            if (File.Exists(this._storagePath))
            {
                File.Delete(this._storagePath);
            }
        }
        catch
        {
            // Swallow deletion errors
        }
        return Task.CompletedTask;
    }

    private static string GetDefaultStoragePath()
    {
        string userDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string tidalPath = Path.Combine(userDataPath, "Tidalarr");
        return Path.Combine(tidalPath, "tidal_tokens.json");
    }

    private void EnsureStorageDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(this._storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            _ = Directory.CreateDirectory(directory);
        }
    }
}

