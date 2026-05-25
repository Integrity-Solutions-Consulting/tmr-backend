using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Lideres.DTOs.Request;
using tmr_backend.Features.Lideres.DTOs.Response;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Lideres.Services;

public class LiderService : ILiderService
{
    private readonly ApplicationDbContext _db;

    public LiderService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<LiderResponse>> ObtenerTodosAsync(bool? activo, CancellationToken ct)
    {
        var query = _db.TblAdministracionLiders
            .Include(l => l.IdpersonaNavigation)
            .Include(l => l.IdtipoNavigation)
            .AsQueryable();

        if (activo.HasValue)
            query = query.Where(l => l.Activo == activo.Value);

        return await query.Select(l => new LiderResponse(
            l.Id,
            l.IdpersonaNavigation.Nombres,
            l.IdpersonaNavigation.Apellidos,
            l.IdpersonaNavigation.Email,
            l.IdpersonaNavigation.Telefono,
            l.IdpersonaNavigation.Tipopersona,
            l.Idtipo,
            l.IdtipoNavigation != null ? l.IdtipoNavigation.Descripcion : null,
            l.Activo,
            l.Fechacreacion
        )).ToListAsync(ct);
    }

    public async Task<LiderResponse?> ObtenerPorIdAsync(int id, CancellationToken ct)
    {
        var lider = await _db.TblAdministracionLiders
            .Include(l => l.IdpersonaNavigation)
            .Include(l => l.IdtipoNavigation)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lider is null) return null;

        return new LiderResponse(
            lider.Id,
            lider.IdpersonaNavigation.Nombres,
            lider.IdpersonaNavigation.Apellidos,
            lider.IdpersonaNavigation.Email,
            lider.IdpersonaNavigation.Telefono,
            lider.IdpersonaNavigation.Tipopersona,
            lider.Idtipo,
            lider.IdtipoNavigation?.Descripcion,
            lider.Activo,
            lider.Fechacreacion);
    }

    public async Task<LiderResponse> CrearAsync(CrearLiderRequest request, CancellationToken ct)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var persona = new TblAdministracionPersona
            {
                Nombres = request.Nombres,
                Apellidos = request.Apellidos,
                Email = request.Email,
                Telefono = request.Telefono,
                Tipopersona = request.Tipopersona,
                Numeroidentificacion = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 20),
                Activo = true,
                Usuariocreacion = request.Usuariocreacion,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = request.Ipcreacion
            };

            _db.TblAdministracionPersonas.Add(persona);
            await _db.SaveChangesAsync(ct);

            var lider = new TblAdministracionLider
            {
                Idpersona = persona.Id,
                Idtipo = request.Idtipo,
                Activo = true,
                Usuariocreacion = request.Usuariocreacion,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = request.Ipcreacion
            };

            _db.TblAdministracionLiders.Add(lider);
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            return new LiderResponse(
                lider.Id,
                persona.Nombres,
                persona.Apellidos,
                persona.Email,
                persona.Telefono,
                persona.Tipopersona,
                lider.Idtipo,
                null,
                lider.Activo,
                lider.Fechacreacion);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<LiderResponse?> ActualizarAsync(int id, ActualizarLiderRequest request, CancellationToken ct)
    {
        var lider = await _db.TblAdministracionLiders
            .Include(l => l.IdpersonaNavigation)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lider is null) return null;

        lider.IdpersonaNavigation.Nombres = request.Nombres;
        lider.IdpersonaNavigation.Apellidos = request.Apellidos;
        lider.IdpersonaNavigation.Email = request.Email;
        lider.IdpersonaNavigation.Telefono = request.Telefono;
        lider.Idtipo = request.Idtipo;
        lider.Activo = request.Activo;
        lider.Usuariomodificacion = request.Usuariomodificacion;
        lider.Fechamodificacion = DateTime.UtcNow;
        lider.Ipmodificacion = request.Ipmodificacion;

        await _db.SaveChangesAsync(ct);

        return new LiderResponse(
            lider.Id,
            lider.IdpersonaNavigation.Nombres,
            lider.IdpersonaNavigation.Apellidos,
            lider.IdpersonaNavigation.Email,
            lider.IdpersonaNavigation.Telefono,
            lider.IdpersonaNavigation.Tipopersona,
            lider.Idtipo,
            null,
            lider.Activo,
            lider.Fechacreacion);
    }

    public async Task<bool> DesactivarAsync(int id, CancellationToken ct)
    {
        var lider = await _db.TblAdministracionLiders.FindAsync([id], ct);
        if (lider is null) return false;

        lider.Activo = false;
        lider.Fechamodificacion = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ContadoresLiderResponse> ObtenerContadoresAsync(CancellationToken ct)
    {
        var internos = await _db.TblAdministracionLiders
            .CountAsync(l => l.Activo && l.Idtipo != null, ct);

        var externos = await _db.TblAdministracionLiders
            .CountAsync(l => l.Activo, ct);

        var inactivos = await _db.TblAdministracionLiders
            .CountAsync(l => !l.Activo, ct);

        return new ContadoresLiderResponse(internos, externos, inactivos);
    }

    public async Task<IEnumerable<PersonaDisponibleResponse>> ObtenerPersonasDisponiblesAsync(CancellationToken ct)
    {
        var idsLideres = await _db.TblAdministracionLiders
            .Select(l => l.Idpersona)
            .ToListAsync(ct);

        return await _db.TblAdministracionPersonas
            .Where(p => p.Activo && !idsLideres.Contains(p.Id))
            .Select(p => new PersonaDisponibleResponse(
                p.Id,
                p.Nombres,
                p.Apellidos,
                p.Email,
                p.Telefono))
            .ToListAsync(ct);
    }
}