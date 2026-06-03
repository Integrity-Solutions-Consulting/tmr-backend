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

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    public string GenerateAccessToken(TblAutenticacionUsuario user)

    /// <summary>
    /// Genera Access Token con claims completos (roles, nombre, employee_id, etc.)
    /// Usado en login y refresh.
    /// </summary>
    public string GenerateAccessTokenWithClaims(
        TblAutenticacionUsuario user,
        IEnumerable<string> roles,
        string? fullName = null,
        int? employeeId = null)
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

        var jti = Guid.NewGuid().ToString();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti,   jti),
            new("sub", user.Id.ToString()),
            new("email", user.Email),
            new("jti", jti),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        // Agregar nombre si está disponible
        if (!string.IsNullOrEmpty(fullName))
            claims.Add(new("name", fullName));

        // Agregar employee_id si está disponible
        if (employeeId.HasValue)
            claims.Add(new("employee_id", employeeId.Value.ToString()));

        // Agregar roles como claims múltiples
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


    // ---------------------------------------------------------------
    // REFRESH TOKEN — bytes aleatorios criptográficamente seguros
    // ---------------------------------------------------------------
    public (string Token, DateTime ExpiresAt) GenerateRefreshToken()
    public (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshTokenRaw()
    {
        var expirationDays = _jwt.RefreshTokenDays;
        if (int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var parsedDays))
        {
            expirationDays = parsedDays;
        }
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var expiresAt = DateTime.UtcNow.AddDays(expirationDays);
        return (token, expiresAt);
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
