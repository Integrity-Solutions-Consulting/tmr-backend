-- ============================================================================
-- SCRIPT DE LIMPIEZA DE SOLICITUDES Y PRUEBAS DE PERMISOS / VACACIONES
-- ============================================================================
-- Propósito: 
--   Eliminar de forma limpia y segura todas las solicitudes de prueba,
--   sus revisiones, adjuntos, avisos de campana y eventos de correo encolados.
--   Esto permite reiniciar el consecutivo (PER-YYYY-001) y probar en fechas
--   actuales (2026/2027) sin conflictos de traslape ni saldos agotados.
--
-- Tablas afectadas:
--   1. notificacion.evento_outbox           (cola de correos de permisos)
--   2. notificacion.notificacion_usuario     (campana interna SSE de solicitudes)
--   3. time_report.tbl_time_report_solicitud_adjunto (metadatos de archivos)
--   4. time_report.tbl_time_report_solicitud_revision (historial de decisiones)
--   5. time_report.tbl_time_report_solicitud (cabecera de solicitudes)
--
-- No toca usuarios, personas, contratos, proyectos, clientes ni catálogos.
-- ============================================================================

BEGIN;

-- 1. Eliminar eventos de correo de solicitudes en el outbox
DELETE FROM notificacion.evento_outbox 
WHERE tipo_evento = 'CorreoPermisoEncolado';

-- 2. Eliminar notificaciones de campana asociadas a solicitudes de permisos
DELETE FROM notificacion.notificacion_usuario 
WHERE id_solicitud IS NOT NULL;

-- 3. Eliminar adjuntos y revisiones
DELETE FROM time_report.tbl_time_report_solicitud_adjunto;
DELETE FROM time_report.tbl_time_report_solicitud_revision;

-- 4. Eliminar todas las solicitudes de permisos/vacaciones
DELETE FROM time_report.tbl_time_report_solicitud;

-- 5. Reiniciar la secuencia de ID de solicitudes para empezar de nuevo desde el 1
ALTER TABLE time_report.tbl_time_report_solicitud 
    ALTER COLUMN id RESTART WITH 1;

COMMIT;

-- Verificación posterior
SELECT 'Solicitudes restantes: ' || COUNT(*) FROM time_report.tbl_time_report_solicitud;
