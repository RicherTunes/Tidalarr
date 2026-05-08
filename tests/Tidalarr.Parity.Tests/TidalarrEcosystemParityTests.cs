using System.Reflection;
using Lidarr.Plugin.Common.TestKit.Compliance;
using Tidalarr.Integration;
using Xunit;

namespace Tidalarr.Parity.Tests;

[Trait("Category", "Parity")]
public class TidalarrEcosystemParityTests : EcosystemParityTestBase
{
    protected override string RepoRootPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    protected override string PluginId => "tidalarr";

    protected override string PluginJsonRelativePath => "plugin.json";

    // Wave 6/10 opt-in: enable behavior-contract checks (FileTokenStore, HttpResponseCache,
    // BridgeDefaults, ManifestCapabilities backing types, ConfigPathRoots).
    // Tidalarr completed Phase 1-5 unification (FileTokenStore<TidalTokens>,
    // StreamingResponseCache, AddBridgeDefaults, deleted local ConfigPathDefaults).
    protected override Assembly? PluginAssembly => typeof(TidalarrPlugin).Assembly;

    [Fact] public void Check_UsesCommonFileTokenStore_Test() => Assert.True(Check_UsesCommonFileTokenStore().Passed, string.Join("; ", Check_UsesCommonFileTokenStore().Errors));
    [Fact] public void Check_UsesCommonHttpResponseCache_Test() => Assert.True(Check_UsesCommonHttpResponseCache().Passed, string.Join("; ", Check_UsesCommonHttpResponseCache().Errors));
    [Fact] public void Check_RegistersBridgeDefaults_Test() => Assert.True(Check_RegistersBridgeDefaults().Passed, string.Join("; ", Check_RegistersBridgeDefaults().Errors));
    [Fact] public void Check_PluginManifest_Capabilities_HaveBackingTypes_Test() => Assert.True(Check_PluginManifest_Capabilities_HaveBackingTypes().Passed, string.Join("; ", Check_PluginManifest_Capabilities_HaveBackingTypes().Errors));
    [Fact] public void Check_NoFluentValidation_ErrorsApi_Drift_Test() => Assert.True(Check_NoFluentValidation_ErrorsApi_Drift().Passed, string.Join("; ", Check_NoFluentValidation_ErrorsApi_Drift().Errors));
    [Fact] public void Check_UsesCommonPluginConfigRoots_Test() => Assert.True(Check_UsesCommonPluginConfigRoots().Passed, string.Join("; ", Check_UsesCommonPluginConfigRoots().Errors));

    [Fact] public void DirectoryBuildProps_Exists_Test() => Assert.True(DirectoryBuildProps_Exists().Passed, string.Join("; ", DirectoryBuildProps_Exists().Errors));
    [Fact] public void DirectoryBuildProps_HasILRepackDisabled_Test() => Assert.True(DirectoryBuildProps_HasILRepackDisabled().Passed, string.Join("; ", DirectoryBuildProps_HasILRepackDisabled().Errors));
    [Fact] public void DirectoryBuildProps_HasVersionManagement_Test() => Assert.True(DirectoryBuildProps_HasVersionManagement().Passed, string.Join("; ", DirectoryBuildProps_HasVersionManagement().Errors));
    [Fact] public void DirectoryBuildProps_HasSourceLink_Test() => Assert.True(DirectoryBuildProps_HasSourceLink().Passed, string.Join("; ", DirectoryBuildProps_HasSourceLink().Errors));
    [Fact] public void DirectoryBuildProps_HasNoWarnSuppression_Test() => Assert.True(DirectoryBuildProps_HasNoWarnSuppression().Passed, string.Join("; ", DirectoryBuildProps_HasNoWarnSuppression().Errors));
    [Fact] public void DirectoryBuildProps_HasCPMExclusion_Test() => Assert.True(DirectoryBuildProps_HasCPMExclusion().Passed, string.Join("; ", DirectoryBuildProps_HasCPMExclusion().Errors));
    [Fact] public void DirectoryBuildProps_HasDeterministic_Test() => Assert.True(DirectoryBuildProps_HasDeterministic().Passed, string.Join("; ", DirectoryBuildProps_HasDeterministic().Errors));
    [Fact] public void DirectoryPackagesProps_Exists_Test() => Assert.True(DirectoryPackagesProps_Exists().Passed, string.Join("; ", DirectoryPackagesProps_Exists().Errors));
    [Fact] public void DirectoryPackagesProps_EnablesCPM_Test() => Assert.True(DirectoryPackagesProps_EnablesCPM().Passed, string.Join("; ", DirectoryPackagesProps_EnablesCPM().Errors));
    [Fact] public void PluginJson_HasAllRequiredFields_Test() => Assert.True(PluginJson_HasAllRequiredFields().Passed, string.Join("; ", PluginJson_HasAllRequiredFields().Errors));
    [Fact] public void PluginJson_TargetFramework_IsNet8_Test() => Assert.True(PluginJson_TargetFramework_IsNet8().Passed, string.Join("; ", PluginJson_TargetFramework_IsNet8().Errors));
    [Fact] public void PluginJson_HasCommonVersion_Test() => Assert.True(PluginJson_HasCommonVersion().Passed, string.Join("; ", PluginJson_HasCommonVersion().Errors));
    [Fact] public void PluginJson_HasAuthor_Test() => Assert.True(PluginJson_HasAuthor().Passed, string.Join("; ", PluginJson_HasAuthor().Errors));
    [Fact] public void PluginJson_HasLicense_Test() => Assert.True(PluginJson_HasLicense().Passed, string.Join("; ", PluginJson_HasLicense().Errors));
    [Fact] public void PluginJson_HasTags_Test() => Assert.True(PluginJson_HasTags().Passed, string.Join("; ", PluginJson_HasTags().Errors));
    [Fact] public void PluginJson_HasRootNamespace_Test() => Assert.True(PluginJson_HasRootNamespace().Passed, string.Join("; ", PluginJson_HasRootNamespace().Errors));
    [Fact] public void PluginJson_NoNonStandardFields_Test() => Assert.True(PluginJson_NoNonStandardFields().Passed, string.Join("; ", PluginJson_NoNonStandardFields().Errors));
    [Fact] public void ManifestJson_TargetFramework_IsNet8_Test() => Assert.True(ManifestJson_TargetFramework_IsNet8().Passed, string.Join("; ", ManifestJson_TargetFramework_IsNet8().Errors));
    [Fact] public void GlobalJson_Exists_Test() => Assert.True(GlobalJson_Exists().Passed, string.Join("; ", GlobalJson_Exists().Errors));
    [Fact] public void GlobalJson_SdkVersion_Is8_0_100_Test() => Assert.True(GlobalJson_SdkVersion_Is8_0_100().Passed, string.Join("; ", GlobalJson_SdkVersion_Is8_0_100().Errors));

    // ---- Documented overrides ---------------------------------------------------------
    //
    // Each override below documents a known divergence with rationale, per
    // EcosystemParityTestBase.BehaviorContracts.cs guidance ("Plugins may override an
    // individual check to document a known divergence with an explicit rationale").

    /// <summary>
    /// Tidalarr's <c>FailOnIOTokenStore&lt;T&gt;</c> is not a fork of FileTokenStore — it is
    /// a deliberate fail-fast no-op store used when <c>ConfigPath</c> is missing/invalid, to
    /// prevent silent persistence to ephemeral/read-only Docker temp paths. The real durable
    /// store is common's <c>FileTokenStore&lt;TidalTokens&gt;</c>, which TidalModule selects
    /// when ConfigPath is configured (see TidalModule.cs and Phase 2 commit 4b1d901).
    /// Both implementations are thus expected; the structural rule "no plugin-local
    /// ITokenStore" does not apply.
    /// </summary>
    public override ComplianceResult Check_UsesCommonFileTokenStore() => ComplianceResult.Success;

    /// <summary>
    /// Tidalarr's <c>TidalResponseCache</c> extends common's <c>StreamingResponseCache</c>
    /// (the canonical base class) — it is not a fork. The base check uses
    /// <c>type.GetInterfaces()</c> which surfaces interfaces inherited via the base class,
    /// producing a false positive for legitimate subclasses. Tidalarr's cache only adds
    /// Tidal-specific endpoint TTLs and statistics on top of common's implementation.
    /// </summary>
    public override ComplianceResult Check_UsesCommonHttpResponseCache() => ComplianceResult.Success;

    /// <summary>
    /// The FV drift heuristic flags any <c>X.Errors</c> token in files that import
    /// FluentValidation. Tidalarr's hits are all legitimate: <c>result.Errors.Add(...)</c>
    /// (mutating the failure list — not the brittle ValidationResult getter that drifted
    /// between FV 9.x↔11.x) and spread/enumerate access on the same list. No call sites use
    /// the unstable <c>ValidationResult.Errors</c> getter signature that motivated the
    /// check.
    /// </summary>
    public override ComplianceResult Check_NoFluentValidation_ErrorsApi_Drift() => ComplianceResult.Success;
}
