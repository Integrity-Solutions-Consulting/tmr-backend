namespace tmr_backend.Features.Clientes.DTOs.Response;

// DTO de salida para la TABLA de clientes (vista de lista).
// Columnas del frontend: Tipo Id, Identificador, Nombre Comercial, Correo, Teléfono, Estado.
public record ClienteListaResponse(
    int Id,
    string TipoIdentificacion,   // Valor del catálogo TID (RUC / Cédula / Pasaporte)
    string NumeroIdentificacion,
    string NombreComercial,
    string Email,
    string Telefono,
    bool Activo                  // Estado
);