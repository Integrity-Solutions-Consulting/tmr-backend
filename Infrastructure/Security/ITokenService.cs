using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Security;

public interface ITokenService
{
    string GenerateAccessToken(TblAutenticacionUsuario user);
    string GenerateAccessTokenWithClaims(
        TblAutenticacionUsuario user,
        IEnumerable<string> roles,
        string? fullName = null,
        int? employeeId = null);
    (string Token, DateTime ExpiresAt) GenerateRefreshToken();
    string HashToken(string rawToken);
}