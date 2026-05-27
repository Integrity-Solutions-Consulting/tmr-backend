namespace tmr_backend.Features.Auth.Logout.Validators;

using FluentValidation;
using tmr_backend.Features.Auth.Logout.DTOs;

/// <summary>
/// Validador para LogoutRequest.
/// </summary>
public class LogoutValidator : AbstractValidator<LogoutRequest>
{
    public LogoutValidator()
    {
        // El refresh token es opcional
    }
}
