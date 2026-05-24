using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Security;

public sealed class TokenService : ITokenService
{
    private readonly JwtSettings _jwt;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _db;

    public TokenService(
        IOptions<JwtSettings> jwtSettings,
        ApplicationDbContext db,
        IConfiguration configuration)
    {
        _jwt = jwtSettings.Value;
        _db = db;
        _configuration = configuration;
    }

    // ---------------------------------------------------------------
    // ACCESS TOKEN — JWT firmado con HS256
    // ---------------------------------------------------------------
    public string GenerateAccessToken(Guid userId, string email)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public string GenerateAccessToken(TblAutenticacionUsuario user)
    {
        var secret = _configuration["Jwt:SecretKey"]!;
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];
        var expirationMinutes = _jwt.AccessTokenMinutes;
        if (int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var parsedMinutes))
        {
            expirationMinutes = parsedMinutes;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ---------------------------------------------------------------
    // REFRESH TOKEN — bytes aleatorios criptográficamente seguros
    // ---------------------------------------------------------------
    public (string Token, DateTime ExpiresAt) GenerateRefreshToken()
    {
        var expirationDays = _jwt.RefreshTokenDays;
        if (int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var parsedDays))
        {
            expirationDays = parsedDays;
        }
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAt = DateTime.UtcNow.AddDays(expirationDays);
        return (token, expiresAt);
    }

    // ---------------------------------------------------------------
    // HASH — SHA-256 del raw token (lo que se guarda en BD)
    // ---------------------------------------------------------------
    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLower();
    }
}