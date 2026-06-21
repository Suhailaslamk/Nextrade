using AuthService.Application.Common.Interfaces;
using TokenValidationResult = AuthService.Application.Common.Interfaces.TokenValidationResult;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace AuthService.Infrastructure.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "nextrade-auth";
    public string Audience { get; set; } = "nextrade-api";
    public int AccessTokenExpiryMinutes { get; set; } = 15;

    /// <summary>
    /// Path to PEM-encoded RSA private key.
    /// </summary>
    public string PrivateKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to PEM-encoded RSA public key.
    /// </summary>
    public string PublicKeyPath { get; set; } = string.Empty;
}

public sealed class JwtService : IJwtService
{
    private readonly JwtOptions _options;
    private readonly RSA _privateKey;
    private readonly RSA _publicKey;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _tokenHandler = new JwtSecurityTokenHandler();

        if (string.IsNullOrWhiteSpace(_options.PrivateKeyPath))
            throw new InvalidOperationException("Jwt:PrivateKeyPath is not configured.");
        if (!System.IO.File.Exists(_options.PrivateKeyPath))
            throw new System.IO.FileNotFoundException($"Private key file not found at '{_options.PrivateKeyPath}'");

        if (string.IsNullOrWhiteSpace(_options.PublicKeyPath))
            throw new InvalidOperationException("Jwt:PublicKeyPath is not configured.");
        if (!System.IO.File.Exists(_options.PublicKeyPath))
            throw new System.IO.FileNotFoundException($"Public key file not found at '{_options.PublicKeyPath}'");

        _privateKey = RSA.Create();
        _privateKey.ImportFromPem(System.IO.File.ReadAllText(_options.PrivateKeyPath));

        _publicKey = RSA.Create();
        _publicKey.ImportFromPem(System.IO.File.ReadAllText(_options.PublicKeyPath));
    }

    public string GenerateAccessToken(User user)
    {
        var signingCredentials = new SigningCredentials(
            new RsaSecurityKey(_privateKey),
            SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        var jti = Guid.NewGuid().ToString();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Role, user.Role.ToString().ToUpperInvariant()),
            new Claim("full_name", user.FullName),
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = now.AddMinutes(_options.AccessTokenExpiryMinutes),
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = signingCredentials,
            NotBefore = now,
            IssuedAt = now,
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    public Task<string> GenerateRefreshTokenAsync()
    {
        // 64-byte cryptographically random token, base64url encoded
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
        return Task.FromResult(token);
    }

    public AuthService.Application.Common.Interfaces.TokenValidationResult? ValidateAccessToken(string token)
    {
        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateLifetime = true,
                IssuerSigningKey = new RsaSecurityKey(_publicKey),
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out _);

            var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var emailClaim = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
            var roleClaim = principal.FindFirst(ClaimTypes.Role)?.Value;
            var jtiClaim = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

            if (userIdClaim is null || emailClaim is null || roleClaim is null || jtiClaim is null)
                return null;

            if (!Guid.TryParse(userIdClaim, out var userId))
                return null;

            if (!Enum.TryParse<UserRole>(roleClaim, ignoreCase: true, out var role))
                return null;

            return new TokenValidationResult(
                IsValid: true,
                UserId: userId,
                Email: emailClaim,
                Role: role,
                Jti: jtiClaim);
        }
        catch
        {
            return null;
        }
    }
}