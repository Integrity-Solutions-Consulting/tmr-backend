using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using tmr_backend.Features.Reportes.Domain;
using tmr_backend.Features.Reportes.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Reportes;

public static class ReportesEndpoints
{
    public static void MapReportesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reportes").WithTags("Reportes"); //.RequireAuthorization();

        // 0. Test DB Connection
        group.MapGet("/test-db", async (ApplicationDbContext db) =>
        {
            try
            {
                var count = await db.TblTimeReportActividadDiaria.CountAsync();
                return Results.Ok(new { message = "Conexión a DB y tabla exitosa", cantidadRegistros = count });
            }
            catch (System.Exception ex)
            {
                return Results.Problem($"Error conectando a la BD: {ex.Message}");
            }
        });

        // 1. Reporte por Horas
        group.MapGet("/horas", async (
            [AsParameters] ReporteHorasFiltroRequest filtro,
            ApplicationDbContext db) =>
        {
            var query = db.TblTimeReportActividadDiaria
                .Include(a => a.IdempleadoNavigation)
                .Include(a => a.IdproyectoNavigation!).ThenInclude(p => p!.IdclienteNavigation)
                .Where(a => a.Activo);

            if (!string.IsNullOrWhiteSpace(filtro.Cliente))
                query = query.Where(a => a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null && a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial != null && a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial.ToLower().Contains(filtro.Cliente.ToLower()));

            if (filtro.Mes.HasValue)
                query = query.Where(a => a.Fechaactividad.Month == filtro.Mes.Value);

            if (filtro.Anio.HasValue)
                query = query.Where(a => a.Fechaactividad.Year == filtro.Anio.Value);

            // Realizamos la agrupación
            var groupedQuery = query
                .GroupBy(a => new {
                    Cliente = a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null ? a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial : "Sin Cliente",
                    ClienteActivo = a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null ? (bool?)a.IdproyectoNavigation.IdclienteNavigation.Activo : null,
                    Mes = a.Fechaactividad.Month,
                    Anio = a.Fechaactividad.Year
                })
                .Select(g => new {
                    Cliente = g.Key.Cliente,
                    ClienteActivo = g.Key.ClienteActivo,
                    MesNum = g.Key.Mes,
                    Anio = g.Key.Anio.ToString(),
                    Recursos = g.Select(x => x.Idempleado).Distinct().Count(),
                    Horas = g.Sum(x => x.Cantidadhoras)
                });

            var total = await groupedQuery.CountAsync();

            var minYear = await db.TblTimeReportActividadDiaria.Where(a => a.Activo).MinAsync(a => (int?)a.Fechaactividad.Year);
            var maxYear = await db.TblTimeReportActividadDiaria.Where(a => a.Activo).MaxAsync(a => (int?)a.Fechaactividad.Year);

            var dbResult = await groupedQuery
                .OrderBy(g => g.Anio).ThenBy(g => g.MesNum).ThenBy(g => g.Cliente)
                .Skip((filtro.Page - 1) * filtro.PageSize)
                .Take(filtro.PageSize)
                .ToListAsync();

            var meses = new string[] { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };
            
            var resultado = dbResult.Select(r => new ReporteHorasResponse(
                Guid.NewGuid().ToString(),
                r.Cliente ?? "Sin Cliente",
                r.Recursos,
                r.Horas,
                r.MesNum >= 1 && r.MesNum <= 12 ? meses[r.MesNum] : "Desconocido",
                r.Anio,
                r.ClienteActivo.HasValue ? (r.ClienteActivo.Value ? "Activo" : "Inactivo") : "Sin Cliente"
            )).ToList();

            return Results.Ok(new PaginatedResponse<ReporteHorasResponse>(resultado, total, minYear, maxYear));
        });

        // 2. Reporte por Fechas
        group.MapGet("/fechas", async (
            [AsParameters] ReporteFechasFiltroRequest filtro,
            ApplicationDbContext db) =>
        {
            var query = db.TblTimeReportActividadDiaria
                .Include(a => a.IdempleadoNavigation).ThenInclude(e => e.IdpersonaNavigation)
                .Include(a => a.IdempleadoNavigation).ThenInclude(e => e.IdcargoNavigation)
                .Include(a => a.IdproyectoNavigation!).ThenInclude(p => p!.IdclienteNavigation)
                .Where(a => a.Activo);

            if (filtro.FechaInicio.HasValue)
            {
                var inicioOnly = DateOnly.FromDateTime(filtro.FechaInicio.Value);
                query = query.Where(a => a.Fechaactividad >= inicioOnly);
            }

            if (filtro.FechaFin.HasValue)
            {
                var finOnly = DateOnly.FromDateTime(filtro.FechaFin.Value);
                query = query.Where(a => a.Fechaactividad <= finOnly);
            }

            if (!string.IsNullOrWhiteSpace(filtro.Cliente))
                query = query.Where(a => a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null && a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial != null && a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial.ToLower().Contains(filtro.Cliente.ToLower()));

            if (!string.IsNullOrWhiteSpace(filtro.Lider))
            {
                var term = filtro.Lider.Trim().ToLower();
                                query = query.Where(a => a.IdproyectoNavigation != null && a.IdproyectoNavigation.TblTimeReportAsignacionProyectos.Any(ep => 
                                                                                ep.Activo && ep.Idlider != null && ep.IdliderNavigation != null && ep.IdliderNavigation.IdpersonaNavigation != null &&
                                                                                ((ep.IdliderNavigation.IdpersonaNavigation.Nombres.ToLower() + " " + 
                                                                                    ep.IdliderNavigation.IdpersonaNavigation.Apellidos.ToLower()).Contains(term) ||
                                                                                 (ep.IdliderNavigation.IdpersonaNavigation.Apellidos.ToLower() + " " + 
                                                                                    ep.IdliderNavigation.IdpersonaNavigation.Nombres.ToLower()).Contains(term))));
            }

            var total = await query.CountAsync();

            var resultado = await query
                .OrderByDescending(a => a.Fechaactividad)
                .Skip((filtro.Page - 1) * filtro.PageSize)
                .Take(filtro.PageSize)
                .Select(a => new ReporteFechasResponse(
                    a.Id.ToString(),
                    a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdclienteNavigation != null ? (a.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial ?? "Sin Cliente") : "Sin Cliente",
                    a.IdproyectoNavigation != null
                        ? (a.IdproyectoNavigation.TblTimeReportAsignacionProyectos
                            .Where(ep => ep.Activo && ep.Idlider != null && ep.IdliderNavigation != null && ep.IdliderNavigation.IdpersonaNavigation != null)
                            .Select(ep => ep.IdliderNavigation.IdpersonaNavigation.Nombres + " " + ep.IdliderNavigation.IdpersonaNavigation.Apellidos)
                            .FirstOrDefault() ?? "Sin Lider")
                        : "Sin Lider",
                    a.IdempleadoNavigation.IdpersonaNavigation.Nombres + " " + a.IdempleadoNavigation.IdpersonaNavigation.Apellidos,
                    a.IdempleadoNavigation.IdcargoNavigation != null ? a.IdempleadoNavigation.IdcargoNavigation.Nombrecargo : "Sin Cargo",
                    new DateTime(a.Fechaactividad.Year, a.Fechaactividad.Month, a.Fechaactividad.Day),
                    new DateTime(a.Fechaactividad.Year, a.Fechaactividad.Month, a.Fechaactividad.Day),
                    a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Codigo : null,
                    a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Nombre : null,
                    a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdestadoproyectoNavigation != null ? a.IdproyectoNavigation.IdestadoproyectoNavigation.Valor : null,
                    a.IdproyectoNavigation != null && a.IdproyectoNavigation.IdtipoproyectoNavigation != null ? a.IdproyectoNavigation.IdtipoproyectoNavigation.Nombretipo : null,
                    a.IdproyectoNavigation != null && a.IdproyectoNavigation.Fechafinreal != null ? new DateTime(a.IdproyectoNavigation.Fechafinreal.Value.Year, a.IdproyectoNavigation.Fechafinreal.Value.Month, a.IdproyectoNavigation.Fechafinreal.Value.Day) : null,
                    a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Presupuesto : null,
                    a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Horasasignadas : null,
                    a.IdproyectoNavigation != null && a.IdproyectoNavigation.Fechainicioespera != null ? new DateTime(a.IdproyectoNavigation.Fechainicioespera.Value.Year, a.IdproyectoNavigation.Fechainicioespera.Value.Month, a.IdproyectoNavigation.Fechainicioespera.Value.Day) : null,
                    a.IdproyectoNavigation != null && a.IdproyectoNavigation.Fechafinespera != null ? new DateTime(a.IdproyectoNavigation.Fechafinespera.Value.Year, a.IdproyectoNavigation.Fechafinespera.Value.Month, a.IdproyectoNavigation.Fechafinespera.Value.Day) : null,
                    a.IdproyectoNavigation != null ? a.IdproyectoNavigation.Observacion : null
                ))
                .ToListAsync();

            return Results.Ok(new PaginatedResponse<ReporteFechasResponse>(resultado, total));
        });

        // =========================================================================
        // INTEGRACIÓN CON TMR-DOCUMENT-SERVICE (Microservicio de Motor de Plantillas)
        // =========================================================================

        // 3. Generar Documento (Proxy)
        group.MapPost("/documentos/generar", async (
            HttpRequest request,
            System.Net.Http.IHttpClientFactory clientFactory,
            Microsoft.Extensions.Configuration.IConfiguration config) =>
        {
            try
            {
                var docServiceUrl = config["DocumentServiceUrl"] ?? "http://localhost:3001";
                var client = clientFactory.CreateClient();
                
                // Lee el JSON completo que envía el frontend (templateName, data, format)
                using var streamReader = new System.IO.StreamReader(request.Body);
                var bodyJson = await streamReader.ReadToEndAsync();
                
                var content = new System.Net.Http.StringContent(bodyJson, System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"{docServiceUrl}/api/reports/generate", content);
                
                var responseBody = await response.Content.ReadAsStringAsync();
                return Results.Content(responseBody, "application/json", statusCode: (int)response.StatusCode);
            }
            catch (System.Exception ex)
            {
                return Results.Problem($"Error al conectar con el motor de plantillas: {ex.Message}");
            }
        });

        // 4. Consultar Estado (Proxy)
        group.MapGet("/documentos/estado/{jobId}", async (
            string jobId,
            System.Net.Http.IHttpClientFactory clientFactory,
            Microsoft.Extensions.Configuration.IConfiguration config) =>
        {
            try
            {
                var docServiceUrl = config["DocumentServiceUrl"] ?? "http://localhost:3001";
                var client = clientFactory.CreateClient();
                
                var response = await client.GetAsync($"{docServiceUrl}/api/reports/status/{jobId}");
                var responseBody = await response.Content.ReadAsStringAsync();
                
                return Results.Content(responseBody, "application/json", statusCode: (int)response.StatusCode);
            }
            catch (System.Exception ex)
            {
                return Results.Problem($"Error al consultar el estado: {ex.Message}");
            }
        });

        // 5. Descargar Documento (Proxy)
        group.MapGet("/documentos/descargar/{filename}", async (
            string filename,
            System.Net.Http.IHttpClientFactory clientFactory,
            Microsoft.Extensions.Configuration.IConfiguration config) =>
        {
            try
            {
                var docServiceUrl = config["DocumentServiceUrl"] ?? "http://localhost:3001";
                var client = clientFactory.CreateClient();
                
                var response = await client.GetAsync($"{docServiceUrl}/api/reports/download/{filename}");
                
                if (response.IsSuccessStatusCode)
                {
                    var stream = await response.Content.ReadAsStreamAsync();
                    var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                    return Results.File(stream, contentType, filename);
                }
                
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (System.Exception ex)
            {
                return Results.Problem($"Error al descargar el documento: {ex.Message}");
            }
        });
    }
}
