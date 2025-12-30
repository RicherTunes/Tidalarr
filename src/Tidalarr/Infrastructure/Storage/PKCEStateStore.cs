using System.Text.Json;

namespace Tidalarr.Infrastructure.Storage;

/// <summary>
/// Persists PKCE state (code_verifier, state, clientUniqueKey) between OAuth authorization and token exchange.
/// Required because the PKCE flow requires the original code_verifier when exchanging the authorization code.
/// </summary>
public class PKCEStateStore
{
    private readonly string _storagePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public PKCEStateStore(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentNullException(nameof(configPath), "Config path is required for PKCE state storage");

        _storagePath = Path.Combine(configPath, "pkce_state.json");
        EnsureStorageDirectoryExists();
    }

    public async Task SaveStateAsync(PKCEState state)
    {
        try
        {
            string json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(_storagePath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save PKCE state: {ex.Message}", ex);
        }
    }

    public async Task<PKCEState?> LoadStateAsync()
    {
        try
        {
            if (!File.Exists(_storagePath))
                return null;

            string json = await File.ReadAllTextAsync(_storagePath);
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var state = JsonSerializer.Deserialize<PKCEState>(json, JsonOptions);

            // Check if state has expired (10 minutes is typical for PKCE)
            if (state != null && state.CreatedAt.AddMinutes(30) < DateTime.UtcNow)
            {
                await DeleteStateAsync();
                return null;
            }

            return state;
        }
        catch
        {
            return null;
        }
    }

    public Task DeleteStateAsync()
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

    private void EnsureStorageDirectoryExists()
    {
        string? directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}

/// <summary>
/// PKCE state data persisted between authorization URL generation and token exchange.
/// </summary>
public record PKCEState(
    string AuthorizationUrl,
    string CodeVerifier,
    string State,
    string ClientUniqueKey,
    DateTime CreatedAt);
