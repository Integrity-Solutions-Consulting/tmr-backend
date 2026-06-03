using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Security;

public sealed class TokenService(IOptions<JwtSettings> jwtSettings) : ITokenService
{
    private readonly JwtSettings _jwt = jwtSettings.Value;

    // ---------------------------------------------------------------
    // ACCESS TOKEN — JWT firmado con HS256
    // ---------------------------------------------------------------
    public (string Token, string Jti) GenerateAccessToken(TblAutenticacionUsuario user, IEnumerable<string> roles)
    {
        var secret = _jwt.SecretKey;
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jti = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti,   jti),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer:             _jwt.Issuer,
            audience:           _jwt.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), jti);
    }

    // ---------------------------------------------------------------
    // REFRESH TOKEN — bytes aleatorios criptográficamente seguros
    // ---------------------------------------------------------------
    public (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshTokenRaw()
    {
        var raw       = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash      = HashToken(raw);
        var expiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays);
        return (raw, hash, expiresAt);
    }

    // ---------------------------------------------------------------
    // HASH TOKEN — SHA-256 del token
    // ---------------------------------------------------------------
    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLower();
    }
}
