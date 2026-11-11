using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using tmr_backend.Features.Permisos.DTOs;

namespace tmr_backend.Features.Permisos.Services;

public interface IPermisosPdfService
{
    byte[] GenerarFormularioOficialPdf(PermisoItemDto datos);
}

/// <summary>
/// Genera el documento PDF oficial "FORMULARIO DE SOLICITUD DE PERMISOS, LICENCIAS Y VACACIONES"
/// de Integrity Solutions (ISC) con el diseño exacto del formulario físico e información de aprobaciones.
/// </summary>
public sealed class PermisosPdfService : IPermisosPdfService
{
    private const string ColorAzulBarra = "#4F81BD";
    private const string ColorGrisCabecera = "#D9D9D9";
    private const string ColorGrisSub = "#EFEFEF";
    private const string ColorBorde = "#334155";
    private const string ColorTexto = "#0F172A";
    private const string ColorCorporativo = "#163572";

    private readonly IWebHostEnvironment _env;

    static PermisosPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public PermisosPdfService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public byte[] GenerarFormularioOficialPdf(PermisoItemDto d)
    {
        var logoBytes = IntentarCargarLogo();

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(32);
                page.MarginVertical(28);
                page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial).FontSize(9).FontColor(ColorTexto));

                page.Content().Column(col =>
                {
                    col.Spacing(8);

                    // 1. LOGO SUPERIOR + FOLIO
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(logoCol =>
                        {
                            if (logoBytes is not null)
                            {
                                logoCol.Item().Height(46).Image(logoBytes).FitHeight();
                            }
                            else
                            {
                                logoCol.Item().Text("INTEGRITY SOLUTIONS (ISC)")
                                    .FontSize(14).Bold().FontColor(ColorCorporativo);
                            }
                        });

                        row.ConstantItem(130).AlignRight().AlignTop().Border(1).BorderColor("#BFDBFE")
                            .Background("#EFF6FF").PaddingVertical(3).PaddingHorizontal(6)
                            .Text($"Folio: {d.FolioOrCodigo()}").FontSize(8).Bold().FontColor(ColorCorporativo);
                    });

                    // 2. TABLA CABECERA: TÍTULO + FECHA DE SOLICITUD
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5.2f);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(1.6f);
                            columns.RelativeColumn(1.6f);
                        });

                        table.Cell().RowSpan(3).Border(1).BorderColor(ColorBorde).Background(ColorGrisCabecera)
                            .Padding(6).AlignCenter().AlignMiddle()
                            .Text("FORMULARIO DE SOLICITUD DE PERMISOS, LICENCIAS\nY VACACIONES")
                            .FontSize(9.5f).Bold();

                        table.Cell().ColumnSpan(3).Border(1).BorderColor(ColorBorde).Background(ColorGrisCabecera)
                            .Padding(4).AlignCenter().Text("FECHA DE SOLICITUD").FontSize(8.5f).Bold();

                        CeldaSubHeader(table.Cell(), "Día");
                        CeldaSubHeader(table.Cell(), "Mes");
                        CeldaSubHeader(table.Cell(), "Año");

                        CeldaValorCentro(table.Cell(), d.DiaSolicitud);
                        CeldaValorCentro(table.Cell(), d.MesSolicitud);
                        CeldaValorCentro(table.Cell(), d.AnioSolicitud);
                    });

                    // 3. TABLA DATOS DEL COLABORADOR
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.4f);
                            columns.RelativeColumn(1.5f);
                            columns.RelativeColumn(2.0f);
                            columns.RelativeColumn(2.1f);
                            columns.RelativeColumn(2.0f);
                        });

                        table.Cell().ColumnSpan(5).Border(1).BorderColor(ColorBorde).Background(ColorGrisCabecera)
                            .Padding(4).AlignCenter().Text("DATOS DEL COLABORADOR").FontSize(8.5f).Bold();

                        CeldaSubHeader(table.Cell(), "Apellidos y nombres");
                        CeldaSubHeader(table.Cell(), "Número de cédula");
                        CeldaSubHeader(table.Cell(), "Cargo");
                        CeldaSubHeader(table.Cell(), "Cliente");
                        CeldaSubHeader(table.Cell(), "Jefe inmediato");

                        var clienteTexto = string.IsNullOrWhiteSpace(d.Proyecto) || d.Proyecto == d.Cliente
                            ? d.Cliente
                            : $"{d.Cliente} ({d.Proyecto})";

                        CeldaValorCentro(table.Cell(), d.Colaborador);
                        CeldaValorCentro(table.Cell(), d.Cedula);
                        CeldaValorCentro(table.Cell(), d.Cargo);
                        CeldaValorCentro(table.Cell(), clienteTexto);
                        CeldaValorCentro(table.Cell(), d.LiderNombre);
                    });

                    // 4. SECCIÓN: TIPO DE PERMISO DE AUSENCIA DE TRABAJO
                    col.Item().Column(sec =>
                    {
                        BarraSeccionAzul(sec.Item(), "TIPO DE PERMISO DE AUSENCIA DE TRABAJO");
                        sec.Item().BorderLeft(1).BorderRight(1).BorderBottom(1).BorderColor(ColorBorde)
                            .Background(ColorGrisCabecera).PaddingVertical(3).PaddingHorizontal(6)
                            .Text("Favor marcar con una X el tipo de permiso solicitado:")
                            .FontSize(8.5f).Bold().Italic();

                        sec.Item().PaddingTop(6).PaddingBottom(4).Table(grid =>
                        {
                            grid.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            ItemCasillaTipo(grid.Cell(), "Vacaciones", EsTipo(d.TipoOficial, "VACACIONES"));
                            ItemCasillaTipo(grid.Cell(), "Licencia por enfermedad", EsTipo(d.TipoOficial, "LICENCIA_ENFERMEDAD"));
                            ItemCasillaTipo(grid.Cell(), "Citas médicas", EsTipo(d.TipoOficial, "CITAS_MEDICAS"));

                            ItemCasillaTipo(grid.Cell(), "Matrimonio", EsTipo(d.TipoOficial, "MATRIMONIO"));
                            ItemCasillaTipo(grid.Cell(), "Nacimiento de un Hijo/a", EsTipo(d.TipoOficial, "NACIMIENTO_HIJO"));
                            ItemCasillaTipo(grid.Cell(), "Otros", EsTipo(d.TipoOficial, "OTROS"));

                            ItemCasillaTipo(grid.Cell(), "Maternidad", EsTipo(d.TipoOficial, "MATERNIDAD"));
                            ItemCasillaTipo(grid.Cell(), "Fallecimiento de un familiar", EsTipo(d.TipoOficial, "FALLECIMIENTO_FAMILIAR"));
                            grid.Cell().Padding(3);
                        });
                    });

                    // 5. SECCIÓN: MOTIVOS DEL PERMISO LABORAL O VACACIONAL
                    col.Item().Column(sec =>
                    {
                        BarraSeccionAzul(sec.Item(), "MOTIVOS DEL PERMISO LABORAL O VACACIONAL");
                        sec.Item().BorderLeft(1).BorderRight(1).BorderBottom(1).BorderColor(ColorBorde)
                            .MinHeight(46).Padding(6).Column(motCol =>
                            {
                                motCol.Item().Text(string.IsNullOrWhiteSpace(d.Motivo) ? "Sin detalle registrado." : d.Motivo)
                                    .FontSize(8.5f);

                                if (!string.IsNullOrWhiteSpace(d.EvidenciaNombre))
                                {
                                    motCol.Item().PaddingTop(4)
                                        .Text($"Respaldo cargado en la solicitud (disponible desde el portal TMR): {d.EvidenciaNombre}")
                                        .FontSize(7.5f).Bold().FontColor(ColorCorporativo);
                                }
                            });
                    });

                    // 6. TABLA: FECHAS Y DÍAS SOLICITADOS
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.8f);
                            columns.RelativeColumn(2.05f);
                            columns.RelativeColumn(2.05f);
                            columns.RelativeColumn(2.05f);
                            columns.RelativeColumn(2.05f);
                        });

                        table.Cell().RowSpan(2).Border(1).BorderColor(ColorBorde).Background(ColorGrisCabecera)
                            .Padding(5).AlignMiddle().Text("Fecha de la solicitud").FontSize(8.5f).Bold();

                        CeldaSubHeader(table.Cell(), "Fecha de inicio");
                        CeldaSubHeader(table.Cell(), "Fecha de finalización");
                        CeldaSubHeader(table.Cell(), "Fecha de regreso");
                        CeldaSubHeader(table.Cell(), "Fecha de aprobación");

                        CeldaValorAlto(table.Cell(), d.FechaInicio);
                        CeldaValorAlto(table.Cell(), d.FechaFin);
                        CeldaValorAlto(table.Cell(), d.FechaRegreso);
                        CeldaValorAlto(table.Cell(), string.IsNullOrWhiteSpace(d.FechaAprobacion) ? "Pendiente" : d.FechaAprobacion);

                        table.Cell().Border(1).BorderColor(ColorBorde).Background(ColorGrisCabecera)
                            .Padding(5).AlignMiddle().Text("Días solicitados").FontSize(8.5f).Bold();

                        table.Cell().ColumnSpan(4).Border(1).BorderColor(ColorBorde)
                            .Padding(5).AlignCenter().AlignMiddle()
                            .Text(d.DiasSolicitados).FontSize(9).Bold().FontColor(ColorCorporativo);
                    });

                    // 7. TABLA: HORARIO Y HORAS SOLICITADAS
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.8f);
                            columns.RelativeColumn(2.05f);
                            columns.RelativeColumn(2.05f);
                            columns.RelativeColumn(2.05f);
                            columns.RelativeColumn(2.05f);
                        });

                        table.Cell().RowSpan(2).Border(1).BorderColor(ColorBorde).Background(ColorGrisCabecera)
                            .Padding(5).AlignMiddle().Text("Horario").FontSize(8.5f).Bold();

                        CeldaSubHeader(table.Cell(), "Mañana");
                        CeldaSubHeader(table.Cell(), "Tarde");
                        CeldaSubHeader(table.Cell(), "Completa");
                        CeldaSubHeader(table.Cell(), "Otros");

                        CeldaHorario(table.Cell(), EsHorario(d.Horario, "MANANA"), d.DetalleHorario);
                        CeldaHorario(table.Cell(), EsHorario(d.Horario, "TARDE"), d.DetalleHorario);
                        CeldaHorario(table.Cell(), EsHorario(d.Horario, "COMPLETA"), d.DetalleHorario);
                        CeldaHorario(table.Cell(), EsHorario(d.Horario, "OTROS"), d.DetalleHorario);

                        table.Cell().Border(1).BorderColor(ColorBorde).Background(ColorGrisCabecera)
                            .Padding(5).AlignMiddle().Text("Horas solicitadas").FontSize(8.5f).Bold();

                        table.Cell().ColumnSpan(4).Border(1).BorderColor(ColorBorde)
                            .Padding(5).AlignCenter().AlignMiddle()
                            .Text(d.HorasSolicitadas).FontSize(9).Bold().FontColor(ColorCorporativo);
                    });

                    // 8. TABLA: FIRMAS / APROBACIONES
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1.8f);
                            columns.RelativeColumn(2.73f);
                            columns.RelativeColumn(2.73f);
                            columns.RelativeColumn(2.74f);
                        });

                        table.Cell().RowSpan(2).Border(1).BorderColor(ColorBorde).Background(ColorGrisCabecera)
                            .Padding(5).AlignMiddle().Text("Firmas").FontSize(8.5f).Bold();

                        CeldaSubHeader(table.Cell(), "Firma del solicitante");
                        CeldaSubHeader(table.Cell(), "Jefe inmediato");
                        CeldaSubHeader(table.Cell(), "Talento Humano");

                        table.Cell().Border(1).BorderColor(ColorBorde).MinHeight(46).Padding(5)
                            .AlignCenter().AlignMiddle().Column(c =>
                            {
                                c.Item().AlignCenter().Text(d.Colaborador).FontSize(8.5f).Bold();
                                c.Item().AlignCenter().Text($"Registrado en TMR · {d.FechaSolicitud}").FontSize(7).FontColor("#475569");
                            });

                        table.Cell().Border(1).BorderColor(ColorBorde).MinHeight(46).Padding(5)
                            .AlignCenter().AlignMiddle().Column(c =>
                            {
                                c.Item().AlignCenter().Text(d.LiderNombre).FontSize(8.5f).Bold();
                                var textoLider = d.AprobadoLider
                                    ? $"APROBADO · {d.MetodoLider} ({d.FechaAprobacionLider ?? d.FechaAprobacion})"
                                    : d.EstadoClase == "rechazado"
                                        ? "RECHAZADO"
                                        : "Pendiente de aprobación";
                                c.Item().AlignCenter().Text(textoLider).FontSize(7)
                                    .FontColor(d.AprobadoLider ? "#15803D" : d.EstadoClase == "rechazado" ? "#B91C1C" : "#B45309");
                            });

                        table.Cell().Border(1).BorderColor(ColorBorde).MinHeight(46).Padding(5)
                            .AlignCenter().AlignMiddle().Column(c =>
                            {
                                c.Item().AlignCenter().Text(string.IsNullOrWhiteSpace(d.ResponsableRrhh) ? "Talento Humano ISC" : d.ResponsableRrhh)
                                    .FontSize(8.5f).Bold();
                                var textoRrhh = d.ConfirmadoRrhh
                                    ? $"CONFIRMADO · {d.FechaConfirmacionRrhh ?? d.FechaAprobacion}"
                                    : "Pendiente de confirmación";
                                c.Item().AlignCenter().Text(textoRrhh).FontSize(7)
                                    .FontColor(d.ConfirmadoRrhh ? "#15803D" : "#64748B");
                            });
                    });

                    // 9. SECCIÓN: COMENTARIOS
                    col.Item().Column(sec =>
                    {
                        BarraSeccionAzul(sec.Item(), "COMENTARIOS");
                        var comentarioFinal = d.ObservacionRrhh ?? d.ComentarioLider ?? d.Comentarios ?? "Sin comentarios adicionales.";
                        sec.Item().BorderLeft(1).BorderRight(1).BorderBottom(1).BorderColor(ColorBorde)
                            .MinHeight(42).Padding(6)
                            .Text(comentarioFinal).FontSize(8.5f);
                    });
                });

                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span($"Documento oficial generado electrónicamente por ISC Time Report · Folio {d.FolioOrCodigo()} · ")
                        .FontSize(7.5f).FontColor("#64748B");
                    txt.Span($"Estado: {d.EstadoTexto}").FontSize(7.5f).Bold().FontColor(ColorCorporativo);
                });
            });
        });

        return doc.GeneratePdf();
    }

    private static void BarraSeccionAzul(IContainer container, string titulo)
    {
        container.Border(1).BorderColor(ColorBorde).Background(ColorAzulBarra)
            .PaddingVertical(4).PaddingHorizontal(6).AlignCenter()
            .Text(titulo).FontSize(8.5f).Bold().FontColor(Colors.White);
    }

    private static void CeldaSubHeader(IContainer container, string texto)
    {
        container.Border(1).BorderColor(ColorBorde).Background(ColorGrisSub)
            .PaddingVertical(3).PaddingHorizontal(4).AlignCenter().AlignMiddle()
            .Text(texto).FontSize(8).Bold();
    }

    private static void CeldaValorCentro(IContainer container, string? texto)
    {
        container.Border(1).BorderColor(ColorBorde)
            .PaddingVertical(4).PaddingHorizontal(4).AlignCenter().AlignMiddle()
            .Text(string.IsNullOrWhiteSpace(texto) ? "-" : texto).FontSize(8.5f).SemiBold();
    }

    private static void CeldaValorAlto(IContainer container, string? texto)
    {
        container.Border(1).BorderColor(ColorBorde).MinHeight(26)
            .PaddingVertical(4).PaddingHorizontal(4).AlignCenter().AlignMiddle()
            .Text(string.IsNullOrWhiteSpace(texto) ? "-" : texto).FontSize(8.5f).SemiBold();
    }

    private static void CeldaHorario(IContainer container, bool marcado, string? detalle)
    {
        container.Border(1).BorderColor(ColorBorde).MinHeight(28)
            .Padding(3).AlignCenter().AlignMiddle().Column(col =>
            {
                if (marcado)
                {
                    col.Item().AlignCenter().Text("X").FontSize(10).Bold().FontColor(ColorCorporativo);
                    if (!string.IsNullOrWhiteSpace(detalle))
                    {
                        col.Item().AlignCenter().Text(detalle).FontSize(7).FontColor("#475569");
                    }
                }
            });
    }

    private static void ItemCasillaTipo(IContainer container, string etiqueta, bool seleccionado)
    {
        container.PaddingVertical(3).PaddingHorizontal(6).Row(row =>
        {
            row.RelativeItem().AlignMiddle().Text(etiqueta).FontSize(8.5f).SemiBold();
            row.ConstantItem(20).Height(16).Border(1.2f).BorderColor(seleccionado ? ColorCorporativo : "#1E293B")
                .Background(seleccionado ? "#EFF6FF" : "#FFFFFF")
                .AlignCenter().AlignMiddle()
                .Text(seleccionado ? "X" : "").FontSize(9).Bold().FontColor(ColorCorporativo);
        });
    }

    private static bool EsTipo(string? actual, string esperado) =>
        string.Equals(actual?.Trim(), esperado, StringComparison.OrdinalIgnoreCase);

    private static bool EsHorario(string? actual, string esperado) =>
        string.Equals(actual?.Trim(), esperado, StringComparison.OrdinalIgnoreCase);

    private byte[]? IntentarCargarLogo()
    {
        var candidatos = new[]
        {
            Path.Combine(_env.ContentRootPath, "..", "tmr-frontend", "public", "assets", "img", "logo-isc.png"),
            Path.Combine(_env.ContentRootPath, "..", "tmr-frontend", "src", "assets", "imagenes", "logo-isc.png"),
            Path.Combine(_env.ContentRootPath, "wwwroot", "assets", "img", "logo-isc.png")
        };

        foreach (var ruta in candidatos)
        {
            try
            {
                if (File.Exists(ruta))
                    return File.ReadAllBytes(ruta);
            }
            catch
            {
                // Ignorar y probar siguiente ruta
            }
        }

        return null;
    }
}
