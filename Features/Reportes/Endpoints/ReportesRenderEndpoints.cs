using System.Security.Claims;
using FluentValidation;
using tmr_backend.Features.Reportes.Carbone;
using tmr_backend.Features.Reportes.DTOs;
using tmr_backend.Features.Reportes.Services;
using tmr_backend.Features.TimeReport;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Reportes;

// Generación de documentos a partir de plantillas (Carbone). El frontend solo habla con esta API;
// tmr-carbone-service no se expone fuera de la red interna.
public static class ReportesRenderEndpoints
{
    public static void MapReportesRenderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reportes")
            .WithTags("Reportes - Plantillas")
            .RequireAuthorization();

        // Reporte de seguimiento del propio colaborador. Igual que "mi-reporte", no usa SEGUIMIENTO_READ:
        // el empleado sale de la sesión, nunca del cuerpo, así nadie puede pedir el reporte de otra persona.
        group.MapPost("/seguimiento/render", async (
            RenderSeguimientoRequest request,
            IValidator<RenderSeguimientoRequest> validator,
            ClaimsPrincipal user,
            ApplicationDbContext db,
            ISeguimientoReporteService servicio,
            CancellationToken ct) =>
        {
            var validacion = await validator.ValidateAsync(request, ct);
            if (!validacion.IsValid) return Results.ValidationProblem(validacion.ToDictionary());

            var idEmpleado = await TimeReportEndpoints.ObtenerEmpleadoSesionAsync(user, db);
            if (idEmpleado is null) return Results.Unauthorized();

            try
            {
                var archivo = await servicio.GenerarAsync(idEmpleado.Value, request, ct);
                return archivo is null
                    ? Results.Problem(
                        statusCode: StatusCodes.Status404NotFound,
                        title: "Sin actividades",
                        detail: "No hay actividades registradas para el periodo seleccionado.")
                    : Results.File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
            }
            catch (CarboneException ex)
            {
                // El detalle técnico ya quedó en el log del cliente; al usuario solo el mensaje seguro.
                return Results.Problem(statusCode: ex.StatusCode, title: "No se pudo generar el reporte", detail: ex.Message);
            }
        });
    }
}
