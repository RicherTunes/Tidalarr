using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace Tidalarr.Integration;

public static class PackagingHelper
{
    public static PackagingMetadata WritePackagingMetadata(string packagePath, string framework, string configuration)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            throw new ArgumentException("Package path is required", nameof(packagePath));
        }

        var packageDirectory = Path.GetDirectoryName(packagePath) ?? throw new InvalidOperationException("Package directory could not be resolved.");
        var version = Path.GetFileName(packagePath)?.Split('-')?.Skip(1)?.FirstOrDefault() ?? "0.0.0";

        var hashPath = packagePath + ".sha256";
        var metadataPath = packagePath + ".metadata.json";

        var assemblies = EnumerateAssemblies(packagePath, packageDirectory, framework, configuration).ToArray();
        var hash = ComputeSha256(packagePath);

        File.WriteAllText(hashPath, hash);

        var metadata = new PackagingMetadata
        {
            PackagePath = packagePath,
            HashPath = hashPath,
            MetadataPath = metadataPath,
            Framework = framework,
            Configuration = configuration,
            Version = version,
            Assemblies = assemblies
        };

        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(metadataPath, json);
        return metadata;
    }

    private static IEnumerable<string> EnumerateAssemblies(string packagePath, string packageDirectory, string framework, string configuration)
    {
        var publishDirectory = Path.GetFullPath(Path.Combine(packageDirectory, "..", "publish", framework, configuration));
        if (Directory.Exists(publishDirectory))
        {
            foreach (var dll in Directory.EnumerateFiles(publishDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                yield return Path.GetFileName(dll);
            }
            yield break;
        }

        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var entry in archive.Entries.Where(e => e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            yield return entry.Name;
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
