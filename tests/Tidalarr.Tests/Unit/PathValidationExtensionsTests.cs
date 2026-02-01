using System.Runtime.InteropServices;

namespace Tidalarr.Tests.Unit;

public class PathValidationExtensionsTests
{
    [Fact]
    public void IsReasonablePath_OsNativeTempPath_ReturnsTrue()
    {
        // Use OS-native temp path - works on both Windows and Linux
        string path = Path.Combine(Path.GetTempPath(), "test", "file.txt");
        Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Fact]
    public void IsReasonablePath_OsNativeAbsolutePath_ReturnsTrue()
    {
        // Use OS-appropriate absolute path
        string path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"C:\temp\file.txt"
            : "/tmp/file.txt";
        Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid|path")]
    [InlineData("relative/path")]
    public void IsReasonablePath_Invalid_ReturnsFalse(string? path)
    {
        Assert.False(Integration.PathValidationExtensions.IsReasonablePath(path));
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
            Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path),
                "Windows drive path should be reasonable on Windows (permissive check)");
        }
        else
        {
            // On non-Windows, Common's permissive check still accepts drive paths
            // (no OS-specific filtering in IsReasonablePath())
            Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path),
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
            Assert.True(Integration.PathValidationExtensions.IsReasonablePath(unc),
                "UNC path should be reasonable on Windows (permissive check)");
        }
        else
        {
            // On non-Windows, Common's permissive check still accepts UNC paths
            // (no OS-specific filtering in IsReasonablePath())
            Assert.True(Integration.PathValidationExtensions.IsReasonablePath(unc),
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
            Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path));
        }
        else
        {
            // Long paths should still be valid on Linux
            Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path));
        }
    }
}
