using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Security;

public sealed class TokenService(IOptions<JwtSettings> jwtSettings, IConfiguration configuration) : ITokenService
{
    private readonly JwtSettings _jwt = jwtSettings.Value;

    public (string Token, string Jti) GenerateAccessToken(TblAutenticacionUsuario user, IEnumerable<string> roles)
    {
        var jti    = Guid.NewGuid().ToString();
        var secret = configuration["Jwt:SecretKey"]!;
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti,   jti),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        // sub → ClaimTypes.NameIdentifier por el mapping por defecto del JWT handler
        // roles → ClaimTypes.Role para que [Authorize(Roles="...")] funcione
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer:            _jwt.Issuer,
            audience:          _jwt.Audience,
            claims:            claims,
            expires:           DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes),
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), jti);
    }

    public (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshTokenRaw()
    {
        var raw       = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash      = HashToken(raw);
        var expiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays);
        return (raw, hash, expiresAt);
    }

    public string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes).ToLower();
    }
}
