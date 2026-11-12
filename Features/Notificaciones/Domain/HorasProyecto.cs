using System.Globalization;

namespace tmr_backend.Features.Notificaciones.Domain;

/// <summary>
/// Horas faltantes de un colaborador en un proyecto. Cada proyecto exige su propia jornada
/// (8 h, o 6 h si es pasante), igual que las filas de Seguimiento. IdProyecto null = "Sin Proyecto".
/// </summary>
public sealed record HorasProyecto(int? IdProyecto, string Nombre, decimal HorasFaltantes)
{
    private static readonly CultureInfo CulturaEc = CultureInfo.GetCultureInfo("es-EC");

    /// <summary>"Portal Web: 16 h · ERP: 8 h" (para el detalle del resumen del corte).</summary>
    public static string? Describir(IReadOnlyList<HorasProyecto>? proyectos) =>
        proyectos is null || proyectos.Count == 0
            ? null
            : string.Join(" · ", proyectos.Select(p => $"{p.Nombre}: {p.HorasFaltantes.ToString("0.##", CulturaEc)} h"));
}
