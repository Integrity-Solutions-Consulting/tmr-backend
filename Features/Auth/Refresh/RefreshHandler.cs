using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using tmr_backend.Features.Auth.Refresh.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Security;

namespace tmr_backend.Features.Auth.Refresh;

/// <summary>
/// Handler para el caso de uso de Refresh de tokens.
/// Busca sesión por hash, valida expiración, genera nuevos tokens, actualiza BD.
/// Implementa token rotation: nuevo refresh token y nuevo access token.
/// </summary>
public class RefreshHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RefreshHandler(
        ApplicationDbContext db,
        ITokenService tokenService,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _tokenService = tokenService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<RefreshTokenResponse> Handle(RefreshTokenRequest request, CancellationToken ct)
    {
        // 1. Validar que el refresh token no sea nulo
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            throw new UnauthorizedAccessException("Refresh token inválido.");

        // 2. Hashear el refresh token recibido
        var refreshTokenHash = _tokenService.HashToken(request.RefreshToken);

        // 3. Buscar la sesión activa por hash
        var sesion = await _db.TblAutenticacionSesions
            .Where(s => s.Tokensesion == refreshTokenHash && s.Estaactiva == true)
            .FirstOrDefaultAsync(ct);

        if (sesion == null)
            throw new UnauthorizedAccessException("Refresh token no encontrado o inactivo.");

        // 4. Validar que el refresh token no haya expirado
        if (sesion.Horasalida.HasValue && sesion.Horasalida <= DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expirado.");

        // 5. Cargar el usuario con sus roles
        var usuario = await _db.TblAutenticacionUsuarios
            .Include(u => u.IdpersonaNavigation)
            .Include(u => u.TblAutenticacionUsuarioRols)
                .ThenInclude(ur => ur.IdrolNavigation)
            .FirstOrDefaultAsync(u => u.Id == sesion.Idusuario, ct);

        if (usuario == null || usuario.Estaactivo != true)
            throw new UnauthorizedAccessException("Usuario no encontrado o inactivo.");

        // 6. Obtener roles del usuario
        var roles = usuario.TblAutenticacionUsuarioRols
            .Where(ur => ur.Activo)
            .Select(ur => ur.IdrolNavigation?.Valor ?? "UNKNOWN")
            .ToList();

        // 7. Obtener datos de la persona (nombre)
        var persona = usuario.IdpersonaNavigation;
        var fullName = persona != null
            ? $"{persona.Nombres} {persona.Apellidos}".Trim()
            : "Usuario";

        // 8. Obtener employee_id desde TblAdministracionEmpleado
        int? employeeId = null;
        if (persona != null)
        {
            var empleado = await _db.TblAdministracionEmpleados
                .FirstOrDefaultAsync(e => e.Idpersona == persona.Id && e.Activo, ct);
            if (empleado != null)
                employeeId = empleado.Id;
        }

        // 9. Generar nuevo access token (con claims completos)
        var newAccessToken = _tokenService.GenerateAccessTokenWithClaims(
            usuario,
            roles,
            fullName,
            employeeId
        );

        // 10. Generar nuevo refresh token (token rotation)
        var (newRefreshToken, newExpiresAt) = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashToken(newRefreshToken);

        // 11. Actualizar la sesión con el nuevo hash y fecha de actividad
        sesion.Tokensesion = newRefreshTokenHash;
        sesion.Horasalida = newExpiresAt;
        sesion.Usuariomodificacion = usuario.Email;
        sesion.Fechamodificacion = DateTime.UtcNow;

        _db.TblAutenticacionSesions.Update(sesion);
        await _db.SaveChangesAsync(ct);

        // 12. Calcular ExpiresIn en segundos (15 minutos por defecto, igual que access token)
        int expiresIn = 15 * 60; // 900 segundos

        // 13. Construir response
        return new RefreshTokenResponse(
            newAccessToken,
            newRefreshToken,
            expiresIn
        );
    }
}
