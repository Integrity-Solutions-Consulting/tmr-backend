// Features/Auth/Validators/RegisterRequestValidator.cs
using FluentValidation;
using tmr_backend.Features.Auth.DTOs.Request;

namespace tmr_backend.Features.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido.")
            .EmailAddress().WithMessage("El email no tiene un formato válido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("El password es requerido.")
            .MinimumLength(8).WithMessage("Mínimo 8 caracteres.")
            .Matches("[A-Z]").WithMessage("Debe tener al menos una mayúscula.")
            .Matches("[0-9]").WithMessage("Debe tener al menos un número.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("La confirmación de contraseña es requerida.")
            .Equal(x => x.Password).WithMessage("Las contraseñas no coinciden.");
    }

}