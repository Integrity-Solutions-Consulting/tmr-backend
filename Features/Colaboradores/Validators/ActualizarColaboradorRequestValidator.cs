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
            .GreaterThan(0).WithMessage("La modalidad es requerida.");

        RuleFor(x => x.IdCategoriaEmpleado)
            .GreaterThan(0).WithMessage("La categoría es requerida.");
    }
}

