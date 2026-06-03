using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Colaboradores.Services;

// Implementación del generador de código de empleado.
public sealed class CodigoEmpleadoGenerator(ApplicationDbContext db) : ICodigoEmpleadoGenerator
{
    public async Task<string> GenerarAsync(string prefijoAsociacion, CancellationToken ct)
    {

        // Traemos solo la columna del código de los empleados existentes.
        var codigos = await db.TblAdministracionEmpleados
            .Select(e => e.Codigoempleado)
            .ToListAsync(ct);

        // Calcular el máximo secuencial.
        int maxSecuencial = 0;
        foreach (var codigo in codigos)
        {
            if (string.IsNullOrWhiteSpace(codigo) || codigo.Length < 4)
                continue;

            // Los últimos 4 caracteres son el número (ej: "RPS0001" → "0001")
            var parteNumerica = codigo[^4..]; // toma los últimos 4 caracteres

            // Intentamos convertirlo a número. Si se puede y es mayor, lo guardamos.
            if (int.TryParse(parteNumerica, out int numero) && numero > maxSecuencial)
                maxSecuencial = numero;
        }

        // El nuevo número es el máximo + 1.
        int nuevoNumero = maxSecuencial + 1;

        // Formatear con 4 dígitos (rellena con ceros a la izquierda):
        //    1 → "0001", 16 → "0016", 123 → "0123".
        string numeroFormateado = nuevoNumero.ToString("D4");

        // Concatenar prefijo + número. Ej: "RPS" + "0001" = "RPS0001".
        return $"{prefijoAsociacion}{numeroFormateado}";
    }
}