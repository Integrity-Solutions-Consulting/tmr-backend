using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc; // OBLIGATORIO PARA EL [FromForm]
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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

        // 1. GET: Obtener todas las actividades activas
        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var actividades = await db.Actividades
                .Where(c => c.Activo)
                .Select(c => new ActividadResponse(c.Id, c.Nombre, c.Descripcion, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(actividades);
        });

        // 2. GET: Obtener una actividad por ID
        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var actividad = await db.Actividades.FindAsync(id);

            if (actividad is null) return Results.NotFound();

            return Results.Ok(new ActividadResponse(actividad.Id, actividad.Nombre, actividad.Descripcion, actividad.Activo, actividad.FechaCreacion));
        });

        // 3. POST: Crear una actividad individual manual
        group.MapPost("/", async (CrearActividadRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevaActividad = Actividad.Crear(request.Nombre, request.Descripcion);
                
                db.Actividades.Add(nuevaActividad);
                await db.SaveChangesAsync();

                var response = new ActividadResponse(nuevaActividad.Id, nuevaActividad.Nombre, nuevaActividad.Descripcion, nuevaActividad.Activo, nuevaActividad.FechaCreacion);
                return Results.Created($"/api/carga-actividades/{nuevaActividad.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        // 4. PUT: Actualizar una actividad existente
        group.MapPut("/{id:guid}", async (Guid id, ActualizarActividadRequest request, ApplicationDbContext db) =>
        {
            var actividad = await db.Actividades.FindAsync(id);

            if (actividad is null) return Results.NotFound();

            try
            {
                actividad.ActualizarDetalles(request.Nombre, request.Descripcion);
                await db.SaveChangesAsync();

                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        // 5. DELETE: Desactivar una actividad
        group.MapDelete("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var actividad = await db.Actividades.FindAsync(id);

            if (actividad is null) return Results.NotFound();

            actividad.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
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