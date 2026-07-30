namespace tmr_backend.Features.Auth.Services;

/// <summary>
/// Servicio para generar templates de email para recuperación de contraseña.
/// </summary>
public interface IEmailTemplateService
{
    string GeneratePasswordResetEmailBody(string userFullName, string resetLink);
    string GeneratePasswordResetConfirmationEmailBody(string userFullName);
}
