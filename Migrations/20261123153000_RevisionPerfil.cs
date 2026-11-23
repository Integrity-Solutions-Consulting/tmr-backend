using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261123153000_RevisionPerfil")]
public sealed class RevisionPerfil : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
ALTER TABLE administracion.tbl_colaborador_solicitud
    ADD COLUMN IF NOT EXISTS "Seccion" text,
    ADD COLUMN IF NOT EXISTS "Propuesta" text,
    ADD COLUMN IF NOT EXISTS "EstadoRevision" text,
    ADD COLUMN IF NOT EXISTS "ObservacionRevision" text,
    ADD COLUMN IF NOT EXISTS "Revisor" text,
    ADD COLUMN IF NOT EXISTS "FechaRevision" timestamptz;
CREATE UNIQUE INDEX IF NOT EXISTS ux_colaborador_revision_pendiente
    ON administracion.tbl_colaborador_solicitud (idempleado, "Seccion")
    WHERE "EstadoRevision" IN ('EnRevision', 'RequiereCorreccion');
""");

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
DROP INDEX IF EXISTS administracion.ux_colaborador_revision_pendiente;
ALTER TABLE administracion.tbl_colaborador_solicitud
    DROP COLUMN "Seccion", DROP COLUMN "Propuesta", DROP COLUMN "EstadoRevision",
    DROP COLUMN "ObservacionRevision", DROP COLUMN "Revisor", DROP COLUMN "FechaRevision";
""");
}
