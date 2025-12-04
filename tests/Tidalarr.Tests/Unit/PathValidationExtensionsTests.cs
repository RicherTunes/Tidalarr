namespace Tidalarr.Tests.Unit;

public class PathValidationExtensionsTests
{
    [Theory]
    [InlineData("C:/temp")]
    [InlineData("C:/temp/file.txt")]
    public void IsReasonablePath_Valid_ReturnsTrue(string path)
    {
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
    public void IsReasonablePath_Unc_ReturnsTrue()
    {
        string unc = "\\\\server\\share\\folder";
        Assert.True(Integration.PathValidationExtensions.IsReasonablePath(unc));
    }

    [Fact]
    public void IsReasonablePath_LongLocalPath_ReturnsTrue()
    {
        string longSegment = new('a', 260);
        string path = $"C:/{longSegment}/file";
        Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path));
    }

    [Fact]
    public void IsReasonablePath_LongUncPath_ReturnsTrue()
    {
        string longSegment = new('b', 260);
        string path = $"\\\\server\\share\\{longSegment}\\folder";
        Assert.True(Integration.PathValidationExtensions.IsReasonablePath(path));
    }
}
