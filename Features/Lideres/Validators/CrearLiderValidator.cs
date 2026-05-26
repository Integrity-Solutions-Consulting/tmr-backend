using FluentValidation;
using tmr_backend.Features.Lideres.DTOs.Request;

namespace tmr_backend.Features.Lideres.Validators;

public class CrearLiderValidator : AbstractValidator<CrearLiderRequest>
{
    public CrearLiderValidator()
    {
        RuleFor(x => x.Idpersona)
            .GreaterThan(0).WithMessage("La persona es requerida.");

        RuleFor(x => x.Idtipo)
            .GreaterThan(0).WithMessage("El tipo de líder es requerido.");

        RuleFor(x => x.Usuariocreacion)
            .NotEmpty().WithMessage("El usuario de creación es requerido.");

        RuleFor(x => x.Ipcreacion)
            .NotEmpty().WithMessage("La IP de creación es requerida.");
    }
}