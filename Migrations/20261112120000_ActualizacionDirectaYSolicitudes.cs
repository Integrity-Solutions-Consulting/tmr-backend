using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261112120000_ActualizacionDirectaYSolicitudes")]
public partial class ActualizacionDirectaYSolicitudes : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE administracion.tbl_colaborador_formacion ADD COLUMN IF NOT EXISTS clave varchar(40); UPDATE administracion.tbl_colaborador_formacion SET clave=md5(random()::text || id::text) WHERE clave IS NULL; ALTER TABLE administracion.tbl_colaborador_formacion ALTER COLUMN clave SET NOT NULL;");
        m.Sql("ALTER TABLE administracion.tbl_colaborador_certificacion ADD COLUMN IF NOT EXISTS clave varchar(40); UPDATE administracion.tbl_colaborador_certificacion SET clave=md5(random()::text || id::text) WHERE clave IS NULL; ALTER TABLE administracion.tbl_colaborador_certificacion ALTER COLUMN clave SET NOT NULL;");
        m.Sql("ALTER TABLE administracion.tbl_colaborador_familiar ADD COLUMN IF NOT EXISTS identificacion varchar(40), ADD COLUMN IF NOT EXISTS clave varchar(40); UPDATE administracion.tbl_colaborador_familiar SET clave=md5(random()::text || id::text) WHERE clave IS NULL; ALTER TABLE administracion.tbl_colaborador_familiar ALTER COLUMN clave SET NOT NULL;");
        m.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ux_colab_formacion_empleado_clave ON administracion.tbl_colaborador_formacion(idempleado, clave); CREATE UNIQUE INDEX IF NOT EXISTS ux_colab_cert_empleado_clave ON administracion.tbl_colaborador_certificacion(idempleado, clave); CREATE UNIQUE INDEX IF NOT EXISTS ux_colab_familiar_empleado_clave ON administracion.tbl_colaborador_familiar(idempleado, clave);");
        m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_datos_control (idempleado integer PRIMARY KEY REFERENCES administracion.tbl_administracion_empleado(id), version bigint NOT NULL DEFAULT 1, fechamodificacion timestamp with time zone NOT NULL DEFAULT now());");
        m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_documento (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id), seccion varchar(40) NOT NULL, referenciaid bigint NULL, referenciaclave varchar(80) NULL, nombreoriginal varchar(255) NOT NULL, contenttype varchar(120) NOT NULL, tamano bigint NOT NULL, storagekey varchar(255) NOT NULL, provider varchar(30) NOT NULL, sha256 varchar(128) NOT NULL, fechacreacion timestamp with time zone NOT NULL, activo boolean NOT NULL DEFAULT true); CREATE INDEX IF NOT EXISTS ix_colaborador_documento_owner ON administracion.tbl_colaborador_documento(idempleado, seccion, referenciaid, referenciaclave);");
        m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_solicitud (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id), asunto varchar(200) NOT NULL, descripcion varchar(4000) NOT NULL, fechacreacion timestamp with time zone NOT NULL, fechaenvio timestamp with time zone NULL, enviada boolean NOT NULL DEFAULT false); CREATE INDEX IF NOT EXISTS ix_colaborador_solicitud_owner ON administracion.tbl_colaborador_solicitud(idempleado, fechacreacion);");
        m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_solicitud_archivo (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idsolicitud bigint NOT NULL REFERENCES administracion.tbl_colaborador_solicitud(id) ON DELETE CASCADE, nombreoriginal varchar(255) NOT NULL, contenttype varchar(120) NOT NULL, tamano bigint NOT NULL, storagekey varchar(255) NOT NULL, provider varchar(30) NOT NULL, sha256 varchar(128) NOT NULL, fechacreacion timestamp with time zone NOT NULL);");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP TABLE IF EXISTS administracion.tbl_colaborador_solicitud_archivo; DROP TABLE IF EXISTS administracion.tbl_colaborador_solicitud; DROP TABLE IF EXISTS administracion.tbl_colaborador_documento; DROP TABLE IF EXISTS administracion.tbl_colaborador_datos_control;");
        m.Sql("ALTER TABLE administracion.tbl_colaborador_familiar DROP COLUMN IF EXISTS identificacion, DROP COLUMN IF EXISTS clave; ALTER TABLE administracion.tbl_colaborador_formacion DROP COLUMN IF EXISTS clave; ALTER TABLE administracion.tbl_colaborador_certificacion DROP COLUMN IF EXISTS clave;");
    }
}
