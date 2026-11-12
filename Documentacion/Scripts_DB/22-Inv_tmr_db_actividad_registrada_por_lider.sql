-- ============================================================================
-- SCRIPT: 22-Inv_tmr_db_actividad_registrada_por_lider.sql
-- Propósito: Permite saber qué actividades cargó un líder a nombre de un colaborador
-- desde el calendario de Seguimiento. IdLiderRegistro queda NULL en las actividades
-- que registró el propio colaborador.
-- Debe aplicarse ANTES de desplegar el backend que usa esta columna.
-- Se puede ejecutar más de una vez.
-- ============================================================================

BEGIN;

ALTER TABLE time_report.tbl_time_report_actividad_diaria
    ADD COLUMN IF NOT EXISTS IdLiderRegistro INTEGER NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_time_report_actividad_diaria_lider_registro'
    ) THEN
        ALTER TABLE time_report.tbl_time_report_actividad_diaria
            ADD CONSTRAINT fk_time_report_actividad_diaria_lider_registro
            FOREIGN KEY (IdLiderRegistro) REFERENCES administracion.tbl_administracion_lider(Id);
    END IF;
END $$;

COMMIT;
