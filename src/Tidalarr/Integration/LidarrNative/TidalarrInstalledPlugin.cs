namespace Tidalarr.Integration;

/// <summary>
/// Registers Tidalarr with Lidarr's "System → Plugins" UI.
///
/// Lidarr has TWO distinct <c>IPlugin</c> interfaces (easy to conflate):
///
/// <list type="bullet">
///   <item><c>NzbDrone.Core.Plugins.IPlugin</c> (from <c>Lidarr.Core.dll</c>) — the host's
///         interface, used by <c>PluginService.GetInstalledPlugins()</c> /
///         <c>/api/v1/system/plugins</c> for plugin management (UI listing, update checks,
///         uninstall).</item>
///   <item><c>Lidarr.Plugin.Abstractions.IPlugin</c> (Common, internalized via ILRepack) —
///         the cross-ALC sandbox contract, never read by the live host.</item>
/// </list>
///
/// Tidalarr's <see cref="TidalarrPlugin"/> satisfies Common's contract for the bridge,
/// and <c>TidalIndexer</c>/<c>TidalDownloadClient</c> are discovered through their Lidarr base
/// classes. Neither satisfies the host's <see cref="IPlugin"/>, so without this class the
/// plugin loads fully and works (Indexer + DownloadClient schemas list it) but is invisible to
/// the System → Plugins UI and cannot be auto-updated / uninstalled through the UI.
///
/// DryIoc's <c>RegisterMany</c> auto-discovers this class from the loaded plugin assembly.
/// <c>InstalledVersion</c> is auto-derived from
/// <see cref="System.Reflection.AssemblyInformationalVersionAttribute"/> by the base class
/// (Tidalarr.csproj wires the version from the top-level VERSION file).
/// </summary>
public sealed class TidalarrInstalledPlugin : NzbDrone.Core.Plugins.Plugin
{
    public override string Name => "Tidalarr";
    public override string Owner => "RicherTunes";
    public override string GithubUrl => "https://github.com/RicherTunes/Tidalarr";
}
