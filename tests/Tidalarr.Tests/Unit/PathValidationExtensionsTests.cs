using System.IO;
using System.Runtime.InteropServices;

namespace Tidalarr.Tests.Unit;

public class PathValidationExtensionsTests
{
    [Fact]
    public void IsReasonablePath_OsNativeTempPath_ReturnsTrue()
    {
        // Use OS-native temp path - works on both Windows and Linux
        var path = Path.Combine(Path.GetTempPath(), "test", "file.txt");
        Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Fact]
    public void IsReasonablePath_OsNativeAbsolutePath_ReturnsTrue()
    {
        // Use OS-appropriate absolute path
        var path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
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
        // Windows drive paths are only valid on Windows
        var path = "C:/temp/file.txt";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path));
        }
        else
        {
            // On non-Windows, drive letter paths are not "reasonable"
            Assert.False(Integration.PathValidationExtensions.IsReasonablePath(path));
        }
    }

    [Fact]
    public void IsReasonablePath_Unc_BehavesCorrectlyPerPlatform()
    {
        // UNC paths are Windows-specific
        string unc = "\\\\server\\share\\folder";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.True(Integration.PathValidationExtensions.IsReasonablePath(unc));
        }
        else
        {
            // On non-Windows, UNC paths are not "reasonable"
            Assert.False(Integration.PathValidationExtensions.IsReasonablePath(unc));
        }
    }

    [Fact]
    public void IsReasonablePath_LongPath_ReturnsTrue()
    {
        // Use OS-appropriate long path
        string longSegment = new('a', 200);
        var path = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
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
