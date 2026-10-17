using tmr_backend.Features.Configuracion.DiasFestivos.Domain.Exceptions;

namespace tmr_backend.Features.Configuracion.DiasFestivos.Domain;

public class Feriado
{
    public int Id { get; private set; }
    public string NombreFeriado { get; private set; } = string.Empty;
    public DateOnly FechaFeriado { get; private set; }
    public bool EsRecurrente { get; private set; }
    public string TipoFeriado { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public bool Activo { get; private set; }

    private Feriado() { }

    public static Feriado Crear(string nombreFeriado, DateOnly fechaFeriado, string tipoFeriado, bool esRecurrente, string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombreFeriado))
            throw new DatosInvalidosFeriadoException("El nombre del feriado es requerido.");

        if (string.IsNullOrWhiteSpace(tipoFeriado))
            throw new DatosInvalidosFeriadoException("El tipo de feriado es requerido.");

        var tiposValidos = new[] { "Local", "Nacional", "Religioso" };
        if (!tiposValidos.Contains(tipoFeriado, StringComparer.OrdinalIgnoreCase))
            throw new DatosInvalidosFeriadoException("Tipo de feriado inválido. Debe ser: Local, Nacional o Religioso.");

        return new Feriado
        {
            NombreFeriado = nombreFeriado.Trim(),
            FechaFeriado = fechaFeriado,
            TipoFeriado = tipoFeriado.Trim(),
            EsRecurrente = esRecurrente,
            Descripcion = descripcion?.Trim(),
            Activo = true
        };
    }

    public void ActualizarDatos(string nombreFeriado, DateOnly fechaFeriado, string tipoFeriado, bool esRecurrente, string? descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombreFeriado))
            throw new DatosInvalidosFeriadoException("El nombre del feriado es requerido.");

        if (string.IsNullOrWhiteSpace(tipoFeriado))
            throw new DatosInvalidosFeriadoException("El tipo de feriado es requerido.");

        var tiposValidos = new[] { "Local", "Nacional", "Religioso" };
        if (!tiposValidos.Contains(tipoFeriado, StringComparer.OrdinalIgnoreCase))
            throw new DatosInvalidosFeriadoException("Tipo de feriado inválido. Debe ser: Local, Nacional o Religioso.");

        NombreFeriado = nombreFeriado.Trim();
        FechaFeriado = fechaFeriado;
        TipoFeriado = tipoFeriado.Trim();
        EsRecurrente = esRecurrente;
        Descripcion = descripcion?.Trim();
    }

    public void Desactivar()
    {
        Activo = false;
    }
}
