using System.Text.Json;

namespace Tidalarr.Infrastructure.Storage;

public sealed class PkceStateFileStore
{
    private readonly string _statePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public PkceStateFileStore(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("ConfigPath is required", nameof(configPath));

        Directory.CreateDirectory(configPath);
        _statePath = Path.Combine(configPath, "tidal_pkce_state.json");
    }

    public string StatePath => _statePath;

    public PkceState? TryLoad()
    {
        try
        {
            if (!File.Exists(_statePath))
                return null;

            string json = File.ReadAllText(_statePath);
            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<PkceState>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void Save(PkceState state)
    {
        if (state is null) throw new ArgumentNullException(nameof(state));      

        string json = JsonSerializer.Serialize(state, JsonOptions);
        string tmp = _statePath + ".tmp";

        try
        {
            File.WriteAllText(tmp, json);
            File.Move(tmp, _statePath, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(tmp))
                    File.Delete(tmp);
            }
            catch
            {
            }
        }
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_statePath))
                File.Delete(_statePath);
        }
        catch
        {
        }
    }
}
