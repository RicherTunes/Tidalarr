using System.IO.Compression;
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

        string packageDirectory = Path.GetDirectoryName(packagePath) ?? throw new InvalidOperationException("Package directory could not be resolved.");
        string version = Path.GetFileName(packagePath)?.Split('-')?.Skip(1)?.FirstOrDefault() ?? "0.0.0";

        string hashPath = packagePath + ".sha256";
        string metadataPath = packagePath + ".metadata.json";

        string[] assemblies = EnumerateAssemblies(packagePath, packageDirectory, framework, configuration).ToArray();
        string hash = ComputeSha256(packagePath);

        File.WriteAllText(hashPath, hash);

        PackagingMetadata metadata = new PackagingMetadata
        {
            PackagePath = packagePath,
            HashPath = hashPath,
            MetadataPath = metadataPath,
            Framework = framework,
            Configuration = configuration,
            Version = version,
            Assemblies = assemblies
        };

        string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(metadataPath, json);
        return metadata;
    }

    private static IEnumerable<string> EnumerateAssemblies(string packagePath, string packageDirectory, string framework, string configuration)
    {
        string publishDirectory = Path.GetFullPath(Path.Combine(packageDirectory, "..", "publish", framework, configuration));
        if (Directory.Exists(publishDirectory))
        {
            foreach (string dll in Directory.EnumerateFiles(publishDirectory, "*.dll", SearchOption.TopDirectoryOnly))
            {
                yield return Path.GetFileName(dll);
            }
            yield break;
        }

        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        foreach (ZipArchiveEntry? entry in archive.Entries.Where(e => e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            yield return entry.Name;
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using FileStream stream = File.OpenRead(filePath);
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}
