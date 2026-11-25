-- ============================================================================
-- SCRIPT: 25-Inv_tmr_db_vacaciones_fecha_afiliacion.sql
-- Fecha base de vacaciones = fecha de AFILIACIÓN IESS (no la fecha de ingreso general).
--   * Agrega fecha_afiliacion_base a time_report.tbl_time_report_ajuste_vacaciones.
--   * Habilita el tipo de ajuste 'FECHA_AFILIACION' (override auditable y editable
--     por Talento Humano, sin modificar administracion.tbl_administracion_empleado).
-- Garantías: transaccional e idempotente. No altera DDL de administracion/autenticacion.
-- ============================================================================

BEGIN;

ALTER TABLE time_report.tbl_time_report_ajuste_vacaciones
    ADD COLUMN IF NOT EXISTS fecha_afiliacion_base DATE NULL;

ALTER TABLE time_report.tbl_time_report_ajuste_vacaciones
    DROP CONSTRAINT IF EXISTS ck_tr_ajuste_vacaciones_tipo;

ALTER TABLE time_report.tbl_time_report_ajuste_vacaciones
    ADD CONSTRAINT ck_tr_ajuste_vacaciones_tipo
        CHECK (tipo_ajuste IN ('AJUSTE_DIAS', 'EXCEPCION_FACTURA', 'FECHA_AFILIACION'));

ALTER TABLE time_report.tbl_time_report_ajuste_vacaciones
    DROP CONSTRAINT IF EXISTS ck_tr_ajuste_vacaciones_fecha_afil;

ALTER TABLE time_report.tbl_time_report_ajuste_vacaciones
    ADD CONSTRAINT ck_tr_ajuste_vacaciones_fecha_afil
        CHECK (tipo_ajuste <> 'FECHA_AFILIACION' OR fecha_afiliacion_base IS NOT NULL);

COMMIT;
