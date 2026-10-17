namespace tmr_backend.Infrastructure.Shared;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string htmlBody);
}
