using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Auth.Login.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Security;

namespace tmr_backend.Features.Auth.Login;

/// <summary>
/// Handler para el caso de uso de Login.
/// Valida credenciales, obtiene roles, genera tokens y guarda sesión.
/// </summary>
public class LoginHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public LoginHandler(
        ApplicationDbContext db,
        ITokenService tokenService,
        IPasswordHasher passwordHasher,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<LoginResponse> Handle(LoginRequest request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.ToLowerInvariant();

        // 1. Buscar usuario por email, incluyendo relaciones necesarias
        var usuario = await _db.TblAutenticacionUsuarios
            .Include(u => u.IdpersonaNavigation)
            .Include(u => u.TblAutenticacionUsuarioRols)
                .ThenInclude(ur => ur.IdrolNavigation)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        // Usuario no encontrado
        if (usuario == null)
            throw new UnauthorizedAccessException("Email o contraseña incorrectos.");

        // 2. Verificar que el usuario esté activo
        if (usuario.Estaactivo != true)
            throw new UnauthorizedAccessException("Usuario inactivo.");

        // 3. Verificar contraseña
        if (!_passwordHasher.Verify(request.Password, usuario.Hashpassword))
            throw new UnauthorizedAccessException("Email o contraseña incorrectos.");

        // 4. Obtener roles del usuario
        var roles = usuario.TblAutenticacionUsuarioRols
            .Where(ur => ur.Activo)
            .Select(ur => ur.IdrolNavigation?.Valor ?? "UNKNOWN")
            .ToList();

        // 5. Obtener datos de la persona (nombre)
        var persona = usuario.IdpersonaNavigation;
        var fullName = persona != null
            ? $"{persona.Nombres} {persona.Apellidos}".Trim()
            : "Usuario";

        // 6. Obtener employee_id desde TblAdministracionEmpleado
        int? employeeId = null;
        if (persona != null)
        {
            var empleado = await _db.TblAdministracionEmpleados
                .FirstOrDefaultAsync(e => e.Idpersona == persona.Id && e.Activo, ct);
            if (empleado != null)
                employeeId = empleado.Id;
        }

        // 7. Generar access token con todos los claims
        var accessToken = _tokenService.GenerateAccessTokenWithClaims(
            usuario,
            roles,
            fullName,
            employeeId
        );

        // 8. Generar refresh token
        var (refreshToken, expiresAt) = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashToken(refreshToken);

        // 9. Obtener IP del cliente
        var ipCreacion = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "UNKNOWN";

        // 10. Guardar sesión en BD (dentro de transacción)
        var sesion = new TblAutenticacionSesion
        {
            Idusuario = usuario.Id,
            Tokensesion = refreshTokenHash,
            Horaingreso = DateTime.UtcNow,
            Horasalida = null,
            Direccionip = ipCreacion,
            Agenteusuario = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString(),
            Estaactiva = true,
            Activo = true,
            Usuariocreacion = usuario.Email,
            Fechacreacion = DateTime.UtcNow,
            Ipcreacion = ipCreacion
        };

        await _db.TblAutenticacionSesions.AddAsync(sesion, ct);

        // 11. Actualizar último login del usuario
        usuario.Ultimologin = DateTime.UtcNow;
        _db.TblAutenticacionUsuarios.Update(usuario);

        // Guardar cambios en BD
        await _db.SaveChangesAsync(ct);

        // 12. Calcular ExpiresIn en segundos (15 minutos por defecto)
        int expiresIn = 15 * 60; // 900 segundos

        // 13. Construir response
        var userDto = new UserDto(
            usuario.Id,
            fullName,
            usuario.Email,
            roles.ToArray(),
            employeeId
        );

        return new LoginResponse(
            accessToken,
            refreshToken,
            expiresIn,
            userDto
        );
    }
}
