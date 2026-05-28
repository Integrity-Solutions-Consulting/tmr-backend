using FluentValidation;
using tmr_backend.Features.Auth.Login.DTOs;

namespace tmr_backend.Features.Auth.Login.Validators;

/// <summary>
/// Validador de LoginRequest usando FluentValidation.
/// Valida email y password según políticas básicas.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El email es requerido.")
            .EmailAddress()
            .WithMessage("El email debe ser válido.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("La contraseña es requerida.")
            .MinimumLength(6)
            .WithMessage("La contraseña debe tener al menos 6 caracteres.");
    }
}
