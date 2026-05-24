using FluentValidation;
using tmr_backend.Features.Lideres.DTOs.Request;

namespace tmr_backend.Features.Lideres.Validators;

public class CrearLiderValidator : AbstractValidator<CrearLiderRequest>
{
    public CrearLiderValidator()
    {
        RuleFor(x => x.Nombres)
            .NotEmpty().WithMessage("El nombre es requerido.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder 100 caracteres.");

        RuleFor(x => x.Apellidos)
            .NotEmpty().WithMessage("Los apellidos son requeridos.")
            .MaximumLength(100).WithMessage("Los apellidos no pueden exceder 100 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.");

        RuleFor(x => x.Telefono)
            .MaximumLength(20).WithMessage("El teléfono no puede exceder 20 caracteres.");

        RuleFor(x => x.Tipopersona)
            .NotEmpty().WithMessage("El tipo de persona es requerido.");

        RuleFor(x => x.Idtipo)
            .GreaterThan(0).WithMessage("El tipo de líder es requerido.");
    }
}