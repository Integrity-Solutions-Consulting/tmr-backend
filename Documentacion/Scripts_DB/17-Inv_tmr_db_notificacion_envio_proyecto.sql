-- =============================================================================
-- PROYECTO EN EL AVISO MANUAL DE HORAS FALTANTES (flujo 2)
-- En Seguimiento cada fila es colaborador + proyecto y cada proyecto exige su propia jornada (8 h, 6 h pasante).
-- El aviso manual se hace por proyecto: el umbral entre avisos (NotificacionHoras:UmbralNotificacionManual)
-- se aplica por colaborador y proyecto, así que un colaborador puede recibir el aviso de otro proyecto.
-- NULL = aviso sin proyecto (fila "Sin Proyecto", avisos automáticos de los cortes y registros anteriores).
-- Se puede volver a ejecutar: si la llave foránea ya existía (versión anterior sin ON DELETE), se reemplaza.
-- =============================================================================

ALTER TABLE notificacion.notificacion_envio
    ADD COLUMN IF NOT EXISTS id_proyecto INT NULL;

-- ON DELETE SET NULL: borrar un proyecto no queda bloqueado por su historial de avisos;
-- esos avisos se conservan sin proyecto.
ALTER TABLE notificacion.notificacion_envio
    DROP CONSTRAINT IF EXISTS fk_notificacion_envio_proyecto;

ALTER TABLE notificacion.notificacion_envio
    ADD CONSTRAINT fk_notificacion_envio_proyecto
    FOREIGN KEY (id_proyecto) REFERENCES time_report.tbl_time_report_proyecto(id)
    ON DELETE SET NULL;

-- Búsqueda del último aviso manual por colaborador y proyecto
CREATE INDEX IF NOT EXISTS idx_notificacion_envio_manual_proyecto
    ON notificacion.notificacion_envio (id_empleado, id_proyecto, fecha_envio)
    WHERE origen = 'Manual';

COMMENT ON COLUMN notificacion.notificacion_envio.id_proyecto
    IS 'Proyecto del aviso manual (fila de Seguimiento); NULL en automáticos y en avisos sin proyecto';
