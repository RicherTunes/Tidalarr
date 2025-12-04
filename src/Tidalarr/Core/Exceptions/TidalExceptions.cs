using Tidalarr.Core.Models;

namespace Tidalarr.Core.Exceptions;

// Custom exception hierarchy (architect requirement)
public class TidalException : Exception
{
    public TidalException(string message) : base(message) { }
    public TidalException(string message, Exception innerException) : base(message, innerException) { }
}

public class TidalAuthenticationException : TidalException
{
    public TidalAuthenticationException(string message) : base(message) { }
    public TidalAuthenticationException(string message, Exception innerException) : base(message, innerException) { }
}

public class TidalRateLimitException(int retryAfterSeconds, string message) : TidalException(message)
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}

public class TidalStreamUnavailableException(string trackId, TidalQuality quality, string message) : TidalException(message)
{
    public string TrackId { get; } = trackId;
    public TidalQuality RequestedQuality { get; } = quality;
}

public class TidalApiException : TidalException
{
    public int? StatusCode { get; }

    public TidalApiException(string message, int? statusCode = null) : base(message)
    {
        StatusCode = statusCode;
    }

    public TidalApiException(string message, Exception innerException, int? statusCode = null) : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}

public class TidalManifestException(string manifestType, string message) : TidalException(message)
{
    public string ManifestType { get; } = manifestType;
}

