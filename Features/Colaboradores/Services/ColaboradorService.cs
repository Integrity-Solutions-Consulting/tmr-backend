using FluentValidation;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Colaboradores.DTOs.Request;
using tmr_backend.Features.Colaboradores.DTOs.Response;
using tmr_backend.Features.Colaboradores.Mappings;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Colaboradores.Services;

// Contiene toda la lógica de negocio del módulo.
// Inyección de DbContext + validadores + generador de código.
public sealed class ColaboradorService(
    ApplicationDbContext db,
    ICodigoEmpleadoGenerator codigoGenerator,
    IValidator<CrearColaboradorRequest> crearValidator,
    IValidator<ActualizarColaboradorRequest> actualizarValidator) : IColaboradorService
{
    // Usuario e IP que se guardan en auditoría. Mientras no exista el JWT,
    // usamos valores fijos. Cuando el token esté listo, saldrán de ahí.
    private const string UsuarioSistema = "SYSTEM";
    private const string IpSistema = "127.0.0.1";

    // =========================================================================
    // LISTAR — alimenta la tabla del frontend
    // =========================================================================
    public async Task<List<ColaboradorListaResponse>> ListarAsync(
        string? busqueda, bool? activo, int? asignacion, CancellationToken ct)
    {
        // .Include trae los datos de las tablas relacionadas (persona, cargo, empresa).
        var query = db.TblAdministracionEmpleados
            .Include(e => e.IdpersonaNavigation)          // datos personales
            .Include(e => e.IdcargoNavigation)            // cargo
            .Include(e => e.IdempresacatalogoNavigation)  // asociación (RPS/ISC)
            .AsQueryable();

        // ── Filtro por estado (Activo / Inactivo) ──
        if (activo.HasValue)
            query = query.Where(e => e.Activo == activo.Value);

        // ── Filtro por búsqueda (nombres, apellidos, identificación, código) ──
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var b = busqueda.Trim().ToLower();
            query = query.Where(e =>
                e.IdpersonaNavigation.Nombres.ToLower().Contains(b) ||
                e.IdpersonaNavigation.Apellidos.ToLower().Contains(b) ||
                e.IdpersonaNavigation.Numeroidentificacion.ToLower().Contains(b) ||
                e.Codigoempleado.ToLower().Contains(b));
        }

        // Traemos los datos a memoria para poder contar proyectos por empleado.
        var empleados = await query.ToListAsync(ct);

        // Lista de Ids de los empleados encontrados (para el conteo de proyectos).
        var idsEmpleados = empleados.Select(e => e.Id).ToList();

        // Contamos los proyectos activos por empleado.
        // Traemos las asignaciones a memoria y agrupamos ahí para evitar warnings de null.
        var asignaciones = await db.TblTimeReportAsignacionProyectos
            .Where(ep => ep.Idempleado != null
                      && idsEmpleados.Contains(ep.Idempleado.Value)
                      && ep.Activo)
            .Select(ep => ep.Idempleado ?? 0)
            .ToListAsync(ct);

        // Agrupamos en memoria: por cada Id de empleado, contamos cuántos proyectos tiene.
        var conteoProyectos = asignaciones
            .GroupBy(idEmpleado => idEmpleado)
            .Select(g => new { IdEmpleado = g.Key, Total = g.Count() })
            .ToList();

        // ── Filtro por asignación (0 = sin proyectos, 1 = con proyectos) ──
        // Se aplica después de tener el conteo.
        var resultado = empleados.Select(e =>
        {
            var numProyectos = conteoProyectos
                .FirstOrDefault(c => c.IdEmpleado == e.Id)?.Total ?? 0;
            return e.ToListaResponse(numProyectos);
        }).ToList();

        if (asignacion.HasValue)
        {
            if (asignacion.Value == 0)
                resultado = resultado.Where(r => r.NumProyectos == 0).ToList();
            else if (asignacion.Value == 1)
                resultado = resultado.Where(r => r.NumProyectos >= 1).ToList();
        }

        return resultado;
    }

    // =========================================================================
    // OBTENER POR ID — alimenta el modal de detalle
    // =========================================================================
    public async Task<ColaboradorDetalleResponse?> ObtenerPorIdAsync(int id, CancellationToken ct)
    {
        var empleado = await db.TblAdministracionEmpleados
            .Include(e => e.IdpersonaNavigation)
                .ThenInclude(p => p.IdgeneroNavigation)
            .Include(e => e.IdcargoNavigation)
                .ThenInclude(c => c!.IddepartamentoNavigation)  // departamento vía cargo
            .Include(e => e.IdempresacatalogoNavigation)       // asociación
            .Include(e => e.IdmodotrabajoNavigation)
            .Include(e => e.IdcategoriaempleadoNavigation)     // categoría
            .Include(e => e.IdtipocontratoNavigation)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (empleado is null) return null;

        // Traemos los proyectos asignados activos del colaborador.
        var proyectos = await db.TblTimeReportAsignacionProyectos
            .Include(ep => ep.IdproyectoNavigation)
                .ThenInclude(p => p.IdclienteNavigation)
            .Include(ep => ep.IdproyectoNavigation)
                .ThenInclude(p => p.IdestadoproyectoNavigation)
            .Where(ep => ep.Idempleado == id && ep.Activo)
            .Select(ep => new ProyectoAsignadoResponse(
                ep.IdproyectoNavigation != null ? ep.IdproyectoNavigation.Id : 0,
                ep.IdproyectoNavigation != null ? ep.IdproyectoNavigation.Nombre : "",
                ep.IdproyectoNavigation != null && ep.IdproyectoNavigation.IdclienteNavigation != null
                    ? (ep.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial
                       ?? ep.IdproyectoNavigation.IdclienteNavigation.Razonsocial ?? "")
                    : "",
                ep.IdproyectoNavigation != null && ep.IdproyectoNavigation.IdestadoproyectoNavigation != null
                    ? ep.IdproyectoNavigation.IdestadoproyectoNavigation.Valor
                    : ""
            ))
            .ToListAsync(ct);

        // Armamos el DTO de detalle con el mapper.
        return empleado.ToDetalleResponse(proyectos);
    }


    // =========================================================================
    // CREAR — solo crea el Empleado, usando una Persona existente del ComboBox
    // =========================================================================
    public async Task<int> CrearAsync(CrearColaboradorRequest request, CancellationToken ct)
    {
        // Validar entrada con FluentValidation.
        var validation = await crearValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Verificar que la persona seleccionada exista.
        var persona = await db.TblAdministracionPersonas
            .FirstOrDefaultAsync(p => p.Id == request.IdPersona, ct);
        if (persona is null)
            throw new InvalidOperationException("La persona seleccionada no existe.");

        // Verificar que esa persona NO sea ya un colaborador (evitar duplicados).
        var yaEsColaborador = await db.TblAdministracionEmpleados
            .AnyAsync(e => e.Idpersona == request.IdPersona, ct);
        if (yaEsColaborador)
            throw new InvalidOperationException("Esta persona ya es un colaborador.");

        // Obtener el prefijo de la asociación para generar el código.
        var asociacion = await db.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(c => c.Id == request.IdEmpresaCatalogo, ct);
        if (asociacion is null)
            throw new InvalidOperationException("La asociación seleccionada no existe.");

        // Generar el código de empleado 
        var codigoEmpleado = await codigoGenerator.GenerarAsync(asociacion.Codigovalor, ct);

        // Crear el empleado 
        var empleado = new TblAdministracionEmpleado
        {
            Idpersona = request.IdPersona,
            Codigoempleado = codigoEmpleado,
            Idcargo = request.IdCargo,
            Idmodotrabajo = request.IdModoTrabajo,
            Idcategoriaempleado = request.IdCategoriaEmpleado,
            Idempresacatalogo = request.IdEmpresaCatalogo,
            Idtipocontrato = request.IdTipoContrato,
            Fechaingreso = request.FechaIngreso,
            Aniosexperiencia = request.AniosExperiencia,
            Activo = true,
            Usuariocreacion = UsuarioSistema,
            Ipcreacion = IpSistema
        };

        await db.TblAdministracionEmpleados.AddAsync(empleado, ct);
        await db.SaveChangesAsync(ct);
        // El trigger de auditoría se dispara solo al insertar el empleado.

        return empleado.Id;
    }


    // =========================================================================
    // ACTUALIZAR — solo modifica los datos laborales del empleado.
    // Los datos personales NO se tocan (se gestionan en el módulo de Personas).
    // =========================================================================
    public async Task ActualizarAsync(int id, ActualizarColaboradorRequest request, CancellationToken ct)
    {
        // Validar entrada.
        var validation = await actualizarValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Buscar el empleado.
        var empleado = await db.TblAdministracionEmpleados
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (empleado is null)
            throw new InvalidOperationException("El colaborador no existe.");

        // Actualizar solo los datos laborales.
        empleado.Idcargo = request.IdCargo;
        empleado.Idmodotrabajo = request.IdModoTrabajo;
        empleado.Idcategoriaempleado = request.IdCategoriaEmpleado;
        empleado.Idtipocontrato = request.IdTipoContrato;
        empleado.Fechaingreso = request.FechaIngreso;
        empleado.Aniosexperiencia = request.AniosExperiencia;
        empleado.Activo = request.Activo;
        empleado.Usuariomodificacion = UsuarioSistema;
        empleado.Fechamodificacion = DateTime.UtcNow;
        empleado.Ipmodificacion = IpSistema;

        await db.SaveChangesAsync(ct);
        // El trigger de auditoría de UPDATE se dispara solo.
    }


    // =========================================================================
    // ELIMINAR — eliminación lógica (Activo = false)
    // =========================================================================
    public async Task EliminarAsync(int id, CancellationToken ct)
    {
        var empleado = await db.TblAdministracionEmpleados
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (empleado is null)
            throw new InvalidOperationException("El colaborador no existe.");

        // No se borra físicamente: solo se marca como inactivo.
        empleado.Activo = false;
        empleado.Usuariomodificacion = UsuarioSistema;
        empleado.Fechamodificacion = DateTime.UtcNow;
        empleado.Ipmodificacion = IpSistema;

        await db.SaveChangesAsync(ct);
    }
}