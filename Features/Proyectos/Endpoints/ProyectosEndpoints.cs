using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Proyectos.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using System.Security.Claims;

namespace tmr_backend.Features.Proyectos;

public static class ProyectosEndpoints
{
    private const string UsuarioSistema = "FRONTEND";
    private const string IpSistema = "127.0.0.1";

    public static void MapProyectosEndpoints(this IEndpointRouteBuilder app)
    {
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

            var estados = await db.TblAdministracionCatalogoDetalles
                .Where(d => d.Activo && d.Idcatalogo == 4)
                .OrderBy(d => d.Orden)
                .Select(d => new LookupDto(d.Id, d.Valor))
                .ToListAsync();

            var tipos = await db.TblAdministracionCatalogoDetalles
                .Where(d => d.Activo && d.Idcatalogo == 3)
                .OrderBy(d => d.Orden)
                .Select(d => new LookupDto(d.Id, d.Valor))
                .ToListAsync();

            return Results.Ok(new { clientes, lideres, estados, tipos });
        });

        group.MapPost("/", async (CrearProyectoRequest request, ApplicationDbContext db, HttpContext context) =>
        {
            // REGLA DE SEGURIDAD EN DESARROLLO: Forzamos ID de prueba local para usar Scalar sin Token JWT
            var usuarioId = "00000000-0000-0000-0000-000000000000";

            // NOTA: Cuando vayas a pasar a producción con la seguridad de la empresa, descomenta la línea de abajo:
            // var usuarioId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(usuarioId))
            {
                return Results.Json(new { isSuccess = false, message = "Token de sesión inválido o expirado." }, statusCode: 401);
            }

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Results.BadRequest(new { Mensaje = "El nombre del proyecto es requerido." });
            }

            var ids = await ResolverRelaciones(request.IdCliente, request.Cliente, request.IdTipoProyecto, request.Tipo,
                request.IdLider, request.Lider, request.IdEstadoProyecto, request.Estado, db);

            if (ids.IdEstadoProyecto == 0)
            {
                return Results.BadRequest(new { Mensaje = "Debe seleccionar o registrar un estado de proyecto valido." });
            }

            var proyecto = new TblTimeReportProyecto
            {
                Codigo = request.Codigo,
                Nombre = request.Nombre.Trim(),
                Descripcion = request.Descripcion,
                Idcliente = ids.IdCliente,
                Idtipoproyecto = ids.IdTipoProyecto,
                Idlider = ids.IdLider,
                Idestadoproyecto = ids.IdEstadoProyecto,
                Fechainicioplaneada = request.FechaInicio,
                Fechafinplaneada = request.FechaFin,
                Presupuesto = request.Presupuesto,
                Horasasignadas = request.Horas,
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            db.TblTimeReportProyectos.Add(proyecto);
            await db.SaveChangesAsync();
            await GuardarRecursos(proyecto.Id, request.Recursos, db);
            await db.SaveChangesAsync();

            var creado = await QueryProyectos(db).FirstAsync(p => p.Id == proyecto.Id);
            return Results.Created($"/api/proyectos/{proyecto.Id}", await MapProyecto(creado, db));
        })
        .RequireAuthorization();

        group.MapPut("/{id:int}", async (int id, ActualizarProyectoRequest request, ApplicationDbContext db, HttpContext context) =>
        {
            // REGLA DE SEGURIDAD EN DESARROLLO: Forzamos ID de prueba local para usar Scalar sin Token JWT
            var usuarioId = "00000000-0000-0000-0000-000000000000";

            // NOTA: Cuando vayas a pasar a producción con la seguridad de la empresa, descomenta la línea de abajo:
            // var usuarioId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(usuarioId))
            {
                return Results.Json(new { isSuccess = false, message = "Token de sesión inválido o expirado." }, statusCode: 401);
            }

            var proyecto = await db.TblTimeReportProyectos.FirstOrDefaultAsync(p => p.Id == id);

            if (proyecto is null)
            {
                return Results.NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Nombre))
            {
                return Results.BadRequest(new { Mensaje = "El nombre del proyecto es requerido." });
            }

            var ids = await ResolverRelaciones(request.IdCliente, request.Cliente, request.IdTipoProyecto, request.Tipo,
                request.IdLider, request.Lider, request.IdEstadoProyecto, request.Estado, db);

            if (ids.IdEstadoProyecto == 0)
            {
                return Results.BadRequest(new { Mensaje = "Debe seleccionar o registrar un estado de proyecto valido." });
            }

            proyecto.Codigo = request.Codigo;
            proyecto.Nombre = request.Nombre.Trim();
            proyecto.Descripcion = request.Descripcion;
            proyecto.Idcliente = ids.IdCliente;
            proyecto.Idtipoproyecto = ids.IdTipoProyecto;
            proyecto.Idlider = ids.IdLider;
            proyecto.Idestadoproyecto = ids.IdEstadoProyecto;
            proyecto.Fechainicioplaneada = request.FechaInicio;
            proyecto.Fechafinplaneada = request.FechaFin;
            proyecto.Presupuesto = request.Presupuesto;
            proyecto.Horasasignadas = request.Horas;
            proyecto.Usuariomodificacion = UsuarioSistema;
            proyecto.Fechamodificacion = DateTime.UtcNow;
            proyecto.Ipmodificacion = IpSistema;

            await GuardarRecursos(id, request.Recursos, db);
            await db.SaveChangesAsync();

            var actualizado = await QueryProyectos(db).FirstAsync(p => p.Id == id);
            return Results.Ok(await MapProyecto(actualizado, db));
        })
        .RequireAuthorization();

        group.MapDelete("/{id:int}", async (int id, ApplicationDbContext db, HttpContext context) =>
        {
            // REGLA DE SEGURIDAD EN DESARROLLO: Forzamos ID de prueba local para usar Scalar sin Token JWT
            var usuarioId = "00000000-0000-0000-0000-000000000000";

            // NOTA: Cuando vayas a pasar a producción con la seguridad de la empresa, descomenta la línea de abajo:
            // var usuarioId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(usuarioId))
            {
                return Results.Json(new { isSuccess = false, message = "Token de sesión inválido o expirado." }, statusCode: 401);
            }

            var proyecto = await db.TblTimeReportProyectos.FirstOrDefaultAsync(p => p.Id == id);

            if (proyecto is null)
            {
                return Results.NotFound();
            }

            proyecto.Activo = false;
            proyecto.Usuariomodificacion = UsuarioSistema;
            proyecto.Fechamodificacion = DateTime.UtcNow;
            proyecto.Ipmodificacion = IpSistema;

            await db.SaveChangesAsync();
            return Results.NoContent();
        })
        .RequireAuthorization();
    }

    private static IQueryable<TblTimeReportProyecto> QueryProyectos(ApplicationDbContext db) =>
        db.TblTimeReportProyectos
            .AsNoTracking()
            .Where(p => p.Activo)
            .Include(p => p.IdclienteNavigation)
            .Include(p => p.IdtipoproyectoNavigation)
            .Include(p => p.IdestadoproyectoNavigation)
            .Include(p => p.IdliderNavigation)
                .ThenInclude(l => l!.IdpersonaNavigation)
            .Include(p => p.TblTimeReportEmpleadoProyectos.Where(ep => ep.Activo))
                .ThenInclude(ep => ep.IdempleadoNavigation)
                    .ThenInclude(e => e!.IdpersonaNavigation)
            .Include(p => p.TblTimeReportEmpleadoProyectos.Where(ep => ep.Activo))
                .ThenInclude(ep => ep.IdempleadoNavigation)
                    .ThenInclude(e => e!.IdcargoNavigation);

    private static async Task<ProyectoResponse> MapProyecto(TblTimeReportProyecto proyecto, ApplicationDbContext db)
    {
        var lider = proyecto.IdliderNavigation?.IdpersonaNavigation;
        
        var nombreTipo = proyecto.IdtipoproyectoNavigation?.Nombretipo ?? string.Empty;
        
        var recursos = proyecto.TblTimeReportEmpleadoProyectos
            .Where(r => r.Activo)
            .Select(r =>
            {
                var persona = r.IdempleadoNavigation?.IdpersonaNavigation;
                var cargo = r.IdempleadoNavigation?.IdcargoNavigation;

                return new ProyectoRecursoResponse(
                    r.Id,
                    r.Idempleado,
                    r.Idproveedor is null ? "Interno" : "Externo",
                    persona is null ? string.Empty : $"{persona.Nombres} {persona.Apellidos}".Trim(),
                    r.Rolasignado ?? cargo?.Nombrecargo ?? string.Empty,
                    r.Fechaasignacion,
                    r.Fechafinasignacion,
                    r.Costoporhora ?? 0,
                    r.Horasasignadas ?? 0
                );
            })
            .ToList();

        return new ProyectoResponse(
            proyecto.Id,
            proyecto.Codigo ?? $"PRY-{proyecto.Id:000}",
            proyecto.Nombre,
            proyecto.Descripcion ?? string.Empty,
            proyecto.Idcliente,
            proyecto.IdclienteNavigation?.Nombrecomercial ?? proyecto.IdclienteNavigation?.Razonsocial ?? string.Empty,
            proyecto.Idtipoproyecto,
            nombreTipo,
            proyecto.Idlider,
            lider is null ? string.Empty : $"{lider.Nombres} {lider.Apellidos}".Trim(),
            string.Empty,
            0,
            0,
            proyecto.Idestadoproyecto,
            proyecto.IdestadoproyectoNavigation?.Valor ?? string.Empty,
            proyecto.Fechainicioplaneada,
            proyecto.Fechafinplaneada,
            proyecto.Presupuesto ?? 0,
            proyecto.Horasasignadas ?? 0,
            recursos.Count,
            proyecto.Activo,
            proyecto.Fechacreacion,
            recursos
        );
    }

    private static async Task<(int? IdCliente, int? IdTipoProyecto, int? IdLider, int IdEstadoProyecto)> ResolverRelaciones(
        int? idCliente,
        string? cliente,
        int? idTipoProyecto,
        string? tipo,
        int? idLider,
        string? lider,
        int idEstadoProyecto,
        string? estado,
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

        idLider ??= await db.TblAdministracionLiders
            .Where(l => l.Activo && (l.IdpersonaNavigation.Nombres + " " + l.IdpersonaNavigation.Apellidos) == lider)
            .Select(l => (int?)l.Id)
            .FirstOrDefaultAsync();

        if (idEstadoProyecto == 0 && !string.IsNullOrWhiteSpace(estado))
        {
            idEstadoProyecto = await db.TblAdministracionCatalogoDetalles
                .Where(d => d.Activo && d.Valor == estado && d.IdcatalogoNavigation.Codigo == "EPR")
                .Select(d => d.Id)
                .FirstOrDefaultAsync();
        }

        return (idCliente, idTipoProyecto, idLider, idEstadoProyecto);
    }

    private static async Task GuardarRecursos(int idProyecto, List<ProyectoRecursoRequest>? recursos, ApplicationDbContext db)
    {
        var actuales = await db.TblTimeReportEmpleadoProyectos
            .Where(r => r.Idproyecto == idProyecto && r.Activo)
            .ToListAsync();

        foreach (var actual in actuales)
        {
            actual.Activo = false;
            actual.Usuariomodificacion = UsuarioSistema;
            actual.Fechamodificacion = DateTime.UtcNow;
            actual.Ipmodificacion = IpSistema;
        }

        foreach (var recurso in recursos ?? [])
        {
            db.TblTimeReportEmpleadoProyectos.Add(new TblTimeReportEmpleadoProyecto
            {
                Idproyecto = idProyecto,
                Idempleado = recurso.IdEmpleado,
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
