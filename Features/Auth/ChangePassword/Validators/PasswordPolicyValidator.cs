namespace tmr_backend.Features.Auth.ChangePassword.Validators;

using FluentValidation;
using tmr_backend.Features.Auth.ChangePassword.DTOs;

/// <summary>
/// Validador de política de contraseña.
/// </summary>
public class PasswordPolicyValidator : AbstractValidator<ChangePasswordRequest>
{
    public PasswordPolicyValidator()
    {
        RuleFor(x => x.OldPassword)
            .NotEmpty()
            .WithMessage("La contraseña actual es requerida.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("La nueva contraseña es requerida.")
            .MinimumLength(8)
            .WithMessage("La contraseña debe tener al menos 8 caracteres.")
            .Matches(@"[A-Z]")
            .WithMessage("La contraseña debe contener al menos una mayúscula.")
            .Matches(@"[a-z]")
            .WithMessage("La contraseña debe contener al menos una minúscula.")
            .Matches(@"[0-9]")
            .WithMessage("La contraseña debe contener al menos un dígito.")
            .Matches(@"[!@#$%^&*()_+\-=\[\]{};:'"",.<>?/\\|`~]")
            .WithMessage("La contraseña debe contener al menos un símbolo especial.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Debe confirmar la contraseña.")
            .Equal(x => x.NewPassword)
            .WithMessage("Las contraseñas no coinciden.");
    }
}
