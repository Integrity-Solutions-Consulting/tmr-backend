-- ============================================================================
-- SCRIPT: 14-Inv_tmr_db_permisos_notificaciones.sql
-- Propósito: Crear permiso NOTIFICACIONES_EJECUTAR y asociarlo a los roles LÍDER y ADMINISTRADOR
-- ============================================================================

DO $$
DECLARE
    v_modulo_id INT;
    v_permiso_id INT;
BEGIN
    -- 1. Obtener id del módulo 'Time Report' (o fallback a Seguimiento si no existe)
    SELECT id INTO v_modulo_id 
    FROM autenticacion.tbl_autenticacion_modulo 
    WHERE nombremodulo ILIKE '%Time Report%' 
    LIMIT 1;

    IF v_modulo_id IS NULL THEN
        SELECT id INTO v_modulo_id 
        FROM autenticacion.tbl_autenticacion_modulo 
        LIMIT 1;
    END IF;

    -- 2. Insertar permiso NOTIFICACIONES_EJECUTAR si no existe
    INSERT INTO autenticacion.tbl_autenticacion_permiso 
        (idmodulo, codigo, accion, descripcion, activo, usuariocreacion, ipcreacion)
    VALUES 
        (v_modulo_id, 'NOTIFICACIONES_EJECUTAR', 'EJECUTAR', 'Permite ejecutar y enviar notificaciones de horas faltantes', true, 'admin', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE 
        SET activo = true
    RETURNING id INTO v_permiso_id;

    -- Si ya existía y no retornó por conflicto, consultarlo directamente
    IF v_permiso_id IS NULL THEN
        SELECT id INTO v_permiso_id 
        FROM autenticacion.tbl_autenticacion_permiso 
        WHERE codigo = 'NOTIFICACIONES_EJECUTAR';
    END IF;

    -- 3. Asignar permiso a los roles ADMINISTRADOR (1) y LÍDER (3)
    -- Insertar para Administrador
    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
        (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT 1, v_permiso_id, true, 'admin', '127.0.0.1'
    WHERE NOT EXISTS (
        SELECT 1 FROM autenticacion.tbl_autenticacion_rol_permiso 
        WHERE idrol = 1 AND idpermiso = v_permiso_id
    );

    -- Insertar para Líder
    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
        (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT 3, v_permiso_id, true, 'admin', '127.0.0.1'
    WHERE NOT EXISTS (
        SELECT 1 FROM autenticacion.tbl_autenticacion_rol_permiso 
        WHERE idrol = 3 AND idpermiso = v_permiso_id
    );

    RAISE NOTICE 'Permiso NOTIFICACIONES_EJECUTAR configurado exitosamente con ID %', v_permiso_id;
END $$;
