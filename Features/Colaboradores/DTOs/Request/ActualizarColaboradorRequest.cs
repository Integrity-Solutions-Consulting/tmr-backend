namespace tmr_backend.Features.Colaboradores.DTOs.Request;

// DTO para EDITAR un colaborador.
// Los datos personales NO se editan aquí (la persona se gestiona en su módulo).
// Solo se modifican los datos laborales y el estado.
public record ActualizarColaboradorRequest(
    // ── Contrato ──────────────────────────────────────
    int IdTipoContrato,
    bool Activo,             // Estado: Activo / Inactivo

    // ── Datos laborales ───────────────────────────────
    int IdDepartamento,
    DateOnly? FechaIngreso,
    int IdCargo,
    int? AniosExperiencia,
    int IdModoTrabajo,
    int IdCategoriaEmpleado
);