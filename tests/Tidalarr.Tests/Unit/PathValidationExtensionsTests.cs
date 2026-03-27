using System.Runtime.InteropServices;

namespace Tidalarr.Tests.Unit;

public class PathValidationExtensionsTests
{
    [Fact]
    public void IsReasonablePath_OsNativeTempPath_ReturnsTrue()
    {
        // Use OS-native temp path - works on both Windows and Linux
        string path = Path.Combine(Path.GetTempPath(), "test", "file.txt");
        Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Fact]
    public void IsReasonablePath_OsNativeAbsolutePath_ReturnsTrue()
    {
        // Use OS-appropriate absolute path
        string path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\temp\file.txt"
            : "/tmp/file.txt";
        Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid|path")]
    [InlineData("relative/path")]
    public void IsReasonablePath_Invalid_ReturnsFalse(string? path)
    {
        Assert.False(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Fact]
    public void IsReasonablePath_WindowsDrivePath_BehavesCorrectlyPerPlatform()
    {
        // Common's PathValidation.IsReasonablePath() is permissive:
        // - Checks for invalid characters
        // - Checks for non-empty root
        // - Does NOT enforce OS-specific rules (avoids host dependencies)
        //
        // Therefore, Windows drive paths are considered "reasonable" on all platforms
        string path = "C:/temp/file.txt";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path),
                "Windows drive path should be reasonable on Windows (permissive check)");
        }
        else
        {
            // On non-Windows, Common's permissive check still accepts drive paths
            // (no OS-specific filtering in IsReasonablePath())
            Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path),
                "Permissive validation accepts drive paths on non-Windows");
        }
    }

    [Fact]
    public void IsReasonablePath_Unc_BehavesCorrectlyPerPlatform()
    {
        // Common's PathValidation.IsReasonablePath() is permissive:
        // - Checks for invalid characters
        // - Checks for non-empty root
        // - Does NOT enforce OS-specific rules (avoids host dependencies)
        //
        // Therefore, UNC paths are considered "reasonable" on all platforms
        string unc = "\\\\server\\share\\folder";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(unc),
                "UNC path should be reasonable on Windows (permissive check)");
        }
        else
        {
            // On non-Windows, Common's permissive check still accepts UNC paths
            // (no OS-specific filtering in IsReasonablePath())
            Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(unc),
                "Permissive validation accepts UNC paths on non-Windows");
        }
    }

    [Fact]
    public void IsReasonablePath_LongPath_ReturnsTrue()
    {
        // Use OS-appropriate long path
        string longSegment = new('a', 200);
        string path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"C:/{longSegment}/file"
            : $"/tmp/{longSegment}/file";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
        }
        else
        {
            // Long paths should still be valid on Linux
            Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
        }
    }

    // --- Wave 2: expanded coverage ---

    [Theory]
    [Trait("Category", "Wave2")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void IsReasonablePath_WhitespaceOnly_ReturnsFalse(string path)
    {
        Assert.False(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Fact]
    [Trait("Category", "Wave2")]
    public void IsReasonablePath_PathWithSpaces_ReturnsTrue()
    {
        // Paths with spaces are valid on all platforms
        string path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\Program Files\My App\data"
            : "/home/user/my folder/data";
        Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Fact]
    [Trait("Category", "Wave2")]
    public void IsReasonablePath_DelegatesTo_CommonPathValidation()
    {
        // Verify the extension method and Common's PathValidation agree on all cases
        string validPath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\temp\test"
            : "/tmp/test";

        Assert.Equal(
            Lidarr.Plugin.Common.Utilities.PathValidation.IsReasonablePath(validPath),
            Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(validPath));

        Assert.Equal(
            Lidarr.Plugin.Common.Utilities.PathValidation.IsReasonablePath(null),
            Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(null));

        Assert.Equal(
            Lidarr.Plugin.Common.Utilities.PathValidation.IsReasonablePath(""),
            Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(""));
    }

    [Fact]
    [Trait("Category", "Wave2")]
    public void IsReasonablePath_RootPathOnly_ReturnsTrue()
    {
        // A root path with no segments should still be valid
        string root = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"C:\" : "/";
        Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(root));
    }

    [Fact]
    [Trait("Category", "Wave2")]
    public void IsReasonablePath_PathWithDotsAndHyphens_ReturnsTrue()
    {
        // Typical download paths with dots and hyphens
        string path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\music\artist-name\album-2024.01\track-01.flac"
            : "/music/artist-name/album-2024.01/track-01.flac";
        Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
    }
}
