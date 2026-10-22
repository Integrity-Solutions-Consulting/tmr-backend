
-- ============================================================================
-- SCRIPT: 16-Inv_tmr_db_permisos_notificaciones.sql
-- Propósito: Crear el permiso NOTIFICACIONES_EJECUTAR y asignarlo
-- a los roles Administrador y Lider.
-- ============================================================================

DO $$
DECLARE
    v_modulo_id INT;
    v_permiso_id INT;
    v_roles_asignados INT;
BEGIN
    -- 1. Buscar el módulo Time Report por su nombre exacto.
    SELECT id INTO STRICT v_modulo_id
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE nombremodulo = 'Time Report';

    -- 2. Crear o actualizar el permiso.
    INSERT INTO autenticacion.tbl_autenticacion_permiso
        (idmodulo, codigo, accion, descripcion, activo,
         usuariocreacion, ipcreacion)
    VALUES
        (v_modulo_id, 'NOTIFICACIONES_EJECUTAR', 'EJECUTAR',
         'Permite ejecutar y enviar notificaciones de horas faltantes',
         TRUE, 'admin', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE
    SET
        idmodulo = EXCLUDED.idmodulo,
        activo = TRUE
    RETURNING id INTO v_permiso_id;

    -- 3. Verificar que existan los roles correspondientes.
    SELECT COUNT(*) INTO v_roles_asignados
    FROM autenticacion.tbl_autenticacion_rol
    WHERE UPPER(nombre) IN ('ADMINISTRADOR', 'LIDER')
      AND activo = TRUE;

    IF v_roles_asignados <> 2 THEN
        RAISE EXCEPTION
            'Se requieren los roles activos Administrador y Lider.';
    END IF;

    -- 4. Asignar el permiso utilizando los IDs reales de los roles.
    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
        (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT
        r.id, v_permiso_id, TRUE, 'admin', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    WHERE UPPER(r.nombre) IN ('ADMINISTRADOR', 'LIDER')
      AND r.activo = TRUE
    ON CONFLICT (idrol, idpermiso) DO UPDATE
    SET activo = TRUE;

    RAISE NOTICE
        'Permiso NOTIFICACIONES_EJECUTAR configurado con ID %',
        v_permiso_id;
END $$;
