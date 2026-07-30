namespace tmr_backend.Features.Auth.Services;

public class EmailTemplateService : IEmailTemplateService
{
    public string GeneratePasswordResetEmailBody(string userFullName, string resetLink)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; padding: 20px; border-radius: 8px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .logo {{ max-width: 150px; height: auto; }}
        .content {{ color: #333; line-height: 1.6; }}
        .button {{ display: inline-block; background-color: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; margin-top: 20px; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; text-align: center; }}
        .warning {{ background-color: #fff3cd; padding: 10px; border-left: 4px solid #ffc107; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <img src=""cid:logo_isc"" alt=""ISC Time Report"" class=""logo"" />
        </div>
        
        <div class=""content"">
            <h2>Recuperación de Contraseña</h2>
            <p>Hola <strong>{userFullName}</strong>,</p>
            <p>Hemos recibido una solicitud para recuperar tu contraseña en ISC Time Report. Si fuiste tú, haz clic en el botón de abajo para establecer una nueva contraseña.</p>
            
            <center>
                <a href=""{resetLink}"" class=""button"">Recuperar Contraseña</a>
            </center>
            
            <p style=""margin-top: 20px;"">O copia y pega este enlace en tu navegador:</p>
            <p style=""word-break: break-all; background-color: #f0f0f0; padding: 10px; border-radius: 5px;"">{resetLink}</p>
            
            <div class=""warning"">
                <strong>⚠️ Importante:</strong> Este enlace expirará en 30 minutos por razones de seguridad.
            </div>
            
            <p>Si no solicitaste la recuperación de contraseña, puedes ignorar este correo con seguridad.</p>
        </div>
        
        <div class=""footer"">
            <p>© 2026 ISC Time Report. Todos los derechos reservados.</p>
            <p>Este es un correo automático, por favor no responder a esta dirección.</p>
        </div>
    </div>
</body>
</html>
";
    }

    public string GeneratePasswordResetConfirmationEmailBody(string userFullName)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; }}
        .container {{ max-width: 600px; margin: 0 auto; background-color: white; padding: 20px; border-radius: 8px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .logo {{ max-width: 150px; height: auto; }}
        .content {{ color: #333; line-height: 1.6; }}
        .success {{ background-color: #d4edda; border: 1px solid #c3e6cb; padding: 15px; border-radius: 5px; margin: 20px 0; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; text-align: center; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <img src=""cid:logo_isc"" alt=""ISC Time Report"" class=""logo"" />
        </div>
        
        <div class=""content"">
            <h2>Contraseña Restablecida</h2>
            <p>Hola <strong>{userFullName}</strong>,</p>
            
            <div class=""success"">
                <p><strong>✓ Tu contraseña ha sido restablecida exitosamente.</strong></p>
            </div>
            
            <p>Ya puedes iniciar sesión en ISC Time Report con tu nueva contraseña.</p>
            <p>Si no fuiste tú quien cambió la contraseña, contacta inmediatamente con el administrador del sistema.</p>
        </div>
        
        <div class=""footer"">
            <p>© 2026 ISC Time Report. Todos los derechos reservados.</p>
            <p>Este es un correo automático, por favor no responder a esta dirección.</p>
        </div>
    </div>
</body>
</html>
";
    }
}
