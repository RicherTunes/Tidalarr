using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Lidarr.Plugin.Common.Services.Performance;
using Microsoft.Extensions.Logging;

namespace Tidalarr.Infrastructure.Performance;

/// <summary>
/// Tidal-specific adapter around the shared UniversalAdaptiveRateLimiter.
/// Ensures all limiter operations are consistently tagged with the "Tidal" service name
/// and exposes convenience helpers for diagnostics.
/// </summary>
public sealed class TidalRateLimiter : IUniversalAdaptiveRateLimiter
{
    private const string Service = "Tidal";

    private readonly UniversalAdaptiveRateLimiter _inner;
    private readonly ILogger<TidalRateLimiter>? _logger;
    private bool _disposed;

    public TidalRateLimiter(ILogger<TidalRateLimiter>? logger = null)
    {
        _logger = logger;
        _inner = new UniversalAdaptiveRateLimiter();
    }

    public Task<bool> WaitIfNeededAsync(string service, string endpoint, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var normalizedService = NormalizeService(service);
        _logger?.LogTrace("Rate limiter wait request for {Service}:{Endpoint}", normalizedService, endpoint);
        return _inner.WaitIfNeededAsync(normalizedService, endpoint ?? string.Empty, cancellationToken);
    }

    public void RecordResponse(string service, string endpoint, HttpResponseMessage response)
    {
        if (_disposed)
        {
            return;
        }

        var normalizedService = NormalizeService(service);
        _inner.RecordResponse(normalizedService, endpoint ?? string.Empty, response);
    }

    public int GetCurrentLimit(string service, string endpoint)
    {
        ThrowIfDisposed();
        return _inner.GetCurrentLimit(NormalizeService(service), endpoint ?? string.Empty);
    }

    public ServiceRateLimitStats GetServiceStats(string service)
    {
        ThrowIfDisposed();
        return _inner.GetServiceStats(NormalizeService(service));
    }

    public GlobalRateLimitStats GetGlobalStats()
    {
        ThrowIfDisposed();
        return _inner.GetGlobalStats();
    }

    public ServiceRateLimitStats GetTidalStats() => GetServiceStats(Service);

    public Task<bool> WaitIfNeededAsync(string endpoint, CancellationToken cancellationToken = default)
        => WaitIfNeededAsync(Service, endpoint, cancellationToken);

    public void RecordResponse(string endpoint, HttpResponseMessage response)
        => RecordResponse(Service, endpoint, response);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _inner.Dispose();
    }

    private static string NormalizeService(string service)
        => string.IsNullOrWhiteSpace(service) ? Service : service;

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TidalRateLimiter));
        }
    }
}

