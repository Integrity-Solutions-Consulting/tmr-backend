using FluentValidation;
using tmr_backend.Features.Auth.DTOs.Request;

namespace tmr_backend.Features.Auth.Validators;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.User)
            .NotEmpty().WithMessage("El user es requerido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("El password es requerido.");
    }
}
