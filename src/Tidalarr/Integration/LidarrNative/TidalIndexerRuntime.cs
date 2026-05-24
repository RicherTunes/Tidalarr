using Microsoft.Extensions.DependencyInjection;
using Tidalarr.Core.Models;

namespace Tidalarr.Integration.LidarrNative;

/// <summary>
/// Runtime bundle for <see cref="TidalLidarrIndexer"/>. Wraps the per-credentials
/// <see cref="IServiceProvider"/> so it can participate in the
/// <c>HostBridgeRuntimeCache</c> graveyard lifecycle (<see cref="IAsyncDisposable"/>).
/// </summary>
public sealed class TidalIndexerRuntime : IAsyncDisposable
{
    public IServiceProvider ServiceProvider { get; }

    public TidalIndexerRuntime(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
