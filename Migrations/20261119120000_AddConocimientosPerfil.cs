using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261119120000_AddConocimientosPerfil")]
public sealed class AddConocimientosPerfil : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TABLE administracion.tbl_colaborador_datos_control ADD COLUMN IF NOT EXISTS conocimientos jsonb;");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn("conocimientos", "tbl_colaborador_datos_control", "administracion");
}