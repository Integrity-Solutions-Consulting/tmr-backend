using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using tmr_backend.Infrastructure.Database;
#nullable disable
namespace tmr_backend.Migrations;
[DbContext(typeof(ApplicationDbContext))]
[Migration("20261111160000_AddActualizacionDatosColaboradores")]
public partial class AddActualizacionDatosColaboradores : Migration
{
 protected override void Up(MigrationBuilder m)
 {
  m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_solicitud (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id), estado varchar(20) NOT NULL, observacion text NULL, idrevisor integer NULL, fechacreacion timestamptz NOT NULL, fechamodificacion timestamptz NOT NULL, fecharevision timestamptz NULL, version integer NOT NULL, datospropuestosjson jsonb NOT NULL);");
  m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_detalle (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE, seccion varchar(40) NOT NULL, campo varchar(80) NOT NULL, valorvigente text NULL, valorpropuesto text NULL);");
  m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_formacion (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE, nivel varchar(80) NOT NULL, institucion varchar(200) NOT NULL, titulo varchar(200) NOT NULL, fechainicio date NULL, fechafin date NULL);");
  m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_certificacion (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE, nombre varchar(200) NOT NULL, entidademisora varchar(200) NOT NULL, fechaemision date NOT NULL, fechavencimiento date NULL);");
  m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_formacion (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id), nivel varchar(80) NOT NULL, institucion varchar(200) NOT NULL, titulo varchar(200) NOT NULL, fechainicio date NULL, fechafin date NULL, activo boolean NOT NULL);");
  m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_certificacion (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id), nombre varchar(200) NOT NULL, entidademisora varchar(200) NOT NULL, fechaemision date NOT NULL, fechavencimiento date NULL, activo boolean NOT NULL);");
  m.Sql("CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_archivo (id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE, nombreoriginal text NOT NULL, contenttype text NOT NULL, tamano bigint NOT NULL, storagekey varchar(255) NOT NULL, provider varchar(30) NOT NULL, sha256 text NOT NULL, fechacreacion timestamptz NOT NULL);");
  m.Sql("CREATE INDEX IF NOT EXISTS ix_actualizacion_solicitud_empleado_estado ON administracion.tbl_actualizacion_solicitud (idempleado, estado); CREATE INDEX IF NOT EXISTS ix_actualizacion_solicitud_empleado_version ON administracion.tbl_actualizacion_solicitud (idempleado, version); CREATE UNIQUE INDEX IF NOT EXISTS ix_actualizacion_archivo_solicitud_key ON administracion.tbl_actualizacion_archivo (idsolicitud, storagekey); CREATE INDEX IF NOT EXISTS ix_colaborador_formacion_empleado_activo ON administracion.tbl_colaborador_formacion (idempleado, activo); CREATE INDEX IF NOT EXISTS ix_colaborador_certificacion_empleado_activo ON administracion.tbl_colaborador_certificacion (idempleado, activo);");
 }
 protected override void Down(MigrationBuilder m){m.Sql("DROP TABLE IF EXISTS administracion.tbl_actualizacion_archivo, administracion.tbl_colaborador_certificacion, administracion.tbl_colaborador_formacion, administracion.tbl_actualizacion_certificacion, administracion.tbl_actualizacion_formacion, administracion.tbl_actualizacion_detalle, administracion.tbl_actualizacion_solicitud;");}
}
