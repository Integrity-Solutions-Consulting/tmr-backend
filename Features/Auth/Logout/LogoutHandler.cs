using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using tmr_backend.Features.Auth.Logout.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Auth.Logout;

/// <summary>
/// Handler para el caso de uso de Logout.
/// Invalida la sesión, agrega el token a blacklist para su revocación inmediata.
/// </summary>
public class LogoutHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public LogoutHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task Handle(LogoutRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId;
        if (userId <= 0)
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        var jti = _currentUserService.Jti;
        if (string.IsNullOrEmpty(jti))
            throw new UnauthorizedAccessException("JTI no encontrado en el token.");

        // 1. Agregar el JTI a la tabla de blacklist (para invalidar inmediatamente)
        // Usando el campo "Token" para almacenar el JTI
        var blacklistEntry = new TblAutenticacionTokenBlacklist
        {
            Token = jti,
            Fechaexpiracion = DateTime.UtcNow.AddMinutes(15), // TTL del access token
            Activo = true,
            Usuariocreacion = _currentUserService.Email ?? "system",
            Fechacreacion = DateTime.UtcNow
        };

        await _db.TblAutenticacionTokenBlacklists.AddAsync(blacklistEntry, ct);

        // 2. Buscar y cerrar todas las sesiones activas del usuario
        var sesionesActivas = await _db.TblAutenticacionSesions
            .Where(s => s.Idusuario == userId && s.Estaactiva == true)
            .ToListAsync(ct);

        foreach (var sesion in sesionesActivas)
        {
            sesion.Estaactiva = false;
            sesion.Horasalida = DateTime.UtcNow;
            sesion.Usuariomodificacion = _currentUserService.Email ?? "system";
            sesion.Fechamodificacion = DateTime.UtcNow;
            _db.TblAutenticacionSesions.Update(sesion);
        }

        // 3. Guardar cambios
        await _db.SaveChangesAsync(ct);
    }
}
