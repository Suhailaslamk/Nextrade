using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace TradingService.API.Authentication;

/// <summary>
/// Loads the Auth Service's RSA public key (PEM format) from disk and
/// wraps it as a <see cref="RsaSecurityKey"/> for JwtBearer signature
/// validation. Falls back to a clear startup failure if the key file
/// is missing, rather than silently accepting unsigned/unvalidated
/// tokens.
/// </summary>
public static class RsaKeyLoader
{
    public static RsaSecurityKey LoadPublicKey(string pemFilePath)
    {
        if (!File.Exists(pemFilePath))
        {
            throw new FileNotFoundException(
                $"JWT public key file not found at '{pemFilePath}'. " +
                "Mount the Auth Service's public key (PEM format) at this path.",
                pemFilePath);
        }

        var pem = File.ReadAllText(pemFilePath);

        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        return new RsaSecurityKey(rsa);
    }
}