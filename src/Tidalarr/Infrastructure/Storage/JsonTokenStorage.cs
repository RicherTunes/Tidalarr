using System.Text.Json;
using Tidalarr.Core.Models;

namespace Tidalarr.Infrastructure.Storage;

public interface ITokenStorage
{
    Task SaveTokensAsync(TidalTokens tokens);
    Task<TidalTokens?> LoadTokensAsync();
    Task DeleteTokensAsync();
}

public class JsonTokenStorage : ITokenStorage
{
    private readonly string _storagePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public JsonTokenStorage(string? storagePath = null)
    {
        _storagePath = storagePath ?? GetDefaultStoragePath();
        EnsureStorageDirectoryExists();
    }

    public async Task SaveTokensAsync(TidalTokens tokens)
    {
        try
        {
            var json = JsonSerializer.Serialize(tokens, JsonOptions);
            await File.WriteAllTextAsync(_storagePath, json);
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
            if (!File.Exists(_storagePath))
                return null;

            var json = await File.ReadAllTextAsync(_storagePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            return JsonSerializer.Deserialize<TidalTokens>(json, JsonOptions);
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
            if (File.Exists(_storagePath))
                File.Delete(_storagePath);
        }
        catch
        {
            // Swallow deletion errors
        }
        return Task.CompletedTask;
    }

    private static string GetDefaultStoragePath()
    {
        var userDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var tidalPath = Path.Combine(userDataPath, "Tidalarr");
        return Path.Combine(tidalPath, "tidal_tokens.json");
    }

    private void EnsureStorageDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
