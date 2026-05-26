using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; // OBLIGATORIO PARA EL [FromForm]
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using MiniExcelLibs;
using System.IO;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using tmr_backend.Features.CargaActividades.Domain;
using tmr_backend.Features.CargaActividades.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.CargaActividades;

public static class CargaActividadesEndpoints
{
    public static void MapCargaActividadesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/carga-actividades").WithTags("CargaActividades");

        // 1. GET: Obtener todas las actividades activas en el esquema real de time_report
        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var actividades = await db.TblTimeReportActividadDiaria
                .Where(c => c.Activo)
                .Select(c => new
                {
                    c.Id,
                    c.Idempleado,
                    c.Idproyecto,
                    c.Idtipoactividad,
                    c.Codigorequerimiento,
                    c.Cantidadhoras,
                    Fechaactividad = c.Fechaactividad.ToString("yyyy-MM-dd"),
                    c.Descripcionactividad,
                    c.Notas,
                    c.Esbillable,
                    c.Aprobadopor,
                    c.Fechaaprobacion,
                    c.Activo,
                    c.Usuariocreacion,
                    c.Fechacreacion
                })
                .ToListAsync();

            return Results.Ok(actividades);
        });

        // 2. GET: Obtener una actividad por ID
        group.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
        {
            var actividad = await db.TblTimeReportActividadDiaria.FindAsync(id);

            if (actividad is null) return Results.NotFound();

            return Results.Ok(new
            {
                actividad.Id,
                actividad.Idempleado,
                actividad.Idproyecto,
                actividad.Idtipoactividad,
                actividad.Codigorequerimiento,
                actividad.Cantidadhoras,
                Fechaactividad = actividad.Fechaactividad.ToString("yyyy-MM-dd"),
                actividad.Descripcionactividad,
                actividad.Notas,
                actividad.Esbillable,
                actividad.Aprobadopor,
                actividad.Fechaaprobacion,
                actividad.Activo,
                actividad.Usuariocreacion,
                actividad.Fechacreacion
            });
        });

        // 3. POST: Crear una actividad individual manual (legacy no soportado en el nuevo esquema)
        group.MapPost("/", () => Results.StatusCode(501));

        // 4. PUT: Actualizar una actividad existente (legacy no soportado en el nuevo esquema)
        group.MapPut("/{id:int}", () => Results.StatusCode(501));

        // 5. DELETE: Desactivar una actividad (legacy no soportado en el nuevo esquema)
        group.MapDelete("/{id:int}", () => Results.StatusCode(501));

        // 6. GET: Descargar actividades activas como archivo Excel
        group.MapGet("/download", async (ApplicationDbContext db) =>
        {
            var actividades = await db.TblTimeReportActividadDiaria
                .Where(c => c.Activo)
                .Select(c => new
                {
                    Fecha = c.Fechaactividad.ToString("yyyy-MM-dd"),
                    Colaborador = c.IdempleadoNavigation.IdpersonaNavigation != null
                        ? (c.IdempleadoNavigation.IdpersonaNavigation.Nombres + " " + c.IdempleadoNavigation.IdpersonaNavigation.Apellidos).Trim()
                        : c.IdempleadoNavigation.Codigoempleado,
                    TipoActividad = c.IdtipoactividadNavigation.Nombretipo,
                    Proyecto = c.IdproyectoNavigation != null
                        ? c.IdproyectoNavigation.Nombre
                        : "Sin proyecto",
                    Cliente = c.IdproyectoNavigation != null && c.IdproyectoNavigation.IdclienteNavigation != null
                        ? c.IdproyectoNavigation.IdclienteNavigation.Razonsocial ?? c.IdproyectoNavigation.IdclienteNavigation.Nombrecomercial ?? "Cliente desconocido"
                        : "Sin cliente",
                    LiderTecnico = c.IdproyectoNavigation != null && c.IdproyectoNavigation.IdliderNavigation != null && c.IdproyectoNavigation.IdliderNavigation.IdpersonaNavigation != null
                        ? (c.IdproyectoNavigation.IdliderNavigation.IdpersonaNavigation.Nombres + " " + c.IdproyectoNavigation.IdliderNavigation.IdpersonaNavigation.Apellidos).Trim()
                        : "Sin líder",
                    c.Codigorequerimiento,
                    Horas = c.Cantidadhoras,
                    Descripción = c.Descripcionactividad,
                    Notas = c.Notas,
                    EsBillable = c.Esbillable.HasValue ? (c.Esbillable.Value ? "Sí" : "No") : "No definido",
                    AprobadoPor = c.AprobadoporNavigation != null && c.AprobadoporNavigation.IdpersonaNavigation != null
                        ? (c.AprobadoporNavigation.IdpersonaNavigation.Nombres + " " + c.AprobadoporNavigation.IdpersonaNavigation.Apellidos).Trim()
                        : "No aprobado",
                    FechaAprobacion = c.Fechaaprobacion.HasValue ? c.Fechaaprobacion.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                    UsuarioCreacion = c.Usuariocreacion,
                    FechaCreacion = c.Fechacreacion.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Actividades");
            worksheet.Cell(1, 1).Value = "Fecha";
            worksheet.Cell(1, 2).Value = "Colaborador";
            worksheet.Cell(1, 3).Value = "TipoActividad";
            worksheet.Cell(1, 4).Value = "Proyecto";
            worksheet.Cell(1, 5).Value = "Cliente";
            worksheet.Cell(1, 6).Value = "LiderTecnico";
            worksheet.Cell(1, 7).Value = "Codigorequerimiento";
            worksheet.Cell(1, 8).Value = "Horas";
            worksheet.Cell(1, 9).Value = "Descripción";
            worksheet.Cell(1, 10).Value = "Notas";
            worksheet.Cell(1, 11).Value = "EsBillable";
            worksheet.Cell(1, 12).Value = "AprobadoPor";
            worksheet.Cell(1, 13).Value = "FechaAprobacion";
            worksheet.Cell(1, 14).Value = "UsuarioCreacion";
            worksheet.Cell(1, 15).Value = "FechaCreacion";

            for (var i = 0; i < actividades.Count; i++)
            {
                var fila = actividades[i];
                var row = i + 2;
                worksheet.Cell(row, 1).Value = fila.Fecha;
                worksheet.Cell(row, 2).Value = fila.Colaborador;
                worksheet.Cell(row, 3).Value = fila.TipoActividad;
                worksheet.Cell(row, 4).Value = fila.Proyecto;
                worksheet.Cell(row, 5).Value = fila.Cliente;
                worksheet.Cell(row, 6).Value = fila.LiderTecnico;
                worksheet.Cell(row, 7).Value = fila.Codigorequerimiento;
                worksheet.Cell(row, 8).Value = fila.Horas;
                worksheet.Cell(row, 9).Value = fila.Descripción;
                worksheet.Cell(row, 10).Value = fila.Notas;
                worksheet.Cell(row, 11).Value = fila.EsBillable;
                worksheet.Cell(row, 12).Value = fila.AprobadoPor;
                worksheet.Cell(row, 13).Value = fila.FechaAprobacion;
                worksheet.Cell(row, 14).Value = fila.UsuarioCreacion;
                worksheet.Cell(row, 15).Value = fila.FechaCreacion;
            }

            worksheet.Columns().AdjustToContents();

            await using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return Results.File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "carga-actividades.xlsx");
        });

        // =============================================================================
        // NUEVA FUNCIONALIDAD: Carga Masiva de Actividades desde Planilla Excel
        // RUTA FINAL: POST /api/carga-actividades/excel
        // =============================================================================
        group.MapPost("/excel", async ([FromForm] IFormFile file, HttpContext context, ICargarActividadesExcelHandler handler) =>
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Results.BadRequest(CargaActividadesResponse.Failure("El archivo Excel no fue proporcionado o está vacío."));
                }

                // REGLA DE SEGURIDAD EN DESARROLLO: Forzamos ID de prueba local para usar Scalar sin Token JWT
                var colaboradorId = "00000000-0000-0000-0000-000000000000";

                // NOTA: Cuando vayas a pasar a producción con la seguridad de la empresa, descomenta la línea de abajo:
                // var colaboradorId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(colaboradorId))
                {
                    return Results.Json(CargaActividadesResponse.Failure("Token de sesión inválido o expirado."), statusCode: 401);
                }

                var command = new CargarActividadesExcelCommand(file, colaboradorId);
                var response = await handler.HandleAsync(command);

                if (response.IsSuccess)
                {
                    // MODIFICACIÓN DE RETORNO: Aseguramos que si 'response' contiene la lista expuesta, 
                    // o si deseas mapearla dinámicamente, se envíe de forma consistente al cliente HTTP.
                    return Results.Ok(response);
                }

                return Results.BadRequest(response);
            }
            catch (Exception ex)
            {
                // Diagnóstico en tiempo de desarrollo para atrapar errores de casteo o MiniExcel
                return Results.Json(new 
                { 
                    isSuccess = false, 
                    message = "Error interno atrapado en el Endpoint al procesar el archivo.", 
                    detallesError = ex.Message,
                    origen = ex.TargetSite?.Name,
                    pilaSeguimiento = ex.StackTrace 
                }, statusCode: 500);
            }
        })
        .WithName("CargarActividadesExcel")
        .DisableAntiforgery(); // Desactiva la protección automática de formularios de .NET 10
    }
}