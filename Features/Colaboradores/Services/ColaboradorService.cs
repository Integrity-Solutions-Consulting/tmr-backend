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
        // Orden: activos primero, y dentro de cada grupo los más nuevos primero.
        var empleados = await query
            .OrderByDescending(e => e.Activo)
            .ThenByDescending(e => e.Id)
            .ToListAsync(ct);

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
            .Include(e => e.IdpersonaNavigation)
                .ThenInclude(p => p.IdnacionalidadNavigation)
            .Include(e => e.IdcargoNavigation)
                .ThenInclude(c => c!.IddepartamentoNavigation)  // departamento vía cargo
            .Include(e => e.IdempresacatalogoNavigation)       // asociación
            .Include(e => e.IdmodotrabajoNavigation)
            .Include(e => e.IdcategoriaempleadoNavigation)     // categoría
            .Include(e => e.IdtipocontratoNavigation)
            .Include(e => e.TipoSalidaNavigation)
            .Include(e => e.CausaSalidaNavigation)
            .Include(e => e.EmpleadoReemplazoNavigation)
                .ThenInclude(r => r.IdpersonaNavigation)
            .Include(e => e.EmpleadosReemplazados)
                .ThenInclude(er => er.IdpersonaNavigation)
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
    // CREAR — crea Persona + Empleado en una transacción.
    // =========================================================================
    public async Task<int> CrearAsync(CrearColaboradorRequest request, CancellationToken ct)
    {
        // Validar entrada con FluentValidation.
        var validation = await crearValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Verificar que no exista ya una persona con la misma identificación.
        var existePersona = await db.TblAdministracionPersonas
            .AnyAsync(p => p.Numeroidentificacion == request.NumeroIdentificacion.Trim(), ct);

        if (existePersona)
            throw new InvalidOperationException("Ya existe una persona con esa identificación.");

        // Obtener la empresa para generar el código de empleado.
        var empresa = await db.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(c => c.Id == request.IdEmpresaCatalogo, ct);

        if (empresa is null)
            throw new InvalidOperationException("La empresa seleccionada no existe.");

        // Usamos transacción para evitar que quede una Persona creada sin Empleado.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            // Crear la persona con los datos ingresados desde el modal.
            var persona = new TblAdministracionPersona
            {
                Numeroidentificacion = request.NumeroIdentificacion.Trim(),
                Idtipoidentificacion = request.TipoPersona == "NATURAL"
                    ? request.IdTipoIdentificacion
                    : null,
                Idgenero = request.IdGenero,
                Idnacionalidad = request.IdNacionalidad,
                Tipopersona = request.TipoPersona,
                Nombres = request.Nombres.Trim(),
                Apellidos = request.Apellidos.Trim(),
                Fechanacimiento = request.FechaNacimiento,
                Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                Telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : request.Telefono.Trim(),
                Direccion = string.IsNullOrWhiteSpace(request.Direccion) ? null : request.Direccion.Trim(),
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Ipcreacion = IpSistema
            };

            await db.TblAdministracionPersonas.AddAsync(persona, ct);
            await db.SaveChangesAsync(ct);

            // Generar el código de empleado con el prefijo de la empresa.
            var codigoEmpleado = await codigoGenerator.GenerarAsync(empresa.Codigovalor, ct);

            // Crear el empleado usando el Id de la persona recién creada.
            var empleado = new TblAdministracionEmpleado
            {
                Idpersona = persona.Id,
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

            // ================================================================
            // NUEVO: Si se envió un reemplazo, actualizar el inactivo
            // ================================================================
            if (request.IdEmpleadoReemplazo.HasValue)
            {
                var inactivo = await db.TblAdministracionEmpleados
                    .FirstOrDefaultAsync(e => e.Id == request.IdEmpleadoReemplazo.Value && e.Activo == false, ct);

                if (inactivo != null)
                {
                    inactivo.IdEmpleadoReemplazo = empleado.Id;  // El nuevo colaborador reemplaza al inactivo
                    inactivo.Usuariomodificacion = UsuarioSistema;
                    inactivo.Fechamodificacion = DateTime.UtcNow;
                    inactivo.Ipmodificacion = IpSistema;
                    await db.SaveChangesAsync(ct);
                }
            }

            // Si todo salió bien, confirmamos Persona + Empleado.
            await transaction.CommitAsync(ct);

            return empleado.Id;
        }
        catch
        {
            // Si falla algo, revertimos para no dejar datos incompletos.
            await transaction.RollbackAsync(ct);
            throw;
        }
    }


    // =========================================================================
    // ACTUALIZAR — modifica datos personales de Persona + datos laborales de Empleado.
    // =========================================================================
    public async Task ActualizarAsync(int id, ActualizarColaboradorRequest request, CancellationToken ct)
    {
        // Validar entrada.
        var validation = await actualizarValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Buscar el empleado junto con su Persona.
        var empleado = await db.TblAdministracionEmpleados
            .Include(e => e.IdpersonaNavigation)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (empleado is null)
            throw new InvalidOperationException("El colaborador no existe.");

        if (empleado.IdpersonaNavigation is null)
            throw new InvalidOperationException("El colaborador no tiene una persona asociada.");

        var persona = empleado.IdpersonaNavigation;

        // ── Actualizar datos personales ──────────────────────────────
        var tipoPersona = string.IsNullOrWhiteSpace(request.TipoPersona)
            ? persona.Tipopersona
            : request.TipoPersona.Trim().ToUpper();

        persona.Tipopersona = tipoPersona;

        persona.Idtipoidentificacion = tipoPersona == "JURIDICA"
            ? null
            : request.IdTipoIdentificacion ?? persona.Idtipoidentificacion;

        if (!string.IsNullOrWhiteSpace(request.NumeroIdentificacion))
            persona.Numeroidentificacion = request.NumeroIdentificacion.Trim();

        if (!string.IsNullOrWhiteSpace(request.Nombres))
            persona.Nombres = request.Nombres.Trim();

        if (!string.IsNullOrWhiteSpace(request.Apellidos))
            persona.Apellidos = request.Apellidos.Trim();

        persona.Fechanacimiento = request.FechaNacimiento;
        persona.Idgenero = request.IdGenero;
        persona.Idnacionalidad = request.IdNacionalidad;

        persona.Email = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : request.Email.Trim();

        persona.Telefono = string.IsNullOrWhiteSpace(request.Telefono)
            ? null
            : request.Telefono.Trim();

        persona.Direccion = string.IsNullOrWhiteSpace(request.Direccion)
            ? null
            : request.Direccion.Trim();

        persona.Usuariomodificacion = UsuarioSistema;
        persona.Fechamodificacion = DateTime.UtcNow;
        persona.Ipmodificacion = IpSistema;

        // ── Actualizar datos laborales ───────────────────────────────
        empleado.Idcargo = request.IdCargo;
        empleado.Idmodotrabajo = request.IdModoTrabajo;
        empleado.Idcategoriaempleado = request.IdCategoriaEmpleado;
        empleado.Idtipocontrato = request.IdTipoContrato;
        empleado.Fechaingreso = request.FechaIngreso;
        empleado.Aniosexperiencia = request.AniosExperiencia;
        empleado.Activo = request.Activo;

        // Empresa / asociación.
        if (request.IdEmpresaCatalogo.HasValue)
            empleado.Idempresacatalogo = request.IdEmpresaCatalogo.Value;

        empleado.Usuariomodificacion = UsuarioSistema;
        empleado.Fechamodificacion = DateTime.UtcNow;
        empleado.Ipmodificacion = IpSistema;

        // ================================================================
        // NUEVO: Manejar el reemplazo en edición
        // ================================================================
        // Buscar el reemplazo actual (el inactivo que tiene este empleado como reemplazo)
        var reemplazoActual = await db.TblAdministracionEmpleados
            .FirstOrDefaultAsync(e => e.IdEmpleadoReemplazo == id && e.Activo == false, ct);

        // Si hay un reemplazo actual y es diferente al nuevo, limpiarlo
        if (reemplazoActual != null && (request.IdEmpleadoReemplazo == null || reemplazoActual.Id != request.IdEmpleadoReemplazo))
        {
            reemplazoActual.IdEmpleadoReemplazo = null;
            reemplazoActual.Usuariomodificacion = UsuarioSistema;
            reemplazoActual.Fechamodificacion = DateTime.UtcNow;
            reemplazoActual.Ipmodificacion = IpSistema;
        }

        // Si se envió un nuevo reemplazo, asignarlo
        if (request.IdEmpleadoReemplazo.HasValue)
        {
            var inactivo = await db.TblAdministracionEmpleados
                .FirstOrDefaultAsync(e => e.Id == request.IdEmpleadoReemplazo.Value && e.Activo == false, ct);

            if (inactivo != null && inactivo.Id != id)
            {
                inactivo.IdEmpleadoReemplazo = id;
                inactivo.Usuariomodificacion = UsuarioSistema;
                inactivo.Fechamodificacion = DateTime.UtcNow;
                inactivo.Ipmodificacion = IpSistema;
            }
        }

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


    // ================================================================
    // REGISTRAR SALIDA
    // ================================================================
    public async Task RegistrarSalidaAsync(int id, RegistrarSalidaRequest request, CancellationToken ct)
    {
        var empleado = await db.TblAdministracionEmpleados
            .Include(e => e.IdpersonaNavigation)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (empleado is null)
            throw new InvalidOperationException("El colaborador no existe.");

        if (!empleado.Activo)
            throw new InvalidOperationException("El colaborador ya está inactivo.");

        if (request.IdEmpleadoReemplazo.HasValue)
        {
            var reemplazo = await db.TblAdministracionEmpleados
                .FirstOrDefaultAsync(e => e.Id == request.IdEmpleadoReemplazo.Value, ct);

            if (reemplazo is null)
                throw new InvalidOperationException("El colaborador de reemplazo no existe.");

            if (reemplazo.Id == id)
                throw new InvalidOperationException("Un colaborador no puede reemplazarse a sí mismo.");
        }

        var tipoSalida = await db.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(d => d.Id == request.IdTipoSalida, ct);

        if (tipoSalida is null)
            throw new InvalidOperationException("El tipo de salida seleccionado no es válido.");

        var causaSalida = await db.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(d => d.Id == request.IdCausaSalida, ct);

        if (causaSalida is null)
            throw new InvalidOperationException("La causa de salida seleccionada no es válida.");

        empleado.Activo = false;
        empleado.Fechaterminacion = request.FechaSalida;
        empleado.IdTipoSalida = request.IdTipoSalida;
        empleado.IdCausaSalida = request.IdCausaSalida;
        empleado.ComentarioSalida = request.Comentario;
        empleado.IdEmpleadoReemplazo = request.IdEmpleadoReemplazo;

        empleado.Usuariomodificacion = "SYSTEM";
        empleado.Fechamodificacion = DateTime.UtcNow;
        empleado.Ipmodificacion = "127.0.0.1";

        await db.SaveChangesAsync(ct);
    }
}