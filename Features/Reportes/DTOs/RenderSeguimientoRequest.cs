using FluentValidation;

namespace tmr_backend.Features.Reportes.DTOs;

// Pedido de descarga del reporte de seguimiento del colaborador autenticado.
// ClienteId = null significa "todos los clientes".
public sealed record RenderSeguimientoRequest(
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    string Formato = "pdf",
    int? ClienteId = null);

public sealed class RenderSeguimientoRequestValidator : AbstractValidator<RenderSeguimientoRequest>
{
    // La plantilla de seguimiento es un .xlsx: pdf y xlsx son los formatos que ya ofrece el frontend.
    public static readonly string[] FormatosPermitidos = ["pdf", "xlsx"];
    private const int MaxDiasRango = 366;

    public RenderSeguimientoRequestValidator()
    {
        RuleFor(x => x.FechaHasta)
            .GreaterThanOrEqualTo(x => x.FechaDesde)
            .WithMessage("La fecha final no puede ser anterior a la inicial.");

        RuleFor(x => x)
            .Must(x => x.FechaHasta.DayNumber - x.FechaDesde.DayNumber <= MaxDiasRango)
            .WithName(nameof(RenderSeguimientoRequest.FechaHasta))
            .WithMessage($"El rango no puede superar {MaxDiasRango} días.");

        RuleFor(x => x.Formato)
            .Must(f => !string.IsNullOrWhiteSpace(f) && FormatosPermitidos.Contains(f.Trim().ToLowerInvariant()))
            .WithMessage($"Formato no permitido. Usa: {string.Join(", ", FormatosPermitidos)}.");
    }
}
