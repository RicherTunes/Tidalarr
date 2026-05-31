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
        // Common's PathValidation.IsReasonablePath() requires a non-empty root
        // (Path.GetPathRoot). Rootedness is inherently OS-specific:
        // - Windows recognizes "C:/..." as rooted (root "C:\"), so it's reasonable.
        // - On Linux, Path.GetPathRoot("C:/temp/...") returns "" (drive letters are
        //   not roots there), so the same string is treated as unrooted -> not reasonable.
        string path = "C:/temp/file.txt";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path),
                "Windows drive path is rooted on Windows -> reasonable");
        }
        else
        {
            // On Linux, "C:/..." has no path root, so the permissive check rejects it.
            Assert.False(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path),
                "Windows drive path is not rooted on non-Windows -> not reasonable");
        }
    }

    [Fact]
    public void IsReasonablePath_Unc_BehavesCorrectlyPerPlatform()
    {
        // Common's PathValidation.IsReasonablePath() requires a non-empty root
        // (Path.GetPathRoot). UNC rootedness is OS-specific:
        // - Windows recognizes "\\server\share\..." as rooted (UNC root), so reasonable.
        // - On Linux, backslash is an ordinary filename char (not a separator), so
        //   Path.GetPathRoot returns "" and the string is treated as unrooted.
        string unc = "\\\\server\\share\\folder";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(unc),
                "UNC path is rooted on Windows -> reasonable");
        }
        else
        {
            // On Linux, a UNC-style string has no path root, so the check rejects it.
            Assert.False(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(unc),
                "UNC path is not rooted on non-Windows -> not reasonable");
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
