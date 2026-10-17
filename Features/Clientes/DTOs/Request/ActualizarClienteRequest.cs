namespace tmr_backend.Features.Clientes.DTOs.Request;

// DTO para ACTUALIZAR un cliente.
// Igual que crear, pero añade el Estado (combo del modal Editar).
public record ActualizarClienteRequest(
    // ── Información General ──────────────────────────────
    bool Activo,                // Combo Estado (Activo / Inactivo)
    int IdTipoIdentificacion,   // Combo TID
    string NumeroIdentificacion,
    string NombreComercial,

    // ── Datos de contacto ────────────────────────────────
    string Nombres,
    string Apellidos,
    string Email,
    string Telefono,
    string Direccion
);