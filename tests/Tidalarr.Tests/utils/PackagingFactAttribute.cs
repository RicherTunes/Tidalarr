namespace Tidalarr.Tests.Utils;

/// <summary>
/// Skips the test if no Tidalarr package zip can be located and packaging tests
/// are not required (see <see cref="Lidarr.Plugin.Common.TestKit.Packaging.PackagingTestPaths.IsStrictMode"/>).
/// </summary>
public sealed class PackagingFactAttribute()
    : Lidarr.Plugin.Common.TestKit.Packaging.PackagingFactAttribute("Tidalarr")
{ }
