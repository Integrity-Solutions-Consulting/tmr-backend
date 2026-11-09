using System.Globalization;

namespace tmr_backend.Features.Notificaciones.Domain;

/// <summary>
/// Un proyecto del colaborador en el período (asignado o con horas registradas) y las horas que lleva en él.
/// Solo informa: la jornada se exige por persona, sumando todos sus proyectos. IdProyecto null = actividades sin proyecto.
/// </summary>
public sealed record ProyectoColaborador(int? IdProyecto, string Nombre, decimal HorasRegistradas)
{
    private static readonly CultureInfo CulturaEc = CultureInfo.GetCultureInfo("es-EC");

    /// <summary>"Horas registradas: Portal Web 40 h · ERP 0 h" (detalle del resumen del corte y de la campana).</summary>
    public static string? Describir(IReadOnlyList<ProyectoColaborador>? proyectos) =>
        proyectos is null || proyectos.Count == 0
            ? null
            : "Horas registradas: " + string.Join(" · ",
                proyectos.Select(p => $"{p.Nombre} {p.HorasRegistradas.ToString("0.##", CulturaEc)} h"));
}
