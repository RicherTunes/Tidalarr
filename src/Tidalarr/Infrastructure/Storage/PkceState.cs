namespace Tidalarr.Infrastructure.Storage;

public sealed record PkceState(
    string CodeVerifier,
    string State,
    DateTimeOffset CreatedAtUtc,
    string AuthorizationUrl)
{
    public bool IsExpired(TimeSpan maxAge, DateTimeOffset nowUtc)
    {
        return nowUtc - CreatedAtUtc > maxAge;
    }
}

