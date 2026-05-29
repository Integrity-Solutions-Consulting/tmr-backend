using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using tmr_backend.Features.Auth.Refresh.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Security;
using tmr_backend.Infrastructure.Database.Entities;

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
    private readonly IConfiguration _configuration;

    public RefreshHandler(
        ApplicationDbContext db,
        ITokenService tokenService,
        IHttpContextAccessor httpContextAccessor,
        IConfiguration configuration)
    {
        _db = db;
        _tokenService = tokenService;
        _httpContextAccessor = httpContextAccessor;
        _configuration = configuration;
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

        // 9.5 Extraer el JTI del nuevo access token
        var handler = new JwtSecurityTokenHandler();
        var tokenObj = handler.ReadJwtToken(newAccessToken);
        var newJti = tokenObj.Claims.FirstOrDefault(c => c.Type == "jti")?.Value ?? Guid.NewGuid().ToString();

        // 9.6 Hacer blacklist del JTI anterior (token rotation security)
        if (!string.IsNullOrEmpty(sesion.UltimoJti))
        {
            var oldBlacklistEntry = new TblAutenticacionTokenBlacklist
            {
                Token = sesion.UltimoJti,
                Fechaexpiracion = DateTime.UtcNow.AddMinutes(15),
                Activo = true,
                Usuariocreacion = usuario.Email,
                Fechacreacion = DateTime.UtcNow
            };
            await _db.TblAutenticacionTokenBlacklists.AddAsync(oldBlacklistEntry);
        }

        // 10. Generar nuevo refresh token (token rotation)
        var (newRefreshToken, newExpiresAt) = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = _tokenService.HashToken(newRefreshToken);

        // 11. Actualizar la sesión con el nuevo hash, JTI y fecha de actividad
        sesion.Tokensesion = newRefreshTokenHash;
        sesion.UltimoJti = newJti;
        sesion.Horasalida = newExpiresAt;
        sesion.Usuariomodificacion = usuario.Email;
        sesion.Fechamodificacion = DateTime.UtcNow;

        _db.TblAutenticacionSesions.Update(sesion);
        await _db.SaveChangesAsync(ct);

        // 12. Calcular ExpiresIn en segundos (leer desde configuración)
        int accessTokenMinutes = _configuration.GetValue<int>("Jwt:AccessTokenMinutes", 15);
        int expiresIn = accessTokenMinutes * 60; // Convertir minutos a segundos

        // 13. Construir response
        return new RefreshTokenResponse(
            newAccessToken,
            newRefreshToken,
            expiresIn
        );
    }
}
