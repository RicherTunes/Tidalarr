using Xunit;

namespace Tidalarr.Tests.Unit;

public class PathValidationExtensionsTests
{
    [Theory]
    [InlineData("C:/temp")]
    [InlineData("C:/temp/file.txt")]
    public void IsReasonablePath_Valid_ReturnsTrue(string path)
    {
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
    public void IsReasonablePath_Unc_ReturnsTrue()
    {
        var unc = "\\\\server\\share\\folder";
        Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(unc));
    }

    [Fact]
    public void IsReasonablePath_LongLocalPath_ReturnsTrue()
    {
        var longSegment = new string('a', 260);
        var path = $"C:/{longSegment}/file";
        Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Fact]
    public void IsReasonablePath_LongUncPath_ReturnsTrue()
    {
        var longSegment = new string('b', 260);
        var path = $"\\\\server\\share\\{longSegment}\\folder";
        Assert.True(Tidalarr.Integration.PathValidationExtensions.IsReasonablePath(path));
    }
}
