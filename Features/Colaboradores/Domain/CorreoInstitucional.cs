namespace tmr_backend.Features.Colaboradores.Domain;

// Regla del correo institucional de los colaboradores (empleado.emailcorporativo).
public static class CorreoInstitucional
{
    public const string Dominio = "@integritysolutions.com.ec";

    public static bool EsInstitucional(string? correo) =>
        !string.IsNullOrWhiteSpace(correo)
        && correo.Trim().EndsWith(Dominio, StringComparison.OrdinalIgnoreCase);

    // Correo que se muestra de una persona: el institucional o, si no tiene, el personal.
    public static string? Principal(string? institucional, string? personal) =>
        string.IsNullOrWhiteSpace(institucional) ? personal : institucional;
}
