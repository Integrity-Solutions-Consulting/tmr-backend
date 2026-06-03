namespace tmr_backend.Features.Clientes.DTOs.Response;

// DTO de salida para el MODAL DE DETALLE de un cliente.
// Incluye datos de contacto + la lista de proyectos asignados.
public record ClienteDetalleResponse(
    int Id,
    string TipoIdentificacion,
    string NumeroIdentificacion,
    string NombreComercial,
    bool Activo,

    // ── Datos de contacto ────────────────────────────────
    string Nombres,
    string Apellidos,
    string Email,
    string Telefono,
    string Direccion,

    // ── Proyectos asignados ──────────────────────────────
    List<ProyectoAsignadoResponse> Proyectos
);