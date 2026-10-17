using FluentValidation;
using tmr_backend.Features.Colaboradores.DTOs.Request;

namespace tmr_backend.Features.Colaboradores.Validators;

public class RegistrarSalidaRequestValidator : AbstractValidator<RegistrarSalidaRequest>
{
    public RegistrarSalidaRequestValidator()
    {
        RuleFor(x => x.FechaSalida)
            .NotNull()
            .WithMessage("La fecha de salida es requerida.");

        RuleFor(x => x.IdTipoSalida)
            .GreaterThan(0)
            .WithMessage("El tipo de salida es requerido.");

        RuleFor(x => x.IdCausaSalida)
            .GreaterThan(0)
            .WithMessage("La causa de salida es requerida.");

        RuleFor(x => x.Comentario)
            .MaximumLength(500)
            .WithMessage("El comentario no puede exceder los 500 caracteres.");

        RuleFor(x => x.IdEmpleadoReemplazo)
            .GreaterThan(0)
            .When(x => x.IdEmpleadoReemplazo.HasValue)
            .WithMessage("El ID del reemplazo debe ser válido.");
    }
}