using Lidarr.Plugin.Common.TestKit.Compliance;
using Xunit;

namespace Tidalarr.Parity.Tests;

[Trait("Category", "Parity")]
public class TidalarrEcosystemParityTests : EcosystemParityTestBase
{
    protected override string RepoRootPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    protected override string PluginId => "tidalarr";

    protected override string PluginJsonRelativePath => "plugin.json";

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
}
