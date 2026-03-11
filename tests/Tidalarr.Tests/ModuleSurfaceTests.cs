using System.Reflection;
using System.Linq;
using Tidalarr.Integration;

namespace Tidalarr.Tests;

/// <summary>
/// Tripwire tests that enforce TidalModule's public API surface.
/// These prevent deleted dead code from drifting back via merge conflicts.
/// See AGENTS.md "Architecture Debt" section for context.
/// </summary>
public class TidalModuleSurfaceTests
{
    private static readonly Type TidalModuleType = typeof(TidalModule);

    /// <summary>
    /// Asserts that TidalModule.CreateIndexer does NOT exist.
    /// This static method was deleted as it had 0 call sites.
    /// If this test fails, someone re-added dead code - remove it again.
    /// </summary>
    [Fact]
    public void TidalModule_CreateIndexer_DoesNotExist()
    {
        MethodInfo? method = TidalModuleType.GetMethod(
            "CreateIndexer",
            BindingFlags.Public | BindingFlags.Static);

        Assert.Null(method);
    }

    /// <summary>
    /// Asserts that TidalModule.CreateDownloadClient does NOT exist.
    /// This static method was deleted as it had 0 call sites.
    /// If this test fails, someone re-added dead code - remove it again.
    /// </summary>
    [Fact]
    public void TidalModule_CreateDownloadClient_DoesNotExist()
    {
        MethodInfo? method = TidalModuleType.GetMethod(
            "CreateDownloadClient",
            BindingFlags.Public | BindingFlags.Static);

        Assert.Null(method);
    }

    /// <summary>
    /// Asserts that the intended public static methods DO exist.
    /// This ensures the tripwire tests are valid (we're testing the right type).
    /// </summary>
    [Theory]
    [InlineData("RegisterServices")]
    [InlineData("CreateOrchestrator")]
    public void TidalModule_IntendedMethods_Exist(string methodName)
    {
        MethodInfo? method = TidalModuleType.GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
    }

    /// <summary>
    /// Validates that ValidateConfiguration overloads exist (one per settings type).
    /// Tested separately because GetMethod throws AmbiguousMatchException for overloaded methods.
    /// </summary>
    [Fact]
    public void TidalModule_ValidateConfiguration_OverloadsExist()
    {
        MethodInfo[] methods = TidalModuleType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "ValidateConfiguration")
            .ToArray();

        // Should have exactly 2 overloads: TidalIndexerSettings and TidalDownloadClientSettings
        Assert.Equal(2, methods.Length);
    }

    /// <summary>
    /// Documents the expected public static method count.
    /// If this changes, someone added or removed methods - review intentionally.
    /// </summary>
    [Fact]
    public void TidalModule_PublicStaticMethodCount_IsExpected()
    {
        MethodInfo[] publicStaticMethods = TidalModuleType.GetMethods(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        // Expected: RegisterServices, ValidateConfiguration (x2 overloads), CreateOrchestrator
        Assert.Equal(4, publicStaticMethods.Length);
    }
}
