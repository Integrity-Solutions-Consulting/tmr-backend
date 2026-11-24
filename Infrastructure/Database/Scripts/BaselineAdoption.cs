using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using tmr_backend.Migrations;

namespace tmr_backend.Infrastructure.Database.Scripts;

internal static class BaselineAdoption
{
    private const string LegacyInitial = "20260527194023_InitialCreate";

    // IDs distribuidos hasta la versión del respaldo de desarrollo. La lista es histórica:
    // no debe crecer al incorporar nuevas migraciones.
    private static readonly string[] DevelopmentMigrations =
    [
        "20261111160000_AddActualizacionDatosColaboradores",
        "20261111173000_AddInformacionFamiliar",
        "20261111180000_AddTieneHijos",
        "20261112120000_ActualizacionDirectaYSolicitudes",
        "20261119120000_AddConocimientosPerfil"
    ];

    internal static async Task RegisterAsync(ApplicationDbContext db, NpgsqlConnection connection, string schema)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // La lectura de IHistoryRepository sobre una tabla ausente provoca 42P01 y deja
        // una transacción PostgreSQL abortada aun cuando el proveedor captura el error.
        await using var exists = new NpgsqlCommand("""
            SELECT EXISTS (SELECT FROM pg_catalog.pg_tables WHERE schemaname = @schema AND tablename = @table)
            """, connection);
        exists.Parameters.AddWithValue("schema", schema);
        exists.Parameters.AddWithValue("table", MigrationHistory.TableName);
        var applied = (bool)(await exists.ExecuteScalarAsync())!
            ? (await db.Database.GetAppliedMigrationsAsync()).ToHashSet()
            : new HashSet<string>();
        if (applied.Contains(BaselineProduccion.Id))
            return;

        await using var path = new NpgsqlCommand("SET LOCAL search_path = pg_catalog", connection);
        await path.ExecuteNonQueryAsync();
        var actual = new Dictionary<string, string>();
        await using var check = new NpgsqlCommand(BaselineProduccion.ReadSql("VerificarEstructura"), connection);
        await using (var reader = await check.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                actual.Add(reader.GetString(0), reader.GetString(1));
        }

        using var stream = typeof(BaselineAdoption).Assembly.GetManifestResourceStream("Tmr.Baseline.Estructuras.json")
            ?? throw new InvalidOperationException("Falta el manifiesto de estructuras de referencia.");
        var references = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
        applied.Remove(LegacyInitial);
        if (applied.Count > 0 && !applied.SetEquals(DevelopmentMigrations))
            throw new InvalidOperationException("Historial parcial o desconocido. Verifique las migraciones en una copia antes de adoptar.");

        string[] candidates = applied.Count == 0
            ? ["Produccion", "SQL01a23"]
            : ["Desarrollo", "SQL01a23ConPerfil", "DesarrolloConRevisionPerfilEImportacionFaltas",
               "DesarrolloConRevisionPerfilEImportacionFaltasCastDeArray"];
        var comparisons = candidates.Select(name =>
        {
            var expected = references[name];
            var differences = expected.Keys.Union(actual.Keys)
                .Where(key => expected.GetValueOrDefault(key) != actual.GetValueOrDefault(key));
            return (Name: name, Differences: differences.ToArray());
        });
        var closest = comparisons.OrderBy(comparison => comparison.Differences.Length).First();
        if (closest.Differences.Length > 0)
            throw new InvalidOperationException(
                $"La estructura difiere de {closest.Name} en: {string.Join(", ", closest.Differences)}. No se registró la línea base.");

        var history = db.GetService<IHistoryRepository>();
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(BaselineProduccion.Id, "10.0.8")));
        await transaction.CommitAsync();
        Console.WriteLine($"Línea base registrada sobre estructura verificada de {closest.Name}.");
    }
}
