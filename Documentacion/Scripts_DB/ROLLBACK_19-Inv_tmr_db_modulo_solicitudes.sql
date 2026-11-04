-- ============================================================================
-- ROLLBACK del script 19-Inv_tmr_db_modulo_solicitudes.sql
--
-- Seguridad:
--   * Este archivo NO empieza con "NN-", así que DatabaseInstaller nunca lo corre solo.
--   * Respalda automáticamente cualquier fila existente en el esquema "respaldo"
--     antes de eliminar las tablas del módulo de Solicitudes.
--   * Corre en una sola transacción (BEGIN / COMMIT).
-- ============================================================================

BEGIN;

CREATE SCHEMA IF NOT EXISTS respaldo;

DO $$
DECLARE
    v_sufijo TEXT := to_char(clock_timestamp(), 'YYYYMMDD_HH24MISS');
    v_filas  BIGINT;
    r        RECORD;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('notificacion', 'notificacion_usuario',               'rb19_notificacion_usuario_'),
            ('time_report',  'tbl_time_report_solicitud_adjunto',  'rb19_solicitud_adjunto_'),
            ('time_report',  'tbl_time_report_solicitud_revision', 'rb19_solicitud_revision_'),
            ('time_report',  'tbl_time_report_solicitud',          'rb19_solicitud_')
        ) AS t(esquema, tabla, prefijo)
    LOOP
        IF to_regclass(format('%I.%I', r.esquema, r.tabla)) IS NOT NULL THEN
            EXECUTE format('SELECT COUNT(*) FROM %I.%I', r.esquema, r.tabla) INTO v_filas;
            IF v_filas > 0 THEN
                EXECUTE format('CREATE TABLE respaldo.%I AS TABLE %I.%I',
                               r.prefijo || v_sufijo, r.esquema, r.tabla);
                RAISE NOTICE 'Respaldo creado: respaldo.% (% filas)', r.prefijo || v_sufijo, v_filas;
            END IF;
        END IF;
    END LOOP;
END $$;

-- Eliminar tablas en orden inverso de dependencias
DROP TABLE IF EXISTS notificacion.notificacion_usuario;
DROP TABLE IF EXISTS time_report.tbl_time_report_solicitud_adjunto;
DROP TABLE IF EXISTS time_report.tbl_time_report_solicitud_revision;
DROP TABLE IF EXISTS time_report.tbl_time_report_solicitud;
DROP TABLE IF EXISTS time_report.tbl_time_report_tipo_permiso;
DROP TABLE IF EXISTS time_report.tbl_time_report_estado_solicitud;

-- Desactivar menú y permisos RBAC de /permisos (sin borrar por si hay auditoría)
UPDATE autenticacion.tbl_autenticacion_rol_permiso rp
SET activo = FALSE
FROM autenticacion.tbl_autenticacion_permiso p
WHERE rp.idpermiso = p.id
  AND p.codigo IN (
      'PERMISOS_VIEW',
      'MIS_PERMISOS_READ', 'MIS_PERMISOS_CREATE',
      'PERMISOS_LIDER_READ', 'PERMISOS_LIDER_APPROVE',
      'PERMISOS_RRHH_READ', 'PERMISOS_RRHH_CONFIRM',
      'PERMISOS_GERENCIA_READ'
  );

UPDATE autenticacion.tbl_autenticacion_modulo
SET activo = FALSE
WHERE nombremodulo IN ('Permisos', 'Mis Permisos', 'Aprobacion Lider', 'Validacion RRHH', 'Historial Gerencia');

DO $$
BEGIN
    IF to_regclass('public.tmr_installation_history') IS NOT NULL THEN
        DELETE FROM public.tmr_installation_history
        WHERE script = '19-Inv_tmr_db_modulo_solicitudes.sql';
    END IF;
END $$;

COMMIT;

