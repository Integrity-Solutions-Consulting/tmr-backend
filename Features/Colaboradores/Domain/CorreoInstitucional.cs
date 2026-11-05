namespace tmr_backend.Features.Colaboradores.Domain;

// Regla del correo institucional de los colaboradores (empleado.emailcorporativo).
public static class CorreoInstitucional
{
    public const string Dominio = "@integritysolutions.com.ec";

    public static bool EsInstitucional(string? correo) =>
        !string.IsNullOrWhiteSpace(correo)
        && correo.Trim().EndsWith(Dominio, StringComparison.OrdinalIgnoreCase);
}
