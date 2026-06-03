using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Security;

public interface ITokenService
{
    /// <summary>Genera un AT firmado. Devuelve el JWT string y el jti único para blacklist.</summary>
    (string Token, string Jti) GenerateAccessToken(TblAutenticacionUsuario user, IEnumerable<string> roles);

    /// <summary>Genera un RT criptográficamente seguro. Devuelve raw (para el cliente), hash (para DB) y expiración.</summary>
    (string RawToken, string TokenHash, DateTime ExpiresAt) GenerateRefreshTokenRaw();

    /// <summary>SHA-256 del token. Nunca se guarda el raw en DB.</summary>
    string HashToken(string rawToken);
}
