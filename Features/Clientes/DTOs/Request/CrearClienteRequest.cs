namespace tmr_backend.Features.Clientes.DTOs.Request;

// DTO para CREAR un cliente.
// El cliente es autónomo: todos los datos viven en tbl_administracion_cliente.
// El único combo del formulario de crear es Tipo de Identificación (catálogo TID).
public record CrearClienteRequest(
    // ── Información General ──────────────────────────────
    int IdTipoIdentificacion,   // Combo TID (RUC / Cédula / Pasaporte)
    string NumeroIdentificacion,
    string NombreComercial,

    // ── Datos de contacto ────────────────────────────────
    string Nombres,
    string Apellidos,
    string Email,
    string Telefono,
    string Direccion
);