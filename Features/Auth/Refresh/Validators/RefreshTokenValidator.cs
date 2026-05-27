using FluentValidation;
using tmr_backend.Features.Auth.Refresh.DTOs;

namespace tmr_backend.Features.Auth.Refresh.Validators;

/// <summary>
/// Validador de RefreshTokenRequest.
/// </summary>
public class RefreshTokenValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .WithMessage("El refresh token es requerido.");
    }
}
