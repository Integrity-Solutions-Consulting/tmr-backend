using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace tmr_backend.Infrastructure.Shared;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            var portStr = _configuration["EmailSettings:Port"] ?? "587";
            var senderName = _configuration["EmailSettings:SenderName"] ?? "ISC Time Report";
            var senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            var username = _configuration["EmailSettings:Username"] ?? "";
            var password = _configuration["EmailSettings:Password"] ?? "";

            int.TryParse(portStr, out var port);
            if (port == 0) port = 587;

            var overrideEmail = _configuration["EmailSettings:OverrideEmailRecipient"];
            if (!string.IsNullOrEmpty(overrideEmail))
            {
                _logger.LogInformation("REDIRECCIÓN DE CORREO: Modificando destinatario real {OriginalEmail} por {OverrideEmail}", toEmail, overrideEmail);
                subject = $"[PRUEBA para: {toEmail}] {subject}";
                toEmail = overrideEmail;
            }

            _logger.LogInformation("Enviando correo SMTP con MailKit a {ToEmail} vía {SmtpServer}:{Port}", toEmail, smtpServer, port);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = htmlBody
            };

            var logoPath = Path.Combine(AppContext.BaseDirectory, "Resources", "logo-isc.png");
            if (!File.Exists(logoPath))
            {
                logoPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "logo-isc.png");
            }

            if (File.Exists(logoPath))
            {
                var image = bodyBuilder.LinkedResources.Add(logoPath);
                image.ContentId = "logo_isc";
            }

            message.Body = bodyBuilder.ToMessageBody();

            // Configurar cabeceras de identificación de agente limpias
            message.Headers.Add("X-Mailer", "MailKit (.NET Core)");

            using var client = new SmtpClient();
            
            // Determinar tipo de socket de conexión seguro
            SecureSocketOptions socketOptions = SecureSocketOptions.Auto;
            if (port == 465)
            {
                socketOptions = SecureSocketOptions.SslOnConnect;
            }
            else if (port == 587)
            {
                socketOptions = SecureSocketOptions.StartTls;
            }

            await client.ConnectAsync(smtpServer, port, socketOptions);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(username, password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Correo enviado exitosamente con MailKit a {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar correo SMTP con MailKit a {ToEmail}", toEmail);
            throw;
        }
    }
}

