using System.Security.Cryptography;
using System.Text;

namespace Tidalarr.Domain.Authentication;

public class PKCEGenerator
{
    public (string codeVerifier, string codeChallenge) GeneratePair()
    {
        string codeVerifier = GenerateCodeVerifier(128);
        string codeChallenge = CreateS256Challenge(codeVerifier);
        return (codeVerifier, codeChallenge);
    }

    private string GenerateCodeVerifier(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";
        char[] result = new char[length];

        using RandomNumberGenerator rng = RandomNumberGenerator.Create();
        byte[] randomBytes = new byte[length];
        rng.GetBytes(randomBytes);

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[randomBytes[i] % chars.Length];
        }

        return new string(result);
    }

    private string CreateS256Challenge(string codeVerifier)
    {
        using SHA256 sha256 = SHA256.Create();
        byte[] challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        return Base64UrlEncode(challengeBytes);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }
}


