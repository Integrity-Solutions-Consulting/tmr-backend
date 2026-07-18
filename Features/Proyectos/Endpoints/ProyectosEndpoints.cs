using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Proyectos.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using System;
using System.Security.Claims;

namespace tmr_backend.Features.Proyectos;

public static class ProyectosEndpoints
{
    private const string UsuarioSistema = "FRONTEND";
    private const string IpSistema = "127.0.0.1";

    public static void MapProyectosEndpoints(this IEndpointRouteBuilder app)
    {
        var env = app.ServiceProvider.GetService(typeof(Microsoft.Extensions.Hosting.IHostEnvironment)) as Microsoft.Extensions.Hosting.IHostEnvironment;
        var group = app.MapGroup("/api/proyectos").WithTags("Proyectos");

        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var proyectos = await QueryProyectos(db)
                .OrderByDescending(p => p.Fechacreacion)
                .ToListAsync();

            var resultado = new List<ProyectoResponse>();
            foreach (var p in proyectos)
            {
                resultado.Add(await MapProyecto(p, db));
            }
            return Results.Ok(resultado);
        });

        group.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
        {
            var proyecto = await QueryProyectos(db).FirstOrDefaultAsync(p => p.Id == id);
            return proyecto is null ? Results.NotFound() : Results.Ok(await MapProyecto(proyecto, db));
        });

        group.MapGet("/lookups", async (ApplicationDbContext db) =>
        {
            var clientes = await db.TblAdministracionClientes
                .Where(c => c.Activo)
                .OrderBy(c => c.Nombrecomercial ?? c.Razonsocial)
                .Select(c => new LookupDto(c.Id, c.Nombrecomercial ?? c.Razonsocial ?? $"Cliente {c.Id}"))
                .ToListAsync();

            var lideres = await db.TblAdministracionLiders
                .Where(l => l.Activo)
                .OrderBy(l => l.IdpersonaNavigation.Nombres)
                .Select(l => new LookupDto(l.Id, (l.IdpersonaNavigation.Nombres + " " + l.IdpersonaNavigation.Apellidos).Trim()))
                .ToListAsync();

            var empleados = await db.TblAdministracionEmpleados
                .Where(e => e.Activo)
                .OrderBy(e => e.IdpersonaNavigation.Nombres)
                .Select(e => new LookupDto(e.Id, (e.IdpersonaNavigation.Nombres + " " + e.IdpersonaNavigation.Apellidos).Trim()))
                .ToListAsync();

            var estados = await db.TblAdministracionCatalogoDetalles
                .Include(d => d.IdcatalogoNavigation)
                .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == "EPR")
                .OrderBy(d => d.Valor)
                .Select(d => new LookupDto(d.Id, d.Valor))
                .ToListAsync();

            var tipos = await db.TblAdministracionCatalogoDetalles
                .Include(d => d.IdcatalogoNavigation)
                .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == "TPR")
                .OrderBy(d => d.Valor)
                .Select(d => new LookupDto(d.Id, d.Valor))
                .ToListAsync();

            var departamentos = await db.TblAdministracionCatalogoDetalles
                .Include(d => d.IdcatalogoNavigation)
                .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == "DEP")
                .OrderBy(d => d.Orden)
                .Select(d => new LookupDto(d.Id, d.Valor))
                .ToListAsync();

            var cargos = await db.TblAdministracionCargos
                .Where(c => c.Activo)
                .OrderBy(c => c.Nombrecargo)
                .Select(c => new { id = c.Id, nombre = c.Nombrecargo, idDepartamento = c.Iddepartamento })
                .ToListAsync();

            return Results.Ok(new { clientes, lideres, empleados, estados, tipos, departamentos, cargos });
        });

        var postEndpoint = group.MapPost("/", async (CrearProyectoRequest request, ApplicationDbContext db, HttpContext context) =>
        {
            var usuarioId = "00000000-0000-0000-0000-000000000000";
            // var usuarioId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(usuarioId))
                return Results.Json(new { isSuccess = false, message = "Token de sesión inválido o expirado." }, statusCode: 401);

            if (string.IsNullOrWhiteSpace(request.Nombre))
                return Results.BadRequest(new { Mensaje = "El nombre del proyecto es requerido." });

            var ids = await ResolverRelaciones(request.IdCliente, request.Cliente, request.IdTipoProyecto, request.Tipo, db);
            var lideres = NormalizarLideres(request);
            var idEstadoProyectoActivo = await ObtenerOCrearEstadoProyectoActivoAsync(db);

            var proyecto = new TblTimeReportProyecto
            {
                Codigo = request.Codigo,
                Nombre = request.Nombre.Trim(),
                Descripcion = request.Descripcion,
                Idcliente = ids.IdCliente,
                Idtipoproyecto = ids.IdTipoProyecto,
                Idestadoproyecto = request.IdEstadoProyecto ?? idEstadoProyectoActivo,
                Fechainicioplaneada = request.FechaInicio,
                Fechafinplaneada = request.FechaFin,
                Presupuesto = request.Presupuesto,
                Horasasignadas = request.Horas,
                Activo = request.Estado is null ? true : EsEstadoActivo(request.Estado),
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema,
                Observacion = request.Observacion,
                Fechainicioespera = request.FechaInicioEspera,
                Fechafinespera = request.FechaFinEspera
            };

            db.TblTimeReportProyectos.Add(proyecto);
            await db.SaveChangesAsync();

            var tieneAsignaciones = lideres.Any(l =>
                l.IdLider.HasValue ||
                !string.IsNullOrWhiteSpace(l.Lider) ||
                (l.Recursos != null && l.Recursos.Count > 0));

            if (tieneAsignaciones)
            {
                await GuardarAsignaciones(proyecto.Id, lideres, db);
                await db.SaveChangesAsync();
            }

            var creado = await QueryProyectos(db).FirstOrDefaultAsync(p => p.Id == proyecto.Id);
            if (creado is null)
                return Results.Problem("No se pudo recuperar el proyecto recién creado.", statusCode: 500);

            return Results.Created($"/api/proyectos/{proyecto.Id}", await MapProyecto(creado, db));
        });
        if (!(env?.IsDevelopment() ?? false)) postEndpoint.RequireAuthorization();

        var putEndpoint = group.MapPut("/{id:int}", async (int id, ActualizarProyectoRequest request, ApplicationDbContext db, HttpContext context) =>
        {
            var usuarioId = "00000000-0000-0000-0000-000000000000";
            // var usuarioId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(usuarioId))
                return Results.Json(new { isSuccess = false, message = "Token de sesión inválido o expirado." }, statusCode: 401);

            var proyecto = await db.TblTimeReportProyectos.FirstOrDefaultAsync(p => p.Id == id);
            if (proyecto is null)
                return Results.NotFound();

            var ids = await ResolverRelaciones(request.IdCliente, request.Cliente, request.IdTipoProyecto, request.Tipo, db);
            var lideres = NormalizarLideres(request);

            int? idEstadoProyectoActivo = null;
            if (!request.IdEstadoProyecto.HasValue)
                idEstadoProyectoActivo = await ObtenerOCrearEstadoProyectoActivoAsync(db);

            proyecto.Descripcion = request.Descripcion;
            proyecto.Idtipoproyecto = ids.IdTipoProyecto;
            proyecto.Idestadoproyecto = request.IdEstadoProyecto ?? idEstadoProyectoActivo ?? proyecto.Idestadoproyecto;
            proyecto.Fechainicioplaneada = request.FechaInicio;
            proyecto.Fechafinplaneada = request.FechaFin;
            proyecto.Presupuesto = request.Presupuesto;
            proyecto.Horasasignadas = request.Horas;
            proyecto.Observacion = request.Observacion;
            proyecto.Fechafinreal = request.FechaFinReal;
            proyecto.Fechainicioespera = request.FechaInicioEspera;
            proyecto.Fechafinespera = request.FechaFinEspera;

            if (request.Estado is not null)
            {
                proyecto.Activo = EsEstadoActivo(request.Estado);
            }
            else if (request.Activo.HasValue)
            {
                proyecto.Activo = request.Activo.Value;
            }

            proyecto.Usuariomodificacion = UsuarioSistema;
            proyecto.Fechamodificacion = DateTime.UtcNow;
            proyecto.Ipmodificacion = IpSistema;

            var tieneAsignaciones = lideres.Any(l =>
                l.IdLider.HasValue ||
                !string.IsNullOrWhiteSpace(l.Lider) ||
                (l.Recursos != null && l.Recursos.Count > 0));

            if (tieneAsignaciones)
                await GuardarAsignaciones(id, lideres, db);

            await db.SaveChangesAsync();

            var actualizado = await QueryProyectos(db).FirstOrDefaultAsync(p => p.Id == id);
            if (actualizado is null)
                return Results.Problem("No se pudo recuperar el proyecto actualizado.", statusCode: 500);

            return Results.Ok(await MapProyecto(actualizado, db));
        });
        if (!(env?.IsDevelopment() ?? false)) putEndpoint.RequireAuthorization();

        var deleteEndpoint = group.MapDelete("/{id:int}", async (int id, ApplicationDbContext db, HttpContext context) =>
        {
            var usuarioId = "00000000-0000-0000-0000-000000000000";
            // var usuarioId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(usuarioId))
                return Results.Json(new { isSuccess = false, message = "Token de sesión inválido o expirado." }, statusCode: 401);

            var proyecto = await db.TblTimeReportProyectos.FirstOrDefaultAsync(p => p.Id == id);
            if (proyecto is null)
                return Results.NotFound();

            proyecto.Activo = false;
            proyecto.Usuariomodificacion = UsuarioSistema;
            proyecto.Fechamodificacion = DateTime.UtcNow;
            proyecto.Ipmodificacion = IpSistema;

            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        if (!(env?.IsDevelopment() ?? false)) deleteEndpoint.RequireAuthorization();
    }

    private static IQueryable<TblTimeReportProyecto> QueryProyectos(ApplicationDbContext db) =>
        db.TblTimeReportProyectos
            .AsNoTracking()
            .Include(p => p.IdclienteNavigation)
            .Include(p => p.IdtipoproyectoNavigation)
            .Include(p => p.IdestadoproyectoNavigation)
            .Include(p => p.TblTimeReportAsignacionProyectos.Where(ep => ep.Activo))
                .ThenInclude(ep => ep.IdliderNavigation)
                    .ThenInclude(l => l!.IdpersonaNavigation)
            .Include(p => p.TblTimeReportAsignacionProyectos.Where(ep => ep.Activo))
                .ThenInclude(ep => ep.IdempleadoNavigation)
                    .ThenInclude(e => e!.IdpersonaNavigation)
            .Include(p => p.TblTimeReportAsignacionProyectos.Where(ep => ep.Activo))
                .ThenInclude(ep => ep.IdempleadoNavigation)
                    .ThenInclude(e => e!.IdcargoNavigation);

    private static async Task<ProyectoResponse> MapProyecto(TblTimeReportProyecto proyecto, ApplicationDbContext db)
    {
        var nombreTipo = proyecto.IdtipoproyectoNavigation?.Nombretipo ?? string.Empty;

        var asignacionesActivas = proyecto.TblTimeReportAsignacionProyectos
            .Where(r => r.Activo)
            .ToList();

        var lideres = asignacionesActivas
            .GroupBy(r => r.Idlider)
            .Select(r =>
            {
                var liderAsignacion = r.FirstOrDefault(x => x.Idempleado == null && x.Idlider != null)
                    ?? r.FirstOrDefault(x => x.Idlider != null);
                var liderPersona = liderAsignacion?.IdliderNavigation?.IdpersonaNavigation;
                var recursosGrupo = r
                    .Where(x => x.Idempleado != null)
                    .Select(x =>
                    {
                        var persona = x.IdempleadoNavigation?.IdpersonaNavigation;
                        var cargo = x.IdempleadoNavigation?.IdcargoNavigation;
                        return new ProyectoRecursoResponse(
                            x.Id,
                            x.Idempleado,
                            x.Idproveedor is null ? "Interno" : "Externo",
                            persona is null ? string.Empty : $"{persona.Nombres} {persona.Apellidos}".Trim(),
                            x.Rolasignado ?? cargo?.Nombrecargo ?? string.Empty,
                            x.Fechaasignacion,
                            x.Fechafinasignacion,
                            x.Costoporhora ?? 0,
                            x.Horasasignadas ?? 0,
                            cargo?.Iddepartamento
                        );
                    })
                    .ToList();

                return new ProyectoLiderResponse(
                    liderAsignacion?.Idlider,
                    liderPersona is null ? string.Empty : $"{liderPersona.Nombres} {liderPersona.Apellidos}".Trim(),
                    liderAsignacion?.IdliderNavigation?.IdtipoNavigation?.Valor ?? string.Empty,
                    liderAsignacion?.Lidercosto ?? 0,
                    liderAsignacion?.Liderhoras ?? 0,
                    recursosGrupo
                );
            })
            .ToList();

        var recursos = lideres.SelectMany(l => l.Recursos).ToList();
        var liderResumen = lideres.FirstOrDefault();

        // ═══ CONSTRUCTOR CON TODOS LOS ARGUMENTOS CON NOMBRE ═══
        return new ProyectoResponse(
            Id: proyecto.Id,
            Codigo: proyecto.Codigo ?? $"PRY-{proyecto.Id:000}",
            Nombre: proyecto.Nombre,
            Descripcion: proyecto.Descripcion ?? string.Empty,
            IdCliente: proyecto.Idcliente,
            Cliente: proyecto.IdclienteNavigation?.Nombrecomercial ?? proyecto.IdclienteNavigation?.Razonsocial ?? string.Empty,
            IdTipoProyecto: proyecto.Idtipoproyecto,
            Tipo: nombreTipo,
            Observacion: proyecto.Observacion,
            FechaFinReal: proyecto.Fechafinreal,
            FechaInicioEspera: proyecto.Fechainicioespera,
            FechaFinEspera: proyecto.Fechafinespera,
            IdLider: liderResumen?.IdLider,
            Lider: liderResumen?.Lider ?? string.Empty,
            CargoLider: liderResumen?.CargoLider ?? string.Empty,
            CostoHoraLider: liderResumen?.CostoHoraLider ?? 0,
            HorasLider: liderResumen?.HorasLider ?? 0,
            IdEstadoProyecto: proyecto.Idestadoproyecto,
            Estado: proyecto.Activo ? "Activo" : "Inactivo",
            FechaInicio: proyecto.Fechainicioplaneada,
            FechaFin: proyecto.Fechafinplaneada,
            Presupuesto: proyecto.Presupuesto ?? 0,
            Horas: proyecto.Horasasignadas ?? 0,
            NumeroRecursos: recursos.Count,
            Activo: proyecto.Activo,
            FechaCreacion: proyecto.Fechacreacion,
            Recursos: recursos,
            Lideres: lideres
        );
    }

    private static async Task<(int? IdCliente, int? IdTipoProyecto)> ResolverRelaciones(
        int? idCliente,
        string? cliente,
        int? idTipoProyecto,
        string? tipo,
        ApplicationDbContext db)
    {
        idCliente ??= await db.TblAdministracionClientes
            .Where(c => c.Activo && (c.Nombrecomercial == cliente || c.Razonsocial == cliente))
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();

        if (idTipoProyecto.HasValue)
        {
            var tipoProyectoExistente = await db.TblTimeReportTipoProyectos
                .Where(t => t.Id == idTipoProyecto.Value && t.Activo)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();

            if (tipoProyectoExistente is null)
            {
                var tipoDetalle = await db.TblAdministracionCatalogoDetalles
                    .Where(d => d.Activo && d.Idcatalogo == 3 && d.Id == idTipoProyecto.Value)
                    .Select(d => new { d.Codigovalor, d.Valor })
                    .FirstOrDefaultAsync();

                if (tipoDetalle is not null)
                {
                    idTipoProyecto = await db.TblTimeReportTipoProyectos
                        .Where(t => t.Activo && (t.Nombretipo == tipoDetalle.Valor || t.Nombretipo == tipoDetalle.Codigovalor))
                        .Select(t => (int?)t.Id)
                        .FirstOrDefaultAsync();

                    if (idTipoProyecto is null)
                    {
                        var nuevoTipo = new TblTimeReportTipoProyecto
                        {
                            Nombretipo = tipoDetalle.Valor,
                            Activo = true,
                            Usuariocreacion = UsuarioSistema,
                            Fechacreacion = DateTime.UtcNow,
                            Ipcreacion = IpSistema
                        };
                        db.TblTimeReportTipoProyectos.Add(nuevoTipo);
                        await db.SaveChangesAsync();
                        idTipoProyecto = nuevoTipo.Id;
                    }
                }
            }
        }

        if (!idTipoProyecto.HasValue && !string.IsNullOrWhiteSpace(tipo))
        {
            idTipoProyecto = await db.TblTimeReportTipoProyectos
                .Where(t => t.Activo && t.Nombretipo == tipo)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync();

            if (!idTipoProyecto.HasValue)
            {
                var tipoDetalle = await db.TblAdministracionCatalogoDetalles
                    .Where(d => d.Activo && d.Idcatalogo == 3 && (d.Valor == tipo || d.Codigovalor == tipo))
                    .Select(d => d.Valor)
                    .FirstOrDefaultAsync();

                var nombreTipo = !string.IsNullOrWhiteSpace(tipoDetalle) ? tipoDetalle : tipo.Trim();
                var nuevoTipo = new TblTimeReportTipoProyecto
                {
                    Nombretipo = nombreTipo,
                    Activo = true,
                    Usuariocreacion = UsuarioSistema,
                    Fechacreacion = DateTime.UtcNow,
                    Ipcreacion = IpSistema
                };
                db.TblTimeReportTipoProyectos.Add(nuevoTipo);
                await db.SaveChangesAsync();
                idTipoProyecto = nuevoTipo.Id;
            }
        }

        return (idCliente, idTipoProyecto);
    }

    private static bool EsEstadoActivo(string? estado)
    {
        if (string.IsNullOrWhiteSpace(estado)) return false;
        var v = estado.Trim().ToUpperInvariant();
        return v == "ACTIVO" || v == "ACT" || v == "A" || v == "TRUE" || v == "1";
    }

    private static async Task<int> ObtenerOCrearEstadoProyectoActivoAsync(ApplicationDbContext db)
    {
        var estadoActivo = await db.TblAdministracionCatalogoDetalles
            .Where(d => d.Activo
                && d.IdcatalogoNavigation.Codigo == "EPR"
                && (d.Valor == "Activo" || d.Valor == "ACTIVO" || d.Codigovalor == "ACT" || d.Codigovalor == "ACTIVO"))
            .Select(d => d.Id)
            .FirstOrDefaultAsync();

        if (estadoActivo != 0)
            return estadoActivo;

        var catalogoEstado = await db.TblAdministracionCatalogos
            .FirstOrDefaultAsync(c => c.Activo && c.Tipocatalogo == "ADM" && c.Codigo == "EPR");

        if (catalogoEstado is null)
        {
            catalogoEstado = new TblAdministracionCatalogo
            {
                Tipocatalogo = "ADM",
                Codigo = "EPR",
                Descripcion = "Estado de proyecto",
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            db.TblAdministracionCatalogos.Add(catalogoEstado);
            await db.SaveChangesAsync();
        }

        var detalleEstado = await db.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(d => d.Activo && d.Idcatalogo == catalogoEstado.Id && d.Codigovalor == "ACT");

        if (detalleEstado is null)
        {
            detalleEstado = new TblAdministracionCatalogoDetalle
            {
                Idcatalogo = catalogoEstado.Id,
                Codigovalor = "ACT",
                Valor = "Activo",
                Descripcion = "Estado activo del proyecto",
                Orden = 1,
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            db.TblAdministracionCatalogoDetalles.Add(detalleEstado);
            await db.SaveChangesAsync();
        }

        var idFinal = await db.TblAdministracionCatalogoDetalles
            .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == "EPR")
            .OrderBy(d => d.Orden)
            .Select(d => d.Id)
            .FirstOrDefaultAsync();

        return idFinal != 0 ? idFinal : detalleEstado.Id;
    }

    private static List<ProyectoLiderRequest> NormalizarLideres(CrearProyectoRequest request)
    {
        if (request.Lideres is { Count: > 0 })
            return request.Lideres;

        return [new ProyectoLiderRequest(request.IdLider, request.Lider, request.LiderCosto, request.LiderHoras, request.Recursos)];
    }

    private static List<ProyectoLiderRequest> NormalizarLideres(ActualizarProyectoRequest request)
    {
        if (request.Lideres is { Count: > 0 })
            return request.Lideres;

        return [new ProyectoLiderRequest(request.IdLider, request.Lider, request.LiderCosto, request.LiderHoras, request.Recursos)];
    }

    private static async Task GuardarAsignaciones(int idProyecto, List<ProyectoLiderRequest> lideres, ApplicationDbContext db)
    {
        var actuales = await db.TblTimeReportAsignacionProyectos
            .Where(r => r.Idproyecto == idProyecto && r.Activo)
            .ToListAsync();

        foreach (var actual in actuales)
        {
            actual.Activo = false;
            actual.Usuariomodificacion = UsuarioSistema;
            actual.Fechamodificacion = DateTime.UtcNow;
            actual.Ipmodificacion = IpSistema;
        }

        foreach (var lider in lideres)
        {
            var idLider = await ResolverLiderIdAsync(lider.IdLider, lider.Lider, db);

            if (idLider.HasValue || lider.LiderCosto.HasValue || lider.LiderHoras.HasValue)
            {
                db.TblTimeReportAsignacionProyectos.Add(new TblTimeReportAsignacionProyecto
                {
                    Idproyecto = idProyecto,
                    Idlider = idLider,
                    Lidercosto = lider.LiderCosto,
                    Liderhoras = lider.LiderHoras,
                    Activo = true,
                    Usuariocreacion = UsuarioSistema,
                    Fechacreacion = DateTime.UtcNow,
                    Ipcreacion = IpSistema
                });
            }

            foreach (var recurso in lider.Recursos ?? [])
            {
                db.TblTimeReportAsignacionProyectos.Add(new TblTimeReportAsignacionProyecto
                {
                    Idproyecto = idProyecto,
                    Idempleado = recurso.IdEmpleado,
                    Idlider = idLider,
                    Fechaasignacion = recurso.Entrada,
                    Fechafinasignacion = recurso.Salida,
                    Rolasignado = recurso.Rol,
                    Costoporhora = recurso.CostoHora,
                    Horasasignadas = recurso.Horas,
                    Activo = true,
                    Usuariocreacion = UsuarioSistema,
                    Fechacreacion = DateTime.UtcNow,
                    Ipcreacion = IpSistema
                });
            }
        }
    }

    private static async Task<int?> ResolverLiderIdAsync(int? idLider, string? lider, ApplicationDbContext db)
    {
        if (idLider.HasValue)
        {
            var existente = await db.TblAdministracionLiders
                .Where(l => l.Activo && l.Id == idLider.Value)
                .Select(l => (int?)l.Id)
                .FirstOrDefaultAsync();

            if (existente.HasValue)
                return existente;
        }

        if (string.IsNullOrWhiteSpace(lider))
            return null;

        return await db.TblAdministracionLiders
            .Where(l => l.Activo && (l.IdpersonaNavigation.Nombres + " " + l.IdpersonaNavigation.Apellidos) == lider)
            .Select(l => (int?)l.Id)
            .FirstOrDefaultAsync();
    }
}