using System.Text.RegularExpressions;

namespace tmr_backend.Shared.Validation;

public static partial class TelefonoRules
{
    public const int DatabaseMaxLength = 20;
    public const string Message = "Usa un teléfono local de 7 a 10 dígitos o internacional con + y 7 a 15 dígitos.";

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsValid(string? value, bool required = false)
    {
        var normalized = Normalize(value);
        if (normalized is null) return !required;
        if (normalized.Length > DatabaseMaxLength) return false;
        if (LocalRegex().IsMatch(normalized)) return normalized.Length is >= 7 and <= 10;
        if (!InternationalRegex().IsMatch(normalized)) return false;
        return normalized.Length - 1 is >= 7 and <= 15;
    }

    [GeneratedRegex(@"^\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalRegex();

    [GeneratedRegex(@"^\+\d+$", RegexOptions.CultureInvariant)]
    private static partial Regex InternationalRegex();
}
