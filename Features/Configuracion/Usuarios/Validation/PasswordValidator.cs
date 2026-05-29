using System.Text.RegularExpressions;

namespace tmr_backend.Features.Configuracion.Usuarios.Validation;

/// <summary>
/// Validador centralizado para contraseñas.
/// Garantiza complejidad: 8+ chars, mayúscula, minúscula, número, carácter especial.
/// </summary>
public static class PasswordValidator
{
    private const int MinLongitud = 8;
    private const string PatronComplejidad = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*\-._])[a-zA-Z0-9!@#$%^&*\-._]{8,}$";

    /// <summary>
    /// Valida que una contraseña cumpla con los requisitos de complejidad.
    /// Requisitos:
    /// - Mínimo 8 caracteres
    /// - Al menos una mayúscula
    /// - Al menos una minúscula
    /// - Al menos un número
    /// - Al menos un carácter especial (!@#$%^&*-._)
    /// </summary>
    public static bool EsValida(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        if (password.Length < MinLongitud)
            return false;

        // Verificar patrón de complejidad
        return Regex.IsMatch(password, PatronComplejidad);
    }

    /// <summary>
    /// Obtiene un mensaje descriptivo de los requisitos de password.
    /// </summary>
    public static string ObtenerRequisitos()
    {
        return "La contraseña debe contener: mínimo 8 caracteres, una mayúscula, una minúscula, un número y un carácter especial (!@#$%^&*-._).";
    }
}
