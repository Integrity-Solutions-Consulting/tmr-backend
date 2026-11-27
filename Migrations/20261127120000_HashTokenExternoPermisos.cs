using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261127120000_HashTokenExternoPermisos")]
public sealed class HashTokenExternoPermisos : Migration
{
    // El magic link del líder externo pasa a guardarse como SHA-256 (ver TokenRevisionExterna.Hash).
    // Convierte los tokens en claro ya emitidos para que los enlaces enviados antes sigan funcionando.
    // Solo toca valores de 32 caracteres hexadecimales: los hashes (64) no se vuelven a procesar.
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
UPDATE time_report.tbl_time_report_solicitud
SET token_externo = encode(sha256(convert_to(lower(token_externo), 'UTF8')), 'hex')
WHERE token_externo ~ '^[0-9A-Fa-f]{32}$';
""");

    // Un hash no se puede revertir al token original. Si se vuelve a una versión anterior del backend,
    // los enlaces pendientes dejan de funcionar y RR. HH. debe usar "Reenviar enlace".
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
