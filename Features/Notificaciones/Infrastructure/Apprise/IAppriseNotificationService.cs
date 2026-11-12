namespace tmr_backend.Features.Notificaciones.Infrastructure.Apprise;

public interface IAppriseNotificationService
{
    Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
