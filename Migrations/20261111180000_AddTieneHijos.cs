using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tmr_backend.Infrastructure.Database;

#nullable disable
namespace tmr_backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261111180000_AddTieneHijos")]
public partial class AddTieneHijos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE administracion.tbl_colaborador_informacion_familiar ADD COLUMN IF NOT EXISTS tienehijos varchar(20) NULL; ALTER TABLE administracion.tbl_colaborador_informacion_familiar ADD CONSTRAINT ck_colab_familiar_tienehijos CHECK (tienehijos IS NULL OR tienehijos IN ('Si','No','NoInformado'));" );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE administracion.tbl_colaborador_informacion_familiar DROP CONSTRAINT IF EXISTS ck_colab_familiar_tienehijos; ALTER TABLE administracion.tbl_colaborador_informacion_familiar DROP COLUMN IF EXISTS tienehijos;");
    }
}
