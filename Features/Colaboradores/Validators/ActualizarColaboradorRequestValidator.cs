using FluentValidation;
using tmr_backend.Features.Colaboradores.DTOs.Request;

namespace tmr_backend.Features.Colaboradores.Validators;

// Validador para editar colaboradores.
// Solo valida datos laborales (los personales no se editan aquí).
public class ActualizarColaboradorRequestValidator : AbstractValidator<ActualizarColaboradorRequest>
{
    public ActualizarColaboradorRequestValidator()
    {
        RuleFor(x => x.IdTipoContrato)
            .GreaterThan(0).WithMessage("El tipo de contrato es requerido.");

        RuleFor(x => x.IdDepartamento)
            .GreaterThan(0).WithMessage("El departamento es requerido.");

        RuleFor(x => x.IdCargo)
            .GreaterThan(0).WithMessage("El cargo es requerido.");

        RuleFor(x => x.IdModoTrabajo)
            .GreaterThan(0)
            .When(x => x.IdModoTrabajo.HasValue)
            .WithMessage("La modalidad seleccionada no es válida.");

        RuleFor(x => x.IdCategoriaEmpleado)
            .GreaterThan(0)
            .When(x => x.IdCategoriaEmpleado.HasValue)
            .WithMessage("La categoría seleccionada no es válida.");

        // ================================================================
        // NUEVO: Validar reemplazo (si se envía, debe existir)
        // ================================================================
        RuleFor(x => x.IdEmpleadoReemplazo)
            .GreaterThan(0)
            .When(x => x.IdEmpleadoReemplazo.HasValue)
            .WithMessage("El colaborador de reemplazo no es válido.");
    }
}