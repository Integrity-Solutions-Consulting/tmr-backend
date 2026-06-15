namespace tmr_backend.Features.Colaboradores.DTOs.Request;

// DTO para CREAR un colaborador.
// 1. Se ingresan los datos de la persona desde el modal.
// 2. El backend crea primero la Persona.
// 3. Luego crea el Empleado usando el IdPersona generado.
public record CrearColaboradorRequest(
    // ── Datos de persona ──────────────────────────────
    string NumeroIdentificacion,
    int? IdTipoIdentificacion,
    string TipoPersona,          // NATURAL / JURIDICA
    int? IdGenero,
    int? IdNacionalidad,
    string Nombres,
    string Apellidos,
    DateOnly? FechaNacimiento,

    // ── Datos de contacto ─────────────────────────────
    string? Email,
    string? Telefono,
    string? Direccion,

    // ── Contrato ──────────────────────────────────────
    int IdEmpresaCatalogo,       // Empresa: RPS / ISC / RPS E ISC
    int IdTipoContrato,          // Tipo de contrato

    // ── Datos laborales ───────────────────────────────
    int IdDepartamento,
    DateOnly? FechaIngreso,
    int IdCargo,
    int? AniosExperiencia,
    int IdModoTrabajo,
    int IdCategoriaEmpleado
);