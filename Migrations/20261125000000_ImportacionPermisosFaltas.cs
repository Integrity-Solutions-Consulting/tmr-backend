using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261125000000_ImportacionPermisosFaltas")]
public sealed class ImportacionPermisosFaltas : Migration
{
    // Tabla operativa de revisión del ETL. Se consulta mediante los scripts de importación.
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS time_report.tbl_time_report_permiso_falta_importacion (
    fila_excel integer PRIMARY KEY CHECK (fila_excel > 1),
    nombre_excel text NOT NULL,
    requerimiento_excel text NOT NULL,
    fecha_excel text NOT NULL,
    solicitud_excel text,
    mes_excel text,
    fecha_solicitud_excel date,
    fecha_desde_candidata date,
    fecha_hasta_candidata date,
    huella text NOT NULL CHECK (length(huella) = 64),
    fecha_carga timestamptz NOT NULL DEFAULT now(),
    decision text NOT NULL DEFAULT 'PENDIENTE'
        CHECK (decision IN ('PENDIENTE', 'LISTO', 'SOLO_HISTORICO', 'DESCARTADO')),
    id_empleado integer REFERENCES administracion.tbl_administracion_empleado(id),
    fecha_solicitud_confirmada date,
    fecha_inicio date,
    fecha_fin date,
    fecha_regreso date,
    dias_confirmados numeric(5,2),
    horas_confirmadas numeric(5,2),
    horario_confirmado varchar(20)
        CHECK (horario_confirmado IN ('COMPLETA','MANANA','TARDE','OTROS')),
    tipo_confirmado varchar(30) CHECK (tipo_confirmado <> 'VACACIONES'),
    motivo_para_tmr text,
    revisado_por varchar(50),
    revisado_en timestamptz,
    nota_revision text,
    id_solicitud integer UNIQUE REFERENCES time_report.tbl_time_report_solicitud(id)
);
REVOKE ALL ON time_report.tbl_time_report_permiso_falta_importacion FROM PUBLIC;
""");

    // Puede contener datos y decisiones de RR. HH. anteriores a su registro en EF.
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("La revisión del histórico contiene datos de RR. HH. Restaure un respaldo verificado para revertirla.");
}
