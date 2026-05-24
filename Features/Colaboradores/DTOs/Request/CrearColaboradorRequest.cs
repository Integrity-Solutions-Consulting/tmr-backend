namespace tmr_backend.Features.Colaboradores.DTOs.Request;

// DTO para CREAR un colaborador.
// Como la persona se selecciona del ComboBox (ya existe en la base),
public record CrearColaboradorRequest(
    // ── Persona seleccionada del ComboBox ──
    int IdPersona,

    // ── Contrato ──────────────────────────────────────
    int IdEmpresaCatalogo,   // Asociación: RPS / ISC / RPS E ISC
    int IdTipoContrato,      // Tipo de contrato

    // ── Datos laborales ───────────────────────────────
    int IdDepartamento,
    DateOnly? FechaIngreso,
    int IdCargo,
    int? AniosExperiencia,
    int IdModoTrabajo,
    int IdCategoriaEmpleado
);