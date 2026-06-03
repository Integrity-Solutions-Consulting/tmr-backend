namespace tmr_backend.Features.Clientes.DTOs.Response;

// DTO de cada proyecto que aparece en el modal de detalle del cliente.
public record ProyectoAsignadoResponse(
    int Id,
    string Nombre,
    string Cliente,   // Nombre comercial del cliente (para mostrar bajo el proyecto)
    string Estado     // Valor del catálogo de estado de proyecto
);