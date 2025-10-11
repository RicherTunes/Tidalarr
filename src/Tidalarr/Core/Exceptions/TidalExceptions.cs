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

public class TidalRateLimitException : TidalException
{
    public int RetryAfterSeconds { get; }

    public TidalRateLimitException(int retryAfterSeconds, string message) : base(message)
    {
        RetryAfterSeconds = retryAfterSeconds;
    }
}

public class TidalStreamUnavailableException : TidalException
{
    public string TrackId { get; }
    public TidalQuality RequestedQuality { get; }

    public TidalStreamUnavailableException(string trackId, TidalQuality quality, string message) : base(message)
    {
        TrackId = trackId;
        RequestedQuality = quality;
    }
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

public class TidalManifestException : TidalException
{
    public string ManifestType { get; }

    public TidalManifestException(string manifestType, string message) : base(message)
    {
        ManifestType = manifestType;
    }
}

