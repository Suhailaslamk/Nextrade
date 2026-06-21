namespace TradingService.API.Authentication;

/// <summary>
/// JWT Bearer validation settings. The Trading Service is a token
/// *consumer* — it validates tokens minted by the Auth Service using
/// the Auth Service's RSA public key, distributed out-of-band (mounted
/// secret / config) so no network call to Auth Service is needed on
/// the hot path of every request.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://auth.nextrade.local";
    public string Audience { get; set; } = "nextrade.trading";

    /// <summary>Path to the Auth Service's RSA public key in PEM format, used to validate token signatures.</summary>
    public string PublicKeyPath { get; set; } = "keys/auth-public-key.pem";
}