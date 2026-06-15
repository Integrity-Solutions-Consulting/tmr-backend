namespace tmr_backend.Features.Colaboradores.DTOs.Request;

// DTO para EDITAR un colaborador.
// Los datos personales NO se editan aquí (la persona se gestiona en su módulo).
// Solo se modifican los datos laborales y el estado.
public record ActualizarColaboradorRequest(
    string? TipoPersona,
    int? IdTipoIdentificacion,
    string? NumeroIdentificacion,
    string? Nombres,
    string? Apellidos,
    DateOnly? FechaNacimiento,
    int? IdGenero,
    int? IdNacionalidad,

    string? Email,
    string? Telefono,
    string? Direccion,

    int? IdEmpresaCatalogo,
    int IdTipoContrato,
    bool Activo,

    int IdDepartamento,
    DateOnly? FechaIngreso,
    int IdCargo,
    int? AniosExperiencia,
    int IdModoTrabajo,
    int IdCategoriaEmpleado
);

