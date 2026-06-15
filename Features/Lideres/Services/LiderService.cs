using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Lideres.DTOs.Request;
using tmr_backend.Features.Lideres.DTOs.Response;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using FluentValidation;

namespace tmr_backend.Features.Lideres.Services;

public class LiderService : ILiderService
{
    private readonly ApplicationDbContext _db;
    private readonly IValidator<CrearLiderRequest> _crearValidator;

    public LiderService(ApplicationDbContext db, IValidator<CrearLiderRequest> crearValidator)
    {
        _db = db;
        _crearValidator = crearValidator;
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
            l.IdpersonaNavigation.Numeroidentificacion,
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
            lider.IdpersonaNavigation.Numeroidentificacion,
            lider.Activo,
            lider.Fechacreacion);
    }

    public async Task<LiderResponse> CrearAsync(CrearLiderRequest request, CancellationToken ct)
    {
        var validation = await _crearValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        using var transaction = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var tipoLider = await _db.TblAdministracionCatalogoDetalles
                .Include(d => d.IdcatalogoNavigation)
                .FirstOrDefaultAsync(d => d.Id == request.Idtipo, ct);

            if (tipoLider is null || tipoLider.IdcatalogoNavigation?.Codigo != "TLI")
                throw new ArgumentException("El tipo de líder especificado no es válido.");

            TblAdministracionPersona persona;

            if (tipoLider.Codigovalor == "INT")
            {
                if (!request.Idpersona.HasValue || request.Idpersona.Value <= 0)
                    throw new ArgumentException("La persona es requerida para líderes internos.");

                persona = await _db.TblAdministracionPersonas
                    .FirstOrDefaultAsync(p => p.Id == request.Idpersona.Value, ct);

                if (persona is null)
                    throw new ArgumentException("La persona especificada no existe.");

                persona.Nombres = request.Nombres;
                persona.Apellidos = request.Apellidos;
                persona.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
                persona.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim();
            }
            else if (tipoLider.Codigovalor == "EXT")
            {
                var numIdentificacion = !string.IsNullOrWhiteSpace(request.NumeroIdentificacion)
                    ? request.NumeroIdentificacion.Trim()
                    : "EXT-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

                var existeIdentificacion = await _db.TblAdministracionPersonas
                    .AnyAsync(p => p.Numeroidentificacion == numIdentificacion, ct);

                if (existeIdentificacion)
                {
                    if (!string.IsNullOrWhiteSpace(request.NumeroIdentificacion))
                        throw new ArgumentException("Ya existe una persona con ese número de identificación.");
                    
                    numIdentificacion = "EXT-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                }

                persona = new TblAdministracionPersona
                {
                    Numeroidentificacion = numIdentificacion,
                    Tipopersona = "NATURAL",
                    Nombres = request.Nombres.Trim(),
                    Apellidos = request.Apellidos.Trim(),
                    Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                    Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
                    Activo = true,
                    Usuariocreacion = request.Usuariocreacion,
                    Fechacreacion = DateTime.UtcNow,
                    Ipcreacion = request.Ipcreacion
                };

                _db.TblAdministracionPersonas.Add(persona);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                throw new ArgumentException("El tipo de líder no está configurado.");
            }

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
                tipoLider.Descripcion,
                persona.Numeroidentificacion,
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
            .Include(l => l.IdtipoNavigation)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lider is null) return null;

        var tipoLider = await _db.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(d => d.Id == request.Idtipo, ct);

        if (tipoLider is not null && tipoLider.Codigovalor == "EXT" && !string.IsNullOrWhiteSpace(request.NumeroIdentificacion))
        {
            var cleanId = request.NumeroIdentificacion.Trim();
            if (lider.IdpersonaNavigation.Numeroidentificacion != cleanId)
            {
                var existeIdentificacion = await _db.TblAdministracionPersonas
                    .AnyAsync(p => p.Id != lider.Idpersona && p.Numeroidentificacion == cleanId, ct);
                if (existeIdentificacion)
                    throw new ArgumentException("Ya existe otra persona con ese número de identificación.");

                lider.IdpersonaNavigation.Numeroidentificacion = cleanId;
            }
        }

        lider.IdpersonaNavigation.Nombres = request.Nombres;
        lider.IdpersonaNavigation.Apellidos = request.Apellidos;
        lider.IdpersonaNavigation.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email;
        lider.IdpersonaNavigation.Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono;
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
            tipoLider?.Descripcion ?? lider.IdtipoNavigation?.Descripcion,
            lider.IdpersonaNavigation.Numeroidentificacion,
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
        // Asumiendo que Idtipo = 1 es Interno, Idtipo = 2 es Externo
        var internos = await _db.TblAdministracionLiders
            .CountAsync(l => l.Activo && l.Idtipo == 1, ct);

        var externos = await _db.TblAdministracionLiders
            .CountAsync(l => l.Activo && l.Idtipo == 2, ct);

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

    public async Task<IEnumerable<TipoLiderResponse>> ObtenerTiposAsync(CancellationToken ct)
    {
        return await _db.TblAdministracionCatalogoDetalles
            .Include(d => d.IdcatalogoNavigation)
            .Where(d => d.Activo &&
                        d.IdcatalogoNavigation.Codigo == "TLI" &&
                        d.IdcatalogoNavigation.Tipocatalogo == "ADM")
            .OrderBy(d => d.Orden)
            .Select(d => new TipoLiderResponse(
                d.Id,
                d.Codigovalor,
                d.Valor,
                d.Descripcion))
            .ToListAsync(ct);
    }

    public async Task<List<PersonaResponse>> ObtenerPersonasNoLideresAsync(CancellationToken ct)
    {
        var idsLideres = await _db.TblAdministracionLiders
            .Select(l => l.Idpersona)
            .ToListAsync(ct);

        return await _db.TblAdministracionPersonas
            .Where(p => p.Activo && !idsLideres.Contains(p.Id))
            .Select(p => new PersonaResponse(
                p.Id,
                p.Nombres,
                p.Apellidos,
                p.Email ?? string.Empty
            ))
            .ToListAsync(ct);
    }
}