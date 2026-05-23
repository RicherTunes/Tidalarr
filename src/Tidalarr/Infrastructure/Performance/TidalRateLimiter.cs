using Lidarr.Plugin.Common.Services.Performance;
using Microsoft.Extensions.Logging;

namespace Tidalarr.Infrastructure.Performance;

/// <summary>
/// Tidal-specific adapter around the shared UniversalAdaptiveRateLimiter.
/// Ensures all limiter operations are consistently tagged with the "Tidal" service name
/// and exposes convenience helpers for diagnostics.
/// </summary>
public sealed class TidalRateLimiter(ILogger<TidalRateLimiter>? logger = null) : IUniversalAdaptiveRateLimiter
{
    private const string Service = "Tidal";

    private readonly UniversalAdaptiveRateLimiter _inner = new();
    private readonly ILogger<TidalRateLimiter>? _logger = logger;
    private bool _disposed;

    public Task<bool> WaitIfNeededAsync(string service, string endpoint, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        string normalizedService = NormalizeService(service);
        this._logger?.LogTrace("Rate limiter wait request for {Service}:{Endpoint}", normalizedService, endpoint);
        return this._inner.WaitIfNeededAsync(normalizedService, endpoint ?? string.Empty, cancellationToken);
    }

    public void RecordResponse(string service, string endpoint, HttpResponseMessage response)
    {
        if (this._disposed)
        {
            return;
        }

        string normalizedService = NormalizeService(service);
        this._inner.RecordResponse(normalizedService, endpoint ?? string.Empty, response);
    }

    public void RecordAuthFailure(string service, string endpoint)
    {
        if (this._disposed)
        {
            return;
        }

        this._inner.RecordAuthFailure(NormalizeService(service), endpoint ?? string.Empty);
    }

    public int GetCurrentLimit(string service, string endpoint)
    {
        ThrowIfDisposed();
        return this._inner.GetCurrentLimit(NormalizeService(service), endpoint ?? string.Empty);
    }

    public ServiceRateLimitStats GetServiceStats(string service)
    {
        ThrowIfDisposed();
        return this._inner.GetServiceStats(NormalizeService(service));
    }

    public GlobalRateLimitStats GetGlobalStats()
    {
        ThrowIfDisposed();
        return this._inner.GetGlobalStats();
    }

    public ServiceRateLimitStats GetTidalStats()
    {
        return GetServiceStats(Service);
    }

    public Task<bool> WaitIfNeededAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        return WaitIfNeededAsync(Service, endpoint, cancellationToken);
    }

    public void RecordResponse(string endpoint, HttpResponseMessage response)
    {
        RecordResponse(Service, endpoint, response);
    }

    public void Dispose()
    {
        if (this._disposed)
        {
            return;
        }

        this._disposed = true;
        this._inner.Dispose();
    }

    private static string NormalizeService(string service)
    {
        return string.IsNullOrWhiteSpace(service) ? Service : service;
    }

    private void ThrowIfDisposed()
    {
        if (this._disposed)
        {
            throw new ObjectDisposedException(nameof(TidalRateLimiter));
        }
    }
}

