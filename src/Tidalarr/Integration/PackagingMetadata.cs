namespace Tidalarr.Integration;

public sealed class PackagingMetadata
{
    public string PackagePath { get; set; } = string.Empty;
    public string HashPath { get; set; } = string.Empty;
    public string MetadataPath { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Configuration { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public IReadOnlyCollection<string> Assemblies { get; set; } = [];
}
