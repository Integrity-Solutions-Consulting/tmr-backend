using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Security;

public interface ITokenService
{
    string GenerateAccessToken(TblAutenticacionUsuario user);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();
    string HashToken(string rawToken);
}