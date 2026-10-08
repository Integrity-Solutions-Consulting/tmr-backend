-- =============================================================================
-- ESTADO DE ASIGNACIÓN DEL RECURSO EN EL PROYECTO
-- sm - Cada recurso asignado a un proyecto puede estar Activo o Inactivo. En la tabla de Proyectos, la columna
-- Recursos solo cuenta los activos. No se reutiliza la columna "activo": la aplicación la usa para marcar la
-- versión vigente de las asignaciones (al guardar un proyecto desactiva las filas anteriores y crea nuevas).
-- Las asignaciones existentes quedan como Activas (DEFAULT TRUE).
-- =============================================================================

ALTER TABLE time_report.tbl_time_report_asignacion_proyecto
    ADD COLUMN IF NOT EXISTS estadoasignacion BOOLEAN NOT NULL DEFAULT TRUE;

COMMENT ON COLUMN time_report.tbl_time_report_asignacion_proyecto.estadoasignacion
    IS 'Estado de asignación del recurso en el proyecto: true = Activo, false = Inactivo';
