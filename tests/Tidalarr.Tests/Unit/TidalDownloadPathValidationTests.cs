using FluentValidation.Results;
using Tidalarr.Integration.LidarrNative;

namespace Tidalarr.Tests.Unit;

/// <summary>
/// Regression guards for TidalLidarrDownloadClient's download-path validation.
///
/// Previously Test() did a NotEmpty check then jumped straight to
/// Directory.Exists / Directory.CreateDirectory — which silently accepted
/// relative paths (resolved against the process CWD) and traversal segments
/// (silently collapsed by Path.GetFullPath). Users only discovered the path
/// was wrong via confusing "Access denied" / "file not found" errors at
/// download time.
///
/// The validation is now extracted into a static
/// <see cref="TidalLidarrDownloadClient.ValidateDownloadPath"/> method that
/// runs DownloadPathValidator BEFORE the filesystem touches, so syntactically
/// bad paths get a clear save-time error.
/// </summary>
public sealed class TidalDownloadPathValidationTests
{
    [Fact]
    public void ValidateDownloadPath_Empty_Required()
    {
        var failures = new List<ValidationFailure>();
        var ok = TidalLidarrDownloadClient.ValidateDownloadPath(string.Empty, failures);

        Assert.False(ok);
        Assert.Contains(failures, f => f.PropertyName == "DownloadPath" && f.ErrorMessage.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateDownloadPath_Whitespace_Required()
    {
        var failures = new List<ValidationFailure>();
        var ok = TidalLidarrDownloadClient.ValidateDownloadPath("   ", failures);

        Assert.False(ok);
        Assert.Contains(failures, f => f.PropertyName == "DownloadPath");
    }

    [Fact]
    public void ValidateDownloadPath_RelativePath_Rejected()
    {
        // Previously silent pass (Directory.Exists resolves against CWD).
        var failures = new List<ValidationFailure>();
        var ok = TidalLidarrDownloadClient.ValidateDownloadPath("downloads/tidal", failures);

        Assert.False(ok);
        Assert.Contains(failures, f => f.PropertyName == "DownloadPath");
    }

    [Fact]
    public void ValidateDownloadPath_TraversalSegment_Rejected()
    {
        var failures = new List<ValidationFailure>();
        var path = OperatingSystem.IsWindows() ? "C:\\downloads\\..\\etc" : "/downloads/../etc";
        var ok = TidalLidarrDownloadClient.ValidateDownloadPath(path, failures);

        Assert.False(ok);
        Assert.Contains(failures, f => f.PropertyName == "DownloadPath" && f.ErrorMessage.Contains("..", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateDownloadPath_EmbeddedNul_Rejected()
    {
        var failures = new List<ValidationFailure>();
        var ok = TidalLidarrDownloadClient.ValidateDownloadPath("/downloads\0/tidal", failures);

        Assert.False(ok);
        Assert.Contains(failures, f => f.PropertyName == "DownloadPath");
    }

    [Fact]
    public void ValidateDownloadPath_TempPath_Passes()
    {
        // The helper validates SYNTAX only — filesystem touches (Exists,
        // CreateDirectory) live in Test() proper. The system temp path is a
        // good baseline: absolute, no traversal, writable on every platform.
        var failures = new List<ValidationFailure>();
        var ok = TidalLidarrDownloadClient.ValidateDownloadPath(Path.GetTempPath(), failures);

        Assert.True(ok, $"expected pass, got: [{string.Join("; ", failures.Select(f => f.ErrorMessage))}]");
        Assert.Empty(failures);
    }

    [Fact]
    public void ValidateDownloadPath_LeadingTildeOnUnix_Rejected()
    {
        // Tilde shell-expansion only happens in a shell; Lidarr's process
        // doesn't expand it. Reject up front.
        if (OperatingSystem.IsWindows()) return;

        var failures = new List<ValidationFailure>();
        var ok = TidalLidarrDownloadClient.ValidateDownloadPath("~/Music", failures);

        Assert.False(ok);
    }
}
