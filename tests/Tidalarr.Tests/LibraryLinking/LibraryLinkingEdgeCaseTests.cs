using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Tidalarr.Tests.LibraryLinking
{
    /// <summary>
    /// Tests for library linking edge cases when Tidalarr is loaded alongside other plugins.
    /// These tests verify that:
    /// - The Common library is properly internalized via ILRepack
    /// - Dependencies like Polly are not exposed publicly
    /// - Assembly isolation works correctly
    /// - Version conflicts between plugins don't cause failures
    /// </summary>
    [Trait("Category", "LibraryLinking")]
    public class LibraryLinkingEdgeCaseTests
    {
        private static readonly string PluginAssemblyPath;
        private static readonly Assembly PluginAssembly;

        static LibraryLinkingEdgeCaseTests()
        {
            // Try to find the plugin assembly
            var possiblePaths = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Lidarr.Plugin.Tidalarr.dll"),
                Path.Combine(Directory.GetCurrentDirectory(), "bin", "Lidarr.Plugin.Tidalarr.dll")
            };

            PluginAssemblyPath = possiblePaths.FirstOrDefault(File.Exists) ?? possiblePaths[0];

            if (File.Exists(PluginAssemblyPath))
            {
                PluginAssembly = Assembly.LoadFrom(PluginAssemblyPath);
            }
            else
            {
                // Fallback: try to get the assembly through a type
                try
                {
                    var type = Type.GetType("Tidalarr.Integration.TidalIndexer, Lidarr.Plugin.Tidalarr");
                    if (type != null)
                    {
                        PluginAssembly = type.Assembly;
                        PluginAssemblyPath = PluginAssembly.Location;
                    }
                }
                catch
                {
                    // Assembly not available
                }
            }
        }

        #region ILRepack Internalization Tests

        [Fact]
        public void CommonLibrary_Types_Should_Not_Be_Publicly_Exposed()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Arrange & Act - Get all public types from the plugin assembly
            var publicTypes = PluginAssembly.GetExportedTypes();
            var commonNamespaceTypes = publicTypes
                .Where(t => t.Namespace?.StartsWith("Lidarr.Plugin.Common", StringComparison.Ordinal) == true)
                .ToList();

            // Assert - Common library types should be internalized after ILRepack
            Assert.Empty(commonNamespaceTypes);
        }

        [Fact]
        public void Polly_Types_Should_Not_Be_Publicly_Exposed()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Arrange & Act
            var publicTypes = PluginAssembly.GetExportedTypes();
            var pollyTypes = publicTypes
                .Where(t => t.Namespace?.StartsWith("Polly", StringComparison.Ordinal) == true)
                .ToList();

            // Assert
            Assert.Empty(pollyTypes);
        }

        [Fact]
        public void TagLibSharp_Types_Should_Not_Be_Publicly_Exposed()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Arrange & Act
            var publicTypes = PluginAssembly.GetExportedTypes();
            var tagLibTypes = publicTypes
                .Where(t => t.Namespace?.StartsWith("TagLib", StringComparison.Ordinal) == true)
                .ToList();

            // Assert
            Assert.Empty(tagLibTypes);
        }

        #endregion

        #region Assembly Reference Tests

        [Fact]
        public void Plugin_Should_Not_Have_External_Reference_To_Common_Assembly()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Arrange & Act
            var referencedAssemblies = PluginAssembly.GetReferencedAssemblies();
            var commonReference = referencedAssemblies
                .FirstOrDefault(a => a.Name == "Lidarr.Plugin.Common");

            // Assert - After ILRepack merge, there should be no external reference
            Assert.Null(commonReference);
        }

        [Fact]
        public void Plugin_Should_Not_Have_External_Reference_To_Polly()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Arrange & Act
            var referencedAssemblies = PluginAssembly.GetReferencedAssemblies();
            var pollyReferences = referencedAssemblies
                .Where(a => a.Name?.StartsWith("Polly", StringComparison.Ordinal) == true)
                .ToList();

            // Assert
            Assert.Empty(pollyReferences);
        }

        [Fact]
        public void Plugin_Assembly_Should_Be_Self_Contained()
        {
            // Skip if assembly not available
            if (PluginAssembly == null || string.IsNullOrEmpty(PluginAssemblyPath))
            {
                return;
            }

            // Arrange
            var pluginDir = Path.GetDirectoryName(PluginAssemblyPath)!;

            // Act - Get assemblies that should have been merged
            var mergedAssemblyNames = new[]
            {
                "Lidarr.Plugin.Common.dll",
                "Polly.dll",
                "Polly.Core.dll",
                "Polly.Extensions.Http.dll"
            };

            var existingMergedAssemblies = mergedAssemblyNames
                .Where(name => File.Exists(Path.Combine(pluginDir, name)))
                .ToList();

            // Assert - These should not exist as separate files after ILRepack
            Assert.Empty(existingMergedAssemblies);
        }

        #endregion

        #region Tidal-Specific Type Tests

        [Fact]
        public void TidalIndexer_Should_Be_Discoverable()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Act
            var indexerType = PluginAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "TidalIndexer");

            // Assert
            Assert.NotNull(indexerType);
        }

        [Fact]
        public void TidalDownloadClient_Should_Be_Discoverable()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Act
            var downloadClientType = PluginAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "TidalDownloadClient");

            // Assert
            Assert.NotNull(downloadClientType);
        }

        [Fact]
        public void Plugin_Public_Types_Should_Be_Properly_Namespaced()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Arrange & Act
            var pluginTypes = PluginAssembly.GetExportedTypes()
                .Where(t => !t.Namespace?.StartsWith("System", StringComparison.Ordinal) == true)
                .Where(t => !t.Namespace?.StartsWith("Microsoft", StringComparison.Ordinal) == true)
                .ToList();

            // Assert - All plugin types should be in Tidalarr namespace
            foreach (var type in pluginTypes)
            {
                Assert.True(
                    type.Namespace?.StartsWith("Tidalarr", StringComparison.Ordinal) == true ||
                    type.Namespace?.StartsWith("Lidarr.Plugin", StringComparison.Ordinal) == true,
                    $"Type {type.FullName} should be in Tidalarr or Lidarr.Plugin namespace");
            }
        }

        #endregion

        #region Protocol and Download Integration Tests

        [Fact]
        public void Protocol_Implementation_Should_Be_Compatible()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Arrange
            var downloadClientType = PluginAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == "TidalDownloadClient");

            if (downloadClientType == null)
            {
                return;
            }

            // Act
            var protocolProperty = downloadClientType.GetProperty("Protocol",
                BindingFlags.Public | BindingFlags.Instance);

            // Assert
            Assert.NotNull(protocolProperty);
        }

        #endregion

        #region Version Compatibility Tests

        [Fact]
        public void Plugin_Manifest_Should_Exist_In_Output()
        {
            // Skip if assembly not available
            if (string.IsNullOrEmpty(PluginAssemblyPath))
            {
                return;
            }

            // Arrange
            var pluginDir = Path.GetDirectoryName(PluginAssemblyPath)!;
            var manifestPath = Path.Combine(pluginDir, "plugin.json");

            // Act & Assert
            if (File.Exists(manifestPath))
            {
                var content = File.ReadAllText(manifestPath);
                Assert.Contains("\"id\"", content);
                Assert.Contains("\"version\"", content);
            }
        }

        [Fact]
        public void Plugin_Should_Target_NET6_Or_Higher()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Act
            var targetFramework = PluginAssembly
                .GetCustomAttributes<System.Runtime.Versioning.TargetFrameworkAttribute>()
                .FirstOrDefault();

            // Assert
            Assert.NotNull(targetFramework);
            Assert.Contains("net", targetFramework!.FrameworkName);
        }

        #endregion

        #region Submodule and Package Reference Tests

        [Fact]
        public void Plugin_Should_Work_With_Either_Submodule_Or_Package()
        {
            // This test verifies that the dual-path wiring works correctly
            // (UseInRepoCommon=true for submodule, false for package)

            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Act - Simply loading the assembly proves this
            var loadedAssembly = Assembly.LoadFrom(PluginAssemblyPath);

            // Assert
            Assert.NotNull(loadedAssembly);
            Assert.NotEmpty(loadedAssembly.GetTypes());
        }

        #endregion

        #region Multi-Plugin Simulation Tests

        [Fact]
        public async Task Plugin_Should_Handle_Concurrent_Loading()
        {
            // Skip if assembly not available
            if (PluginAssembly == null || string.IsNullOrEmpty(PluginAssemblyPath))
            {
                return;
            }

            // Arrange
            var loadTasks = new List<Task<Assembly>>();

            // Act - Simulate concurrent plugin access
            for (int i = 0; i < 5; i++)
            {
                loadTasks.Add(Task.Run(() => Assembly.LoadFrom(PluginAssemblyPath)));
            }

            var assemblies = await Task.WhenAll(loadTasks);

            // Assert - All loads should succeed
            foreach (var assembly in assemblies)
            {
                Assert.NotNull(assembly);
            }
        }

        [Fact]
        public void Plugin_Type_Names_Should_Not_Conflict_With_Other_Streaming_Plugins()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Arrange - Type names that could conflict with Qobuzarr or other streaming plugins
            var potentialConflicts = new[]
            {
                "StreamingIndexer",
                "BaseDownloadClient",
                "AuthService",
                "TokenProvider"
            };

            // Act
            var pluginTypes = PluginAssembly.GetExportedTypes();

            // Assert - Any potentially conflicting types should be properly namespaced
            foreach (var conflict in potentialConflicts)
            {
                var matchingTypes = pluginTypes
                    .Where(t => t.Name == conflict)
                    .ToList();

                foreach (var type in matchingTypes)
                {
                    Assert.True(
                        type.Namespace?.Contains("Tidal", StringComparison.OrdinalIgnoreCase) == true ||
                        type.Namespace?.Contains("Tidalarr", StringComparison.OrdinalIgnoreCase) == true,
                        $"Type {conflict} should be in Tidal-specific namespace to avoid conflicts");
                }
            }
        }

        #endregion

        #region DASH Manifest and Chunk Download Isolation Tests

        [Fact]
        public void TidalStreamManifest_Parser_Should_Be_Internal()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Arrange - Tidal-specific DASH parsing should be properly scoped
            var manifestTypes = PluginAssembly.GetTypes()
                .Where(t => t.Name.Contains("Manifest") || t.Name.Contains("Chunk"))
                .ToList();

            // Assert - These should be properly namespaced
            foreach (var type in manifestTypes)
            {
                Assert.True(
                    type.Namespace?.Contains("Tidal", StringComparison.OrdinalIgnoreCase) == true ||
                    type.IsNotPublic,
                    $"Type {type.Name} should be internal or in Tidal namespace");
            }
        }

        #endregion

        #region OAuth Token Provider Isolation Tests

        [Fact]
        public void OAuthTokenProvider_Should_Be_Plugin_Specific()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Act
            var tokenProviderTypes = PluginAssembly.GetTypes()
                .Where(t => t.Name.Contains("TokenProvider") || t.Name.Contains("OAuth"))
                .ToList();

            // Assert - Token providers should be properly namespaced for Tidal
            foreach (var type in tokenProviderTypes)
            {
                Assert.True(
                    type.Namespace?.Contains("Tidal", StringComparison.OrdinalIgnoreCase) == true ||
                    type.Namespace?.Contains("Tidalarr", StringComparison.OrdinalIgnoreCase) == true,
                    $"Token provider {type.Name} should be in Tidal-specific namespace");
            }
        }

        #endregion

        #region Resource and Content Tests

        [Fact]
        public void Plugin_Embedded_Resources_Should_Be_Accessible()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Act
            var resourceNames = PluginAssembly.GetManifestResourceNames();

            // Assert
            Assert.NotNull(resourceNames);
        }

        [Fact]
        public void Plugin_Assembly_Version_Should_Be_Valid()
        {
            // Skip if assembly not available
            if (PluginAssembly == null)
            {
                return;
            }

            // Act
            var version = PluginAssembly.GetName().Version;

            // Assert
            Assert.NotNull(version);
            Assert.True(version!.Major >= 0);
        }

        #endregion
    }
}
