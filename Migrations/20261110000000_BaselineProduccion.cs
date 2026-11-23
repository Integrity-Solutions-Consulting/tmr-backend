using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration(Id)]
public sealed class BaselineProduccion : Migration
{
    public const string Id = "20261110000000_BaselineProduccion";

    internal static string ReadSql(string file)
    {
        using var stream = typeof(BaselineProduccion).Assembly.GetManifestResourceStream($"Tmr.Baseline.{file}.sql")
            ?? throw new InvalidOperationException($"Recurso de la línea base ausente: {file}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(ReadSql("Produccion"));
        migrationBuilder.Sql(ReadSql("Catalogos"));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("La línea base contiene datos de negocio. Para volver a un estado anterior restaure un respaldo verificado.");
}
