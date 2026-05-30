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
    [Fact] public void Check_UsesCommonDownloadTelemetrySink_Test() => Assert.True(Check_UsesCommonDownloadTelemetrySink().Passed, string.Join("; ", Check_UsesCommonDownloadTelemetrySink().Errors));
    [Fact] public void Check_UsesCommonLyricsEnricher_Test() => Assert.True(Check_UsesCommonLyricsEnricher().Passed, string.Join("; ", Check_UsesCommonLyricsEnricher().Errors));
    [Fact] public void Check_UsesCommonDiagnosticTypes_Test() => Assert.True(Check_UsesCommonDiagnosticTypes().Passed, string.Join("; ", Check_UsesCommonDiagnosticTypes().Errors));
    [Fact] public void Check_DownloadClientUsesPathTraversalGuard_Test() => Assert.True(Check_DownloadClientUsesPathTraversalGuard().Passed, string.Join("; ", Check_DownloadClientUsesPathTraversalGuard().Errors));
    [Fact] public void Check_FileClassNameParity_Test() => Assert.True(Check_FileClassNameParity().Passed, string.Join("; ", Check_FileClassNameParity().Errors));
    [Fact] public void Check_ClaudeMdDocumentsCommonHelpers_Test() => Assert.True(Check_ClaudeMdDocumentsCommonHelpers().Passed, string.Join("; ", Check_ClaudeMdDocumentsCommonHelpers().Errors));

    [Fact] public void DirectoryBuildProps_Exists_Test() => Assert.True(DirectoryBuildProps_Exists().Passed, string.Join("; ", DirectoryBuildProps_Exists().Errors));
    [Fact] public void DirectoryBuildProps_HasILRepackDisabled_Test() => Assert.True(DirectoryBuildProps_HasILRepackDisabled().Passed, string.Join("; ", DirectoryBuildProps_HasILRepackDisabled().Errors));
    [Fact] public void DirectoryBuildProps_HasVersionManagement_Test() => Assert.True(DirectoryBuildProps_HasVersionManagement().Passed, string.Join("; ", DirectoryBuildProps_HasVersionManagement().Errors));
    [Fact] public void DirectoryBuildProps_HasSourceLink_Test() => Assert.True(DirectoryBuildProps_HasSourceLink().Passed, string.Join("; ", DirectoryBuildProps_HasSourceLink().Errors));
    [Fact] public void DirectoryBuildProps_HasNoWarnSuppression_Test() => Assert.True(DirectoryBuildProps_HasNoWarnSuppression().Passed, string.Join("; ", DirectoryBuildProps_HasNoWarnSuppression().Errors));
    [Fact] public void DirectoryBuildProps_HasCPMExclusion_Test() => Assert.True(DirectoryBuildProps_HasCPMExclusion().Passed, string.Join("; ", DirectoryBuildProps_HasCPMExclusion().Errors));
    [Fact] public void DirectoryBuildProps_HasDeterministic_Test() => Assert.True(DirectoryBuildProps_HasDeterministic().Passed, string.Join("; ", DirectoryBuildProps_HasDeterministic().Errors));
    [Fact] public void DirectoryPackagesProps_Exists_Test() => Assert.True(DirectoryPackagesProps_Exists().Passed, string.Join("; ", DirectoryPackagesProps_Exists().Errors));
    [Fact] public void DirectoryPackagesProps_EnablesCPM_Test() => Assert.True(DirectoryPackagesProps_EnablesCPM().Passed, string.Join("; ", DirectoryPackagesProps_EnablesCPM().Errors));
    [Fact] public void DirectoryPackagesProps_HostVersionsMatchCanonical_Test() => Assert.True(DirectoryPackagesProps_HostVersionsMatchCanonical().Passed, string.Join("; ", DirectoryPackagesProps_HostVersionsMatchCanonical().Errors));
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
    /// Tidalarr's settings classes use <c>validation.Errors.First().ErrorMessage</c> to
    /// surface the first FV failure to Lidarr's <c>IsValid(out string errorMessage)</c>
    /// contract. This is a real LINQ-chain hit on the FV <c>ValidationResult.Errors</c>
    /// getter that wave 11's refined heuristic correctly flags. Migrated in <c>11c</c>:
    /// the 3 callsites (<c>TidalarrSettings.cs</c>, <c>TidalIndexerSettings.cs</c>,
    /// <c>TidalDownloadClientSettings.cs</c>) now use stable <c>validation.ToString()</c>
    /// — override dropped.
    /// </summary>
}
