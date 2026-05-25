using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; // OBLIGATORIO PARA EL [FromForm]
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
                    Fechaaprobacion = c.Fechaaprobacion.HasValue ? c.Fechaaprobacion.Value.ToString("yyyy-MM-dd HH:mm:ss") : null,
                    c.Activo,
                    c.Usuariocreacion,
                    Fechacreacion = c.Fechacreacion.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            await using var stream = new MemoryStream();
            await stream.SaveAsAsync(actividades);
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