namespace tmr_backend.Features.Colaboradores.DTOs.Response;

// DTO de salida para la TABLA de colaboradores (vista de lista).
// Contiene solo lo que se muestra en las columnas:
// Identificación, Tipo, Nombre, Núm. Proyectos, Correo, Cargo, Estado.
public record ColaboradorListaResponse(
    int Id,
    int IdPersona,
    string CodigoEmpleado,        // RPS0001
    string NumeroIdentificacion,
    string Asociacion,            // Valor del catálogo EMP (RPS / ISC / RPS E ISC)
    string NombreCompleto,        // Nombres + Apellidos
    string Email,
    string Cargo,                 // NombreCargo
    int NumProyectos,             // Conteo de proyectos activos
    bool Activo                   // Estado
);