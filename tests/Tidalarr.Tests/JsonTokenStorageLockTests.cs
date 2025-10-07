using Tidalarr.Core.Models;
using Tidalarr.Infrastructure.Storage;
using Xunit;

namespace Tidalarr.Tests;

public class JsonTokenStorageLockTests
{
    [Fact]
    public async Task SaveTokens_WhenFileLocked_ThrowsInvalidOperation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tidalarr_lock_{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{}");

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
        var storage = new JsonTokenStorage(path);
        var tokens = new TidalTokens("at","rt","Bearer", DateTime.UtcNow.AddHours(1), "sess","US","uid");

        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.SaveTokensAsync(tokens));

        fs.Dispose();
        try { File.Delete(path); } catch { }
    }
}




