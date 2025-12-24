namespace Tidalarr.Tests.Utils;

public sealed class PackagingFactAttribute : FactAttribute
{
    public PackagingFactAttribute()
    {
        if (PackagingTestPaths.IsStrictMode())
        {
            return;
        }

        if (PackagingTestPaths.TryFindPackagePath() == null)
        {
            Skip =
                "Tidalarr package not found. Run `./build.ps1 -Package -Configuration Release` " +
                "or set `TIDALARR_PACKAGE_PATH` to enable packaging policy tests.";
        }
    }
}

