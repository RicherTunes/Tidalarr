namespace Tidalarr.Core.Models;

/// <summary>
/// Represents the result of an OAuth callback.
/// </summary>
public record TidalCallbackResult(
    bool IsSuccess,
    string AuthCode,
    string State,
    string ErrorMessage)
{
    /// <summary>
    /// Creates a successful callback result.
    /// </summary>
    public static TidalCallbackResult Success(string authCode, string state)
    {
        return new(true, authCode, state, string.Empty);
    }

    /// <summary>
    /// Creates a failed callback result with an error message.
    /// </summary>
    public static TidalCallbackResult Failure(string errorMessage)
    {
        return new(false, string.Empty, string.Empty, errorMessage);
    }
}
