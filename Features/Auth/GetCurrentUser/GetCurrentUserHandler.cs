using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Auth.GetCurrentUser.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Auth.GetCurrentUser;

/// <summary>
/// Handler para obtener datos del usuario autenticado.
/// Consulta usuario por ID (LINQ con Include/ThenInclude).
/// Retorna: id, nombres, apellidos, email, cargo, codigoEmpleado, roles.
/// </summary>
public class GetCurrentUserHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public GetCurrentUserHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db;
        _currentUserService = currentUserService;
    }

    public async Task<CurrentUserResponse> Handle(CancellationToken ct)
    {
        var userId = _currentUserService.UserId;
        if (userId <= 0)
            throw new UnauthorizedAccessException("Usuario no autenticado.");

        // Consulta con includes entre esquemas
        var usuario = await _db.TblAutenticacionUsuarios
            .Include(u => u.IdpersonaNavigation)
            .Include(u => u.TblAutenticacionUsuarioRols)
                .ThenInclude(ur => ur.IdrolNavigation)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Estaactivo == true, ct);

        if (usuario == null)
            throw new UnauthorizedAccessException("Usuario no encontrado o inactivo.");

        var persona = usuario.IdpersonaNavigation;
        if (persona == null)
            throw new InvalidOperationException("Usuario sin datos de persona asociados.");

        // Obtener cargo y detalles del empleado
        var empleado = await _db.TblAdministracionEmpleados
            .Include(e => e.IdcargoNavigation)
            .FirstOrDefaultAsync(e => e.Idpersona == persona.Id && e.Activo, ct);

        // Construir nombre completo
        var nombre = $"{persona.Nombres} {persona.Apellidos}".Trim();

        // Obtener roles del usuario
        var roles = usuario.TblAutenticacionUsuarioRols
            .Where(ur => ur.Activo)
            .Select(ur => ur.IdrolNavigation?.Valor ?? "UNKNOWN")
            .Distinct()
            .ToArray();

        // Retornar respuesta
        return new CurrentUserResponse(
            Id: usuario.Id,
            Nombre: nombre,
            Email: usuario.Email,
            Roles: roles,
            Foto: null, // Foto no disponible en BD actual
            EmployeeId: empleado?.Id,
            FechaCreacion: usuario.Fechacreacion
        );
    }
}
