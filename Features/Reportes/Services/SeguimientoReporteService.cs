using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using tmr_backend.Features.Reportes.Carbone;
using tmr_backend.Features.Reportes.DTOs;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Reportes.Services;

public sealed record ArchivoGenerado(byte[] Contenido, string NombreArchivo, string ContentType);

public interface ISeguimientoReporteService
{
    /// <summary>Genera el reporte de seguimiento del colaborador. Devuelve null si no hay actividades en el periodo.</summary>
    /// <exception cref="CarboneException">El motor de plantillas falló o no está disponible.</exception>
    Task<ArchivoGenerado?> GenerarAsync(int idEmpleado, RenderSeguimientoRequest request, CancellationToken ct);
}

// Arma los datos del reporte de seguimiento (un documento por proyecto) y los renderiza con tmr-carbone-service.
// Un solo proyecto -> el archivo directo. Varios proyectos -> un ZIP con un archivo por proyecto.
public sealed class SeguimientoReporteService(
    ApplicationDbContext db,
    ICarboneClient carbone,
    IOptions<CarboneSettings> settings) : ISeguimientoReporteService
{
    // Contrato de datos con la plantilla: estos nombres son las etiquetas {d.xxx} que usan las plantillas.
    // Cambiarlos rompe las plantillas ya cargadas; si hay que cambiarlos, hacerlo con una plantilla nueva.
    private sealed record PeriodoData(string Desde, string Hasta);
    private sealed record ActividadData(
        string Fecha, string Tipo, string Requerimiento, decimal Horas, string Descripcion, string Notas, string Billable);
    private sealed record ReporteData(
        string Colaborador, string Proyecto, string Cliente, string Lider,
        PeriodoData Periodo, decimal TotalHoras, List<ActividadData> Actividades);

    public async Task<ArchivoGenerado?> GenerarAsync(int idEmpleado, RenderSeguimientoRequest request, CancellationToken ct)
    {
        var formato = request.Formato.Trim().ToLowerInvariant();

        var colaborador = await db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => e.Id == idEmpleado)
            .Select(e => e.IdpersonaNavigation.Nombres + " " + e.IdpersonaNavigation.Apellidos)
            .FirstOrDefaultAsync(ct) ?? "Colaborador";

        var origen = await ActividadesReporteQuery.ObtenerAsync(db, idEmpleado, request.FechaDesde, request.FechaHasta, ct);
        var actividades = origen.Actividades
            .Where(a => request.ClienteId is null || a.IdCliente == request.ClienteId)
            .ToList();
        if (actividades.Count == 0) return null;

        var archivos = new List<(string Nombre, byte[] Contenido)>();
        var nombresUsados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var proyecto in actividades.GroupBy(a => new { a.IdProyecto, a.Proyecto }).OrderBy(g => g.Key.Proyecto))
        {
            var filas = proyecto.OrderBy(a => a.Fecha).ToList();
            var datos = new ReporteData(
                colaborador,
                proyecto.Key.Proyecto,
                filas[0].ClienteProyecto,
                filas[^1].LiderProyecto, // líder vigente en la última actividad del periodo
                new PeriodoData(request.FechaDesde.ToString("yyyy-MM-dd"), request.FechaHasta.ToString("yyyy-MM-dd")),
                filas.Sum(a => a.Horas),
                filas.Select(a => new ActividadData(
                    a.Fecha, a.TipoActividad, a.CodigoRequerimiento, a.Horas, a.Descripcion, a.Notas, a.EsBillable)).ToList());

            var contenido = await carbone.RenderAsync(settings.Value.TemplateSeguimiento, datos, formato, ct);

            var baseNombre = $"Reporte_{Limpiar(colaborador, "Colaborador")}_{Limpiar(proyecto.Key.Proyecto, "proyecto")}";
            var nombre = baseNombre;
            for (var n = 2; !nombresUsados.Add(nombre); n++) nombre = $"{baseNombre}_{n}";
            archivos.Add(($"{nombre}.{formato}", contenido));
        }

        if (archivos.Count == 1)
            return new ArchivoGenerado(archivos[0].Contenido, archivos[0].Nombre, ContentTypeDe(formato));

        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nombre, contenido) in archivos)
            {
                var entrada = zip.CreateEntry(nombre, CompressionLevel.Optimal);
                await using var salida = entrada.Open();
                await salida.WriteAsync(contenido, ct);
            }
        }

        var etiqueta = formato == "pdf" ? "PDF" : "Excel";
        var nombreZip = $"Seguimiento_{Limpiar(colaborador, "Colaborador")}_{etiqueta}_{request.FechaDesde:yyyy-MM-dd}_a_{request.FechaHasta:yyyy-MM-dd}.zip";
        return new ArchivoGenerado(memoria.ToArray(), nombreZip, "application/zip");
    }

    private static string ContentTypeDe(string formato) => formato switch
    {
        "pdf" => "application/pdf",
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };

    // Deja solo letras, números, guion y guion bajo para que sea un nombre de archivo válido en cualquier sistema.
    private static string Limpiar(string texto, string respaldo)
    {
        var limpio = new string(texto.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
        limpio = System.Text.RegularExpressions.Regex.Replace(limpio, "_{2,}", "_").Trim('_');
        return limpio.Length == 0 ? respaldo : limpio;
    }
}
