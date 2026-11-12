-- ============================================================================
-- SCRIPT: 18-Inv_tmr_db_fechas_corte_configurables.sql
-- Propósito: Flujo 3 de notificaciones. Las fechas de corte de horas dejan de
-- estar fijas en el código y se editan desde la pantalla "Fechas de Corte".
--   1. Columnas de auditoría de la última modificación en notificacion_fecha_corte.
--   2. Módulo "Fechas de Corte" (hijo de Time Report).
--   3. Permiso NOTIFICACIONES_CONFIGURAR en ese módulo, asignado a Administrador.
--      Para dárselo a otro rol (p. ej. quien revisa los informes) basta con marcar
--      el módulo "Fechas de Corte" en Configuración > Roles.
-- Requiere: 15-Inv_tmr_db_notificaciones_outbox.sql
-- Se puede ejecutar más de una vez.
-- ============================================================================

-- 1. Auditoría de la última modificación
ALTER TABLE notificacion.notificacion_fecha_corte
    ADD COLUMN IF NOT EXISTS id_usuario_modificacion INT NULL,
    ADD COLUMN IF NOT EXISTS fecha_modificacion TIMESTAMPTZ NULL;

COMMENT ON COLUMN notificacion.notificacion_fecha_corte.id_usuario_creador IS
    'Usuario que creó la fila. 0 = creada automáticamente por el sistema con la fecha por defecto.';

DO $$
DECLARE
    v_padre_id INT;
    v_modulo_id INT;
    v_permiso_id INT;
BEGIN
    -- 2. Módulo "Fechas de Corte" dentro de Time Report
    SELECT id INTO STRICT v_padre_id
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE nombremodulo = 'Time Report';

    SELECT id INTO v_modulo_id
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE nombremodulo = 'Fechas de Corte';

    IF v_modulo_id IS NULL THEN
        INSERT INTO autenticacion.tbl_autenticacion_modulo
            (nombremodulo, rutamodulo, ordenvisualizacion, idmodulopadre, activo, usuariocreacion, ipcreacion)
        VALUES
            ('Fechas de Corte', '/menu/time-reports/cut-dates', 3, v_padre_id, TRUE, 'admin', '127.0.0.1')
        RETURNING id INTO v_modulo_id;
    ELSE
        UPDATE autenticacion.tbl_autenticacion_modulo
        SET activo = TRUE, idmodulopadre = v_padre_id
        WHERE id = v_modulo_id;
    END IF;

    -- 3. Permiso
    INSERT INTO autenticacion.tbl_autenticacion_permiso
        (idmodulo, codigo, accion, descripcion, activo, usuariocreacion, ipcreacion)
    VALUES
        (v_modulo_id, 'NOTIFICACIONES_CONFIGURAR', 'CONFIGURAR',
         'Permite ver y modificar las fechas de corte de la notificación de horas faltantes',
         TRUE, 'admin', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE
    SET idmodulo = EXCLUDED.idmodulo,
        activo = TRUE
    RETURNING id INTO v_permiso_id;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
        (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT r.id, v_permiso_id, TRUE, 'admin', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    -- Sin distinguir mayúsculas: según la instalación el rol se llama "Administrador" o "ADMINISTRADOR"
    WHERE UPPER(r.nombre) = 'ADMINISTRADOR'
      AND r.activo = TRUE
    ON CONFLICT (idrol, idpermiso) DO UPDATE
    SET activo = TRUE;

    RAISE NOTICE 'Módulo Fechas de Corte (ID %) y permiso NOTIFICACIONES_CONFIGURAR (ID %) configurados',
        v_modulo_id, v_permiso_id;
END $$;
