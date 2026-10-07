using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace tmr_backend.Features.Notificaciones.Presentation.Endpoint;

public static class NotificacionesEndpoints
{
    public static void MapNotificacionesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notificaciones").WithTags("Notificaciones");

        // GET /pendientes - Consulta los empleados que deben horas
        group.MapGet("/pendientes", async () => 
        {
            // TODO: Lógica para obtener pendientes delegada al handler de detección de Allan
            return Results.Ok(new { Mensaje = "Lista de empleados recuperada (Mock)", Datos = new[] { 1, 2, 3 } });
        })
        .RequireAuthorization("NOTIFICACIONES_EJECUTAR")
        .WithName("GetPendientesNotificacion")
        .WithSummary("Obtiene la lista de empleados que deben horas en el período en curso");

        // POST /notificar - Envía notificaciones a IDs específicos
        group.MapPost("/notificar", async ([FromBody] NotificarRequest request) =>
        {
            if (request.EmpleadoIds == null || !request.EmpleadoIds.Any())
                return Results.BadRequest(new { Mensaje = "Debe seleccionar al menos un empleado para notificar." });

            // TODO: Llamar al orquestador principal (NotificacionHorasService) pasando los IDs
            return Results.Ok(new { Mensaje = "Notificaciones enviadas exitosamente", Enviados = request.EmpleadoIds.Count });
        })
        .RequireAuthorization("NOTIFICACIONES_EJECUTAR")
        .WithName("NotificarEmpleadosSeleccionados")
        .WithSummary("El Líder Técnico fuerza el envío del correo de notificación a los empleados indicados");
    }
}

public class NotificarRequest
{
    public List<int> EmpleadoIds { get; set; } = new();
}
