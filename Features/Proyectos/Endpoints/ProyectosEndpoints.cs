using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using tmr_backend.Features.Proyectos.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

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

        // Descargar listado de proyectos en Excel
        group.MapGet("/download", async (ApplicationDbContext db) =>
        {
            var proyectosEnt = await QueryProyectos(db)
                .OrderByDescending(p => p.Fechacreacion)
                .ToListAsync();

            var proyectos = proyectosEnt.Select(p => new
            {
                Codigo = p.Codigo ?? $"PRY-{p.Id:000}",
                Nombre = p.Nombre,
                Cliente = p.IdclienteNavigation != null ? (p.IdclienteNavigation.Nombrecomercial ?? p.IdclienteNavigation.Razonsocial ?? string.Empty) : string.Empty,
                TipoProyecto = p.IdtipoproyectoNavigation?.Nombretipo ?? string.Empty,
                Lider = p.IdliderNavigation?.IdpersonaNavigation != null ? (p.IdliderNavigation.IdpersonaNavigation.Nombres + " " + p.IdliderNavigation.IdpersonaNavigation.Apellidos).Trim() : string.Empty,
                Estado = p.IdestadoproyectoNavigation?.Valor ?? string.Empty,
                FechaInicio = p.Fechainicioplaneada.HasValue ? p.Fechainicioplaneada.Value.ToString("yyyy-MM-dd") : string.Empty,
                FechaFin = p.Fechafinplaneada.HasValue ? p.Fechafinplaneada.Value.ToString("yyyy-MM-dd") : string.Empty,
                Presupuesto = p.Presupuesto ?? 0,
                HorasAsignadas = p.Horasasignadas ?? 0,
                Recursos = p.TblTimeReportEmpleadoProyectos?.Count(r => r.Activo) ?? 0
            }).ToList();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Proyectos");
            // Encabezados
            ws.Cell(1, 1).Value = "Código";
            ws.Cell(1, 2).Value = "Nombre";
            ws.Cell(1, 3).Value = "Cliente";
            ws.Cell(1, 4).Value = "Tipo";
            ws.Cell(1, 5).Value = "Líder";
            ws.Cell(1, 6).Value = "Estado";
            ws.Cell(1, 7).Value = "Fecha inicio";
            ws.Cell(1, 8).Value = "Fecha fin";
            ws.Cell(1, 9).Value = "Presupuesto";
            ws.Cell(1, 10).Value = "Horas asignadas";
            ws.Cell(1, 11).Value = "Recursos";

            for (var i = 0; i < proyectos.Count; i++)
            {
                var row = i + 2;
                var p = proyectos[i];
                ws.Cell(row, 1).Value = p.Codigo;
                ws.Cell(row, 2).Value = p.Nombre;
                ws.Cell(row, 3).Value = p.Cliente;
                ws.Cell(row, 4).Value = p.TipoProyecto;
                ws.Cell(row, 5).Value = p.Lider;
                ws.Cell(row, 6).Value = p.Estado;
                ws.Cell(row, 7).Value = p.FechaInicio;
                ws.Cell(row, 8).Value = p.FechaFin;
                ws.Cell(row, 9).Value = p.Presupuesto;
                ws.Cell(row, 10).Value = p.HorasAsignadas;
                ws.Cell(row, 11).Value = p.Recursos;
            }

            ws.Columns().AdjustToContents();

            await using var ms = new System.IO.MemoryStream();
            workbook.SaveAs(ms);
            ms.Position = 0;
            return Results.File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "proyectos.xlsx");
        });

        // Descargar listado de proyectos en PDF
        group.MapGet("/download/pdf", async (ApplicationDbContext db) =>
        {
            var proyectosEnt = await QueryProyectos(db)
                .OrderByDescending(p => p.Fechacreacion)
                .ToListAsync();

            var proyectos = proyectosEnt.Select(p => new
            {
                Codigo = p.Codigo ?? $"PRY-{p.Id:000}",
                Nombre = p.Nombre,
                Cliente = p.IdclienteNavigation != null ? (p.IdclienteNavigation.Nombrecomercial ?? p.IdclienteNavigation.Razonsocial ?? string.Empty) : string.Empty,
                TipoProyecto = p.IdtipoproyectoNavigation?.Nombretipo ?? string.Empty,
                Lider = p.IdliderNavigation?.IdpersonaNavigation != null ? (p.IdliderNavigation.IdpersonaNavigation.Nombres + " " + p.IdliderNavigation.IdpersonaNavigation.Apellidos).Trim() : string.Empty,
                Estado = p.IdestadoproyectoNavigation?.Valor ?? string.Empty,
                FechaInicio = p.Fechainicioplaneada.HasValue ? p.Fechainicioplaneada.Value.ToString("yyyy-MM-dd") : string.Empty,
                FechaFin = p.Fechafinplaneada.HasValue ? p.Fechafinplaneada.Value.ToString("yyyy-MM-dd") : string.Empty,
                Presupuesto = p.Presupuesto ?? 0,
                HorasAsignadas = p.Horasasignadas ?? 0,
                Recursos = p.TblTimeReportEmpleadoProyectos?.Count(r => r.Activo) ?? 0
            }).ToList();

            await using var ms = new System.IO.MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Content().Column(column =>
                    {
                        column.Item().Text("Listado de Proyectos").FontSize(16).Bold();
                        column.Item().PaddingTop(10).Element(c =>
                        {
                            c.Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(3);
                                    cols.RelativeColumn(3);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(1);
                                    cols.RelativeColumn(1);
                                });

                                // Header
                                table.Header(header =>
                                {
                                    header.Cell().Text("Código").Bold();
                                    header.Cell().Text("Nombre").Bold();
                                    header.Cell().Text("Cliente").Bold();
                                    header.Cell().Text("Tipo").Bold();
                                    header.Cell().Text("Líder").Bold();
                                    header.Cell().Text("Estado").Bold();
                                    header.Cell().Text("Fecha inicio").Bold();
                                    header.Cell().Text("Fecha fin").Bold();
                                    header.Cell().Text("Presupuesto").Bold();
                                    header.Cell().Text("Horas").Bold();
                                    header.Cell().Text("Recursos").Bold();
                                });

                                foreach (var p in proyectos)
                                {
                                    table.Cell().Text(p.Codigo ?? string.Empty);
                                    table.Cell().Text(p.Nombre ?? string.Empty);
                                    table.Cell().Text(p.Cliente ?? string.Empty);
                                    table.Cell().Text(p.TipoProyecto ?? string.Empty);
                                    table.Cell().Text(p.Lider ?? string.Empty);
                                    table.Cell().Text(p.Estado ?? string.Empty);
                                    table.Cell().Text(p.FechaInicio ?? string.Empty);
                                    table.Cell().Text(p.FechaFin ?? string.Empty);
                                    table.Cell().Text(p.Presupuesto.ToString());
                                    table.Cell().Text(p.HorasAsignadas.ToString());
                                    table.Cell().Text(p.Recursos.ToString());
                                }
                            });
                        });
                    });
                });
            }).GeneratePdf(ms);

            ms.Position = 0;
            return Results.File(ms.ToArray(), "application/pdf", "proyectos.pdf");
        });

        group.MapPost("/", async (CrearProyectoRequest request, ApplicationDbContext db) =>
        {
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
        });

        group.MapPut("/{id:int}", async (int id, ActualizarProyectoRequest request, ApplicationDbContext db) =>
        {
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
        });

        group.MapDelete("/{id:int}", async (int id, ApplicationDbContext db) =>
        {
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
        });
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
