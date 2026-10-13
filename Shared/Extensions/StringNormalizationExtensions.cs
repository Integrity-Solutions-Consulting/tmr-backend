using System.Text;
using System.Text.RegularExpressions;
using System.Linq;

namespace tmr_backend.Shared.Extensions;

// Normaliza texto para búsquedas (minúsculas, sin tildes, espacios repetidos colapsados).
// Misma lógica que normalizarTexto() del frontend (shared/utils/texto.utils.ts): deben
// producir el mismo resultado para que la búsqueda se comporte igual en todas las ventanas.
//
// sm - No se usa string.Normalize(NormalizationForm.FormD) + CharUnicodeInfo: en la imagen
// runtime de Alpine (mcr.microsoft.com/dotnet/aspnet:*-alpine, la que corre en producción)
// la descomposición de tildes no funciona igual que en Windows/el SDK (ICU reducido), así que
// "é" no se separaba en "e" y quedaba sin normalizar. Se reemplaza carácter por carácter, sin
// depender de ICU, para que el comportamiento sea el mismo en cualquier entorno.
public static class StringNormalizationExtensions
{
    private static readonly Dictionary<char, char> MapaAcentos = new()
    {
        ['á'] = 'a', ['é'] = 'e', ['í'] = 'i', ['ó'] = 'o', ['ú'] = 'u', ['ü'] = 'u', ['ñ'] = 'n',
        ['à'] = 'a', ['è'] = 'e', ['ì'] = 'i', ['ò'] = 'o', ['ù'] = 'u',
        ['â'] = 'a', ['ê'] = 'e', ['î'] = 'i', ['ô'] = 'o', ['û'] = 'u',
        ['ã'] = 'a', ['õ'] = 'o', ['ç'] = 'c', ['ý'] = 'y',
    };

    public static string NormalizarBusqueda(this string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        var minuscula = texto.Trim().ToLowerInvariant();
        var sb = new StringBuilder(minuscula.Length);
        foreach (var c in minuscula)
            sb.Append(MapaAcentos.TryGetValue(c, out var sinAcento) ? sinAcento : c);

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    // sm - Coincide por palabras sueltas, no por substring contiguo: buscar "Jorge Lino" debe
    // encontrar a "Jorge Augusto Lino Bermeo" aunque "Augusto" quede en medio. Cada palabra de la
    // búsqueda debe aparecer en el texto, en cualquier orden y posición. Misma lógica que
    // coincideBusqueda() del frontend (shared/utils/texto.utils.ts).
    public static bool Coincide(this string? candidato, string? busqueda)
    {
        var termino = busqueda.NormalizarBusqueda();
        if (termino.Length == 0) return true;

        var texto = candidato.NormalizarBusqueda();
        var palabras = termino.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return palabras.All(p => texto.Contains(p));
    }
}
