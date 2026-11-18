using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tmr_backend.Infrastructure.Database;

#nullable disable
namespace tmr_backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261111173000_AddInformacionFamiliar")]
public partial class AddInformacionFamiliar : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_informacion_familiar (idempleado integer PRIMARY KEY REFERENCES administracion.tbl_administracion_empleado(id), estadocivil varchar(80) NULL, tienediscapacidad varchar(20) NULL, tipodiscapacidad varchar(120) NULL, porcentajediscapacidad numeric(5,2) NULL, CONSTRAINT ck_colab_familiar_discapacidad CHECK (tienediscapacidad IS NULL OR tienediscapacidad IN ('Si','No','NoInformado')), CONSTRAINT ck_colab_familiar_porcentaje CHECK (porcentajediscapacidad IS NULL OR (porcentajediscapacidad >= 0 AND porcentajediscapacidad <= 100)));");
        m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_familiar (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idempleado integer NOT NULL REFERENCES administracion.tbl_colaborador_informacion_familiar(idempleado), tiporelacion varchar(30) NOT NULL, nombres varchar(100) NOT NULL, apellidos varchar(100) NOT NULL, fechanacimiento date NULL, tienediscapacidad varchar(20) NULL, tipodiscapacidad varchar(120) NULL, porcentajediscapacidad numeric(5,2) NULL, activo boolean NOT NULL DEFAULT true, CONSTRAINT ck_colab_familiar_hijo_discapacidad CHECK (tienediscapacidad IS NULL OR tienediscapacidad IN ('Si','No','NoInformado')), CONSTRAINT ck_colab_familiar_hijo_porcentaje CHECK (porcentajediscapacidad IS NULL OR (porcentajediscapacidad >= 0 AND porcentajediscapacidad <= 100)));");
        m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_familiar (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE, tiporelacion varchar(30) NOT NULL, nombres varchar(100) NOT NULL, apellidos varchar(100) NOT NULL, fechanacimiento date NULL, tienediscapacidad varchar(20) NULL, tipodiscapacidad varchar(120) NULL, porcentajediscapacidad numeric(5,2) NULL, CONSTRAINT ck_actualizacion_familiar_discapacidad CHECK (tienediscapacidad IS NULL OR tienediscapacidad IN ('Si','No','NoInformado')), CONSTRAINT ck_actualizacion_familiar_porcentaje CHECK (porcentajediscapacidad IS NULL OR (porcentajediscapacidad >= 0 AND porcentajediscapacidad <= 100)));");
        m.Sql("ALTER TABLE administracion.tbl_actualizacion_archivo ADD COLUMN IF NOT EXISTS seccion varchar(40) NOT NULL DEFAULT 'General'; ALTER TABLE administracion.tbl_actualizacion_archivo ADD COLUMN IF NOT EXISTS referenciaid bigint NULL;");
        m.Sql("CREATE INDEX IF NOT EXISTS ix_colaborador_familiar_empleado_activo ON administracion.tbl_colaborador_familiar (idempleado, activo); CREATE INDEX IF NOT EXISTS ix_actualizacion_familiar_solicitud ON administracion.tbl_actualizacion_familiar (idsolicitud);");
    }
    protected override void Down(MigrationBuilder m) { m.Sql("DROP TABLE IF EXISTS administracion.tbl_actualizacion_familiar; DROP TABLE IF EXISTS administracion.tbl_colaborador_familiar; DROP TABLE IF EXISTS administracion.tbl_colaborador_informacion_familiar;"); }
}
