using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace tmr_backend.Shared;

public class DateOnlyJsonConverter : JsonConverter<DateOnly?>
{
    private static readonly string[] Formatos =
    [
        "dd-MM-yyyy",
        "dd/MM/yyyy",
        "yyyy-MM-dd"
    ];

    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var valor = reader.GetString();
        if (string.IsNullOrWhiteSpace(valor)) return null;

        if (DateOnly.TryParseExact(valor, Formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            return fecha;

        return null;
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Value.ToString("dd-MM-yyyy"));
    }
}