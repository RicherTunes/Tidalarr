using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Storage;

namespace Tidalarr.Tests;

public class FileTokenStoreLockTests
{
    [Fact]
    public async Task SaveTokens_WhenFileLocked_ThrowsInvalidOperation()
    {
        string path = Path.Combine(Path.GetTempPath(), $"tidalarr_lock_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{}");

        await using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        FileTokenStore storage = new FileTokenStore(path);
        TidalTokens tokens = new TidalTokens("at", "rt", "Bearer", DateTime.UtcNow.AddHours(1), "sess", "US", "uid");

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveTokensAsync(tokens));

        fs.Dispose();
        try { File.Delete(path); } catch { }
    }
}




