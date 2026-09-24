using System.IO.Compression;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace tmr_backend.Features.TimeReport.Services;

public sealed record SeguimientoActividad(
    string Fecha,
    string TipoActividad,
    string CodigoRequerimiento,
    decimal Horas,
    string Descripcion,
    string LiderProyecto,
    string ClienteProyecto,
    bool EsRecurrente
);

public static class SeguimientoReportService
{
    public static string SanitizarNombreArchivo(string nombre)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpio = string.Concat(nombre.Select(c => invalidos.Contains(c) ? '_' : c)).Trim();
        return string.IsNullOrWhiteSpace(limpio) ? "colaborador" : limpio;
    }

    public static byte[] CrearReporte(
        string nombreColaborador,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        IReadOnlyCollection<SeguimientoActividad> actividades,
        IReadOnlySet<string> feriados)
    {
        using var workbook = new XLWorkbook();
        var porCliente = actividades
            .GroupBy(a => string.IsNullOrWhiteSpace(a.ClienteProyecto) ? "Sin Cliente" : a.ClienteProyecto)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (porCliente.Count == 0)
        {
            porCliente.Add(new List<SeguimientoActividad>().GroupBy(_ => "Sin Cliente").First());
        }

        var fechas = Enumerable.Range(0, fechaHasta.DayNumber - fechaDesde.DayNumber + 1)
            .Select(fechaDesde.AddDays)
            .ToList();

        foreach (var (grupo, indice) in porCliente.Select((g, i) => (g, i)))
        {
            var nombreHoja = LimpiarNombreHoja($"Reporte_{grupo.Key}", indice + 1, workbook);
            var hoja = workbook.Worksheets.Add(nombreHoja);
            var totalColumnas = 7 + fechas.Count;

            hoja.Cell(4, 1).Value = "Cliente:";
            hoja.Cell(4, 3).Value = grupo.Key;
            hoja.Cell(5, 1).Value = "Nombre del consultor:";
            hoja.Cell(5, 3).Value = nombreColaborador;
            hoja.Range(4, 1, 5, 1).Style.Font.Bold = true;
            hoja.Range(4, 1, 5, 1).Style.Font.FontColor = XLColor.FromHtml("#163572");
            hoja.Range(4, 3, 5, 3).Style.Font.Bold = true;

            var encabezados = new[] { "N°", "TIPO DE ACTIVIDAD", "LIDER DE PROYECTO", "CODIGO REQUERIMIENTO / INCIDENTE", "DESCRIPCION DE TRABAJOS REALIZADOS", "TOTAL HORAS POR ACTIVIDAD" }
                .Concat(fechas.Select(f => f.ToString("dd")))
                .Concat(new[] { "TOTAL HORAS POR ACT." })
                .ToArray();
            hoja.Row(6).Height = 28;
            for (var columna = 1; columna <= encabezados.Length; columna++)
            {
                hoja.Cell(6, columna).Value = encabezados[columna - 1];
            }

            var encabezado = hoja.Range(6, 1, 6, encabezados.Length);
            encabezado.Style.Fill.BackgroundColor = XLColor.FromHtml("#163572");
            encabezado.Style.Font.FontColor = XLColor.White;
            encabezado.Style.Font.Bold = true;
            encabezado.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            encabezado.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            encabezado.Style.Alignment.WrapText = true;

            var filas = grupo.GroupBy(a => new { a.TipoActividad, a.LiderProyecto, a.CodigoRequerimiento, a.Descripcion, a.EsRecurrente });
            var fila = 7;
            var numero = 1;
            foreach (var actividad in filas)
            {
                hoja.Cell(fila, 1).Value = numero++;
                hoja.Cell(fila, 2).Value = actividad.Key.TipoActividad;
                hoja.Cell(fila, 3).Value = actividad.Key.LiderProyecto;
                hoja.Cell(fila, 4).Value = actividad.Key.CodigoRequerimiento;
                hoja.Cell(fila, 5).Value = actividad.Key.Descripcion;
                for (var dia = 0; dia < fechas.Count; dia++)
                {
                    var fecha = fechas[dia].ToString("yyyy-MM-dd");
                    var horas = actividad.Where(a => a.Fecha == fecha).Sum(a => a.Horas);
                    if (horas > 0) hoja.Cell(fila, 7 + dia).Value = horas;
                }
                hoja.Cell(fila, 6).FormulaA1 = $"SUM(G{fila}:{hoja.Cell(fila, 6 + fechas.Count).Address.ColumnLetter}{fila})";
                hoja.Cell(fila, totalColumnas).FormulaA1 = $"SUM(G{fila}:{hoja.Cell(fila, 6 + fechas.Count).Address.ColumnLetter}{fila})";
                fila++;
            }

            hoja.Cell(fila, 1).Value = "TOTAL";
            hoja.Cell(fila, 6).FormulaA1 = $"SUM(F7:F{fila - 1})";
            hoja.Cell(fila, totalColumnas).FormulaA1 = $"SUM({hoja.Cell(7, totalColumnas).Address.ColumnLetter}7:{hoja.Cell(fila - 1, totalColumnas).Address.ColumnLetter}{fila - 1})";
            hoja.Range(fila, 1, fila, totalColumnas).Style.Font.Bold = true;

            hoja.Column(1).Width = 6;
            hoja.Column(2).Width = 20;
            hoja.Column(3).Width = 25;
            hoja.Column(4).Width = 32;
            hoja.Column(5).Width = 60;
            hoja.Column(6).Width = 18;
            for (var columna = 7; columna <= 6 + fechas.Count; columna++) hoja.Column(columna).Width = 4.5;
            hoja.Column(totalColumnas).Width = 18;
            hoja.Range(6, 1, fila, totalColumnas).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            hoja.Range(6, 1, fila, totalColumnas).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            hoja.SheetView.FreezeRows(6);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static byte[] CrearZip(IEnumerable<(string Nombre, byte[] Contenido)> reportes)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var reporte in reportes)
            {
                var entry = zip.CreateEntry(reporte.Nombre, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                entryStream.Write(reporte.Contenido);
            }
        }
        return stream.ToArray();
    }

    public static byte[] CrearReportePdf(
        string nombreColaborador,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        IReadOnlyCollection<SeguimientoActividad> actividades)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var filas = actividades
            .GroupBy(a => new { a.Fecha, a.ClienteProyecto, a.TipoActividad, a.Descripcion })
            .OrderBy(g => g.Key.Fecha)
            .ThenBy(g => g.Key.ClienteProyecto)
            .ToList();

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(24);
            page.DefaultTextStyle(style => style.FontSize(8));
            page.Header().Column(column =>
            {
                column.Item().Text($"Seguimiento de Colaborador - {nombreColaborador}")
                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                column.Item().Text($"Periodo: del {fechaDesde:yyyy-MM-dd} al {fechaHasta:yyyy-MM-dd}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
            page.Content().PaddingTop(16).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.2f);
                    columns.RelativeColumn(2.2f);
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(3.5f);
                    columns.RelativeColumn(1f);
                });
                table.Header(header =>
                {
                    foreach (var titulo in new[] { "Fecha", "Cliente", "Tipo", "Descripción", "Horas" })
                        header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text(titulo).Bold().FontColor(Colors.White);
                });
                foreach (var fila in filas)
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(fila.Key.Fecha);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(fila.Key.ClienteProyecto);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(fila.Key.TipoActividad);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(fila.Key.Descripcion);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(4).AlignRight().Text(fila.Sum(a => a.Horas).ToString("0.##"));
                }
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("TMR - Seguimiento | ");
                text.CurrentPageNumber();
            });
        })).GeneratePdf();
    }

    public static bool EsPdf(byte[] contenido)
    {
        return contenido.Length >= 5
            && contenido[0] == (byte)'%'
            && contenido[1] == (byte)'P'
            && contenido[2] == (byte)'D'
            && contenido[3] == (byte)'F'
            && contenido[4] == (byte)'-';
    }

    private static string LimpiarNombreHoja(string nombre, int indice, XLWorkbook workbook)
    {
        var limpio = string.Concat(nombre.Select(c => "[]:*?/\\".Contains(c) ? '_' : c));
        limpio = string.IsNullOrWhiteSpace(limpio) ? $"Reporte_{indice}" : limpio[..Math.Min(31, limpio.Length)];
        var baseNombre = limpio;
        var sufijo = 1;
        while (workbook.Worksheets.Any(w => w.Name.Equals(limpio, StringComparison.OrdinalIgnoreCase)))
        {
            var extra = $"_{sufijo++}";
            limpio = baseNombre[..Math.Min(31 - extra.Length, baseNombre.Length)] + extra;
        }
        return limpio;
    }
}
