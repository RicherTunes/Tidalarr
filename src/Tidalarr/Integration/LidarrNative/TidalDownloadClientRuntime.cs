using Lidarr.Plugin.Common.Services.Download;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Runtime bundle for <see cref="TidalLidarrDownloadClient"/>. Wraps the per-credentials
/// <see cref="IServiceProvider"/> and the <see cref="SimpleDownloadOrchestrator"/> so
/// both can participate in the <c>HostBridgeRuntimeCache</c> graveyard lifecycle.
/// </summary>
public sealed class TidalDownloadClientRuntime : IAsyncDisposable
{
    public IServiceProvider ServiceProvider { get; }
    public SimpleDownloadOrchestrator Orchestrator { get; }

    public TidalDownloadClientRuntime(IServiceProvider serviceProvider, SimpleDownloadOrchestrator orchestrator)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public async ValueTask DisposeAsync()
    {
        if (ServiceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
