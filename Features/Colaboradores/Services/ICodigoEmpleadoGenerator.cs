namespace tmr_backend.Features.Colaboradores.Services;

// Contrato del generador de código de empleado (SOLID - DIP).
public interface ICodigoEmpleadoGenerator
{
    // Genera el siguiente código de empleado según la asociación (RPS, ISC, RPS E ISC).
    // Recibe el "valor" del catálogo de empresa (ej: "RPS") para usarlo como prefijo.
    // Es async porque consulta la base para saber cuál es el último número usado.
    Task<string> GenerarAsync(string prefijoAsociacion, CancellationToken ct);
}


