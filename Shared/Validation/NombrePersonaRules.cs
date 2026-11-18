using System.Text.RegularExpressions;
using System.Text;

namespace tmr_backend.Shared.Validation;

public static partial class NombrePersonaRules
{
    public const int MaxLength = 100;
    public const string Message = "Ingresa solo letras y espacios. Se permiten tildes y ñ. Máximo 100 caracteres.";

    public static string Normalize(string? value) => (value ?? string.Empty).Normalize(NormalizationForm.FormC).Trim();

    public static bool IsValid(string? value, bool required = true)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0) return !required && string.IsNullOrEmpty(value);
        return normalized.Length <= MaxLength && NameRegex().IsMatch(normalized);
    }

    public static bool IsOnlySpaces(string? value) => !string.IsNullOrEmpty(value) && Normalize(value).Length == 0;

    [GeneratedRegex(@"^[\p{L}\p{M} ]+$", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();
}
