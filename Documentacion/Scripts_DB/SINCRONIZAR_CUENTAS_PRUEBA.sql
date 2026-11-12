-- ============================================================================
-- SCRIPT DE SINCRONIZACIÓN DE LAS 5 CUENTAS DE PRUEBA DEL FLUJO DE PERMISOS
-- Cuentas sincronizadas:
--   1. admin@tmr.com        -> Administrador
--   2. colaborador@tmr.com  -> Colaborador (asignado al proyecto de lider@tmr.com)
--   3. lider@tmr.com        -> Líder Interno (Jefe Inmediato de colaborador@tmr.com)
--   4. ger1@tmr.com         -> Gerencia (acceso a Historial Gerencia y Seguimiento)
--   5. rec1@tmr.com         -> Recursos Humanos (Líder de Talento Humano)
--
-- Nota: Si las cuentas ya existen en autenticacion.tbl_autenticacion_usuario,
-- conserva su contraseña actual y únicamente sincroniza Persona, Empleado,
-- Contrato (Nómina), Líder, Proyecto, Asignación de Proyecto y Permisos RBAC.
-- ============================================================================

DO $$
DECLARE
    v_now                TIMESTAMPTZ := NOW();
    v_ip                 TEXT := '127.0.0.1';
    v_user_sys           TEXT := 'SYNC_PRUEBAS';
    v_hash_base          TEXT;
    v_id_contrato_nomina INT;
    v_id_tipo_lider_int  INT;
    v_id_depto           INT;
    v_id_cargo_th        INT;
    v_id_cargo_general   INT;
    v_id_lider_prueba    INT;
    v_id_emp_colab       INT;
    v_id_emp_lider       INT;
    v_id_cliente         INT;
    v_id_proyecto        INT;
    v_id_estado_proy     INT;

    r_cuenta             RECORD;
    v_id_persona         INT;
    v_id_usuario         INT;
    v_id_empleado        INT;
    v_id_rol             INT;
    v_id_cargo           INT;
    v_cod_emp            TEXT;
BEGIN
    -- 1. Obtener hash de contraseña existente de cualquier cuenta @tmr.com (o hash por defecto)
    SELECT u.hashpassword INTO v_hash_base
    FROM autenticacion.tbl_autenticacion_usuario u
    WHERE LOWER(u.email) IN ('admin@tmr.com', 'colaborador@tmr.com', 'lider@tmr.com', 'ger1@tmr.com', 'rec1@tmr.com')
      AND u.hashpassword IS NOT NULL AND u.hashpassword <> ''
    ORDER BY u.id
    LIMIT 1;

    IF v_hash_base IS NULL THEN
        SELECT u.hashpassword INTO v_hash_base
        FROM autenticacion.tbl_autenticacion_usuario u
        WHERE u.activo = TRUE AND u.hashpassword IS NOT NULL AND u.hashpassword <> ''
        ORDER BY u.id
        LIMIT 1;
    END IF;

    IF v_hash_base IS NULL THEN
        v_hash_base := '$2a$12$UFDB3k8YBM7xlG.ZMt6PGe7fetHSiIKf3HxnJpgka1oA5e6brt4Y2';
    END IF;

    -- 2. Catálogo Tipo Contrato Nómina (no Consultor COS) y Tipo Líder Interno (INT)
    SELECT d.id INTO v_id_contrato_nomina
    FROM administracion.tbl_administracion_catalogo_detalle d
    JOIN administracion.tbl_administracion_catalogo c ON c.id = d.idcatalogo
    WHERE c.codigo = 'TCT' AND d.activo = TRUE AND UPPER(COALESCE(d.codigovalor, '')) <> 'COS'
    ORDER BY d.id
    LIMIT 1;

    SELECT d.id INTO v_id_tipo_lider_int
    FROM administracion.tbl_administracion_catalogo_detalle d
    JOIN administracion.tbl_administracion_catalogo c ON c.id = d.idcatalogo
    WHERE c.codigo = 'TLI' AND d.activo = TRUE AND (UPPER(d.codigovalor) = 'INT' OR UPPER(d.valor) LIKE '%INTERN%')
    ORDER BY d.id
    LIMIT 1;

    SELECT d.id INTO v_id_depto
    FROM administracion.tbl_administracion_catalogo_detalle d
    JOIN administracion.tbl_administracion_catalogo c ON c.id = d.idcatalogo
    WHERE c.codigo = 'DEP' AND d.activo = TRUE
    ORDER BY d.id
    LIMIT 1;

    -- 3. Asegurar cargo "Líder de Talento Humano" y cargo general
    SELECT id INTO v_id_cargo_th
    FROM administracion.tbl_administracion_cargo
    WHERE UPPER(nombrecargo) IN ('LÍDER DE TALENTO HUMANO', 'LIDER DE TALENTO HUMANO')
    ORDER BY id
    LIMIT 1;

    IF v_id_cargo_th IS NULL THEN
        INSERT INTO administracion.tbl_administracion_cargo
            (nombrecargo, descripcion, iddepartamento, activo, usuariocreacion, fechacreacion, ipcreacion)
        VALUES
            ('Líder de Talento Humano', 'Responsable de validación y confirmación de Talento Humano', v_id_depto, TRUE, v_user_sys, v_now, v_ip)
        RETURNING id INTO v_id_cargo_th;
    ELSE
        UPDATE administracion.tbl_administracion_cargo SET activo = TRUE WHERE id = v_id_cargo_th;
    END IF;

    SELECT id INTO v_id_cargo_general
    FROM administracion.tbl_administracion_cargo
    WHERE activo = TRUE AND id <> v_id_cargo_th
    ORDER BY id
    LIMIT 1;

    v_id_cargo_general := COALESCE(v_id_cargo_general, v_id_cargo_th);

    -- 4. Asegurar roles base si faltan
    IF NOT EXISTS (SELECT 1 FROM autenticacion.tbl_autenticacion_rol WHERE UPPER(nombre) IN ('GERENCIA', 'GERENTE') AND activo = TRUE) THEN
        INSERT INTO autenticacion.tbl_autenticacion_rol (nombre, descripcion, essistema, activo, usuariocreacion, fechacreacion, ipcreacion)
        VALUES ('Gerencia', 'Rol de Gerencia e historial ejecutivo', TRUE, TRUE, v_user_sys, v_now, v_ip);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM autenticacion.tbl_autenticacion_rol WHERE UPPER(nombre) IN ('RECURSOS HUMANOS', 'RECURSOS_HUMANOS') AND activo = TRUE) THEN
        INSERT INTO autenticacion.tbl_autenticacion_rol (nombre, descripcion, essistema, activo, usuariocreacion, fechacreacion, ipcreacion)
        VALUES ('Recursos Humanos', 'Rol de Talento Humano', TRUE, TRUE, v_user_sys, v_now, v_ip);
    END IF;

    -- 5. Recorrer las 5 cuentas de prueba
    FOR r_cuenta IN
        SELECT * FROM (VALUES
            ('admin@tmr.com',       '1790000001', 'Administrador', 'General TMR',     'EMP-ADM-001',  'ADMINISTRADOR',    FALSE),
            ('colaborador@tmr.com', '1790000002', 'Carlos Andrés', 'Colaborador TMR', 'EMP-COL-002',  'COLABORADOR',      FALSE),
            ('lider@tmr.com',       '1790000003', 'Luis Fernando', 'Líder Proyecto',  'EMP-LID-003',  'LIDER',            TRUE),
            ('ger1@tmr.com',        '1790000004', 'Gabriel',       'Gerencia TMR',    'EMP-GER-004',  'GERENCIA',         FALSE),
            ('rec1@tmr.com',        '1790000005', 'Ana María',     'Talento Humano',  'EMP-RRHH-005', 'RECURSOS HUMANOS', FALSE)
        ) AS t(email, cedula, nombres, apellidos, codigo_emp, rol_clave, es_lider)
    LOOP
        v_id_persona := NULL;
        v_id_usuario := NULL;
        v_id_empleado := NULL;

        SELECT id, idpersona INTO v_id_usuario, v_id_persona
        FROM autenticacion.tbl_autenticacion_usuario
        WHERE LOWER(email) = r_cuenta.email
        LIMIT 1;

        IF v_id_persona IS NULL THEN
            SELECT id INTO v_id_persona
            FROM administracion.tbl_administracion_persona
            WHERE LOWER(email) = r_cuenta.email OR numeroidentificacion = r_cuenta.cedula
            ORDER BY (LOWER(email) = r_cuenta.email) DESC, id
            LIMIT 1;
        END IF;

        IF v_id_persona IS NULL THEN
            INSERT INTO administracion.tbl_administracion_persona
                (numeroidentificacion, tipopersona, nombres, apellidos, email, activo, usuariocreacion, fechacreacion, ipcreacion)
            VALUES
                (r_cuenta.cedula, 'NATURAL', r_cuenta.nombres, r_cuenta.apellidos, r_cuenta.email, TRUE, v_user_sys, v_now, v_ip)
            RETURNING id INTO v_id_persona;
        ELSE
            UPDATE administracion.tbl_administracion_persona
            SET activo = TRUE,
                email = COALESCE(NULLIF(BTRIM(email), ''), r_cuenta.email),
                nombres = COALESCE(NULLIF(BTRIM(nombres), ''), r_cuenta.nombres),
                apellidos = COALESCE(NULLIF(BTRIM(apellidos), ''), r_cuenta.apellidos)
            WHERE id = v_id_persona;
        END IF;

        IF v_id_usuario IS NULL THEN
            INSERT INTO autenticacion.tbl_autenticacion_usuario
                (idpersona, email, hashpassword, emailverificado, intentosfallidos, bloqueadohasta, debecambiarpassword, activo, usuariocreacion, fechacreacion, ipcreacion)
            VALUES
                (v_id_persona, r_cuenta.email, v_hash_base, TRUE, 0, NULL, FALSE, TRUE, v_user_sys, v_now, v_ip)
            RETURNING id INTO v_id_usuario;
        ELSE
            UPDATE autenticacion.tbl_autenticacion_usuario
            SET idpersona = COALESCE(idpersona, v_id_persona),
                activo = TRUE,
                intentosfallidos = 0,
                bloqueadohasta = NULL,
                debecambiarpassword = FALSE
            WHERE id = v_id_usuario;

            SELECT idpersona INTO v_id_persona
            FROM autenticacion.tbl_autenticacion_usuario
            WHERE id = v_id_usuario;
        END IF;

        -- Asignar rol correspondiente
        SELECT id INTO v_id_rol
        FROM autenticacion.tbl_autenticacion_rol
        WHERE activo = TRUE
          AND (
              UPPER(nombre) = r_cuenta.rol_clave
              OR (r_cuenta.rol_clave = 'LIDER' AND UPPER(nombre) IN ('LIDER', 'LÍDER'))
              OR (r_cuenta.rol_clave = 'GERENCIA' AND UPPER(nombre) IN ('GERENCIA', 'GERENTE'))
              OR (r_cuenta.rol_clave = 'RECURSOS HUMANOS' AND UPPER(nombre) IN ('RECURSOS HUMANOS', 'RECURSOS_HUMANOS'))
          )
        ORDER BY id
        LIMIT 1;

        IF v_id_rol IS NOT NULL THEN
            INSERT INTO autenticacion.tbl_autenticacion_usuario_rol
                (idusuario, idrol, asignadoen, activo, usuariocreacion, fechacreacion, ipcreacion)
            SELECT v_id_usuario, v_id_rol, v_now, TRUE, v_user_sys, v_now, v_ip
            WHERE NOT EXISTS (
                SELECT 1 FROM autenticacion.tbl_autenticacion_usuario_rol
                WHERE idusuario = v_id_usuario AND idrol = v_id_rol
            );

            UPDATE autenticacion.tbl_autenticacion_usuario_rol
            SET activo = TRUE
            WHERE idusuario = v_id_usuario AND idrol = v_id_rol;
        END IF;

        -- Asegurar ficha de empleado
        v_id_cargo := CASE WHEN r_cuenta.email = 'rec1@tmr.com' THEN v_id_cargo_th ELSE v_id_cargo_general END;

        SELECT id INTO v_id_empleado
        FROM administracion.tbl_administracion_empleado
        WHERE idpersona = v_id_persona
        ORDER BY activo DESC, id
        LIMIT 1;

        IF v_id_empleado IS NULL THEN
            v_cod_emp := r_cuenta.codigo_emp;
            IF EXISTS (SELECT 1 FROM administracion.tbl_administracion_empleado WHERE codigoempleado = v_cod_emp) THEN
                v_cod_emp := 'EMP-' || LPAD(v_id_persona::TEXT, 4, '0');
            END IF;

            INSERT INTO administracion.tbl_administracion_empleado
                (idpersona, codigoempleado, idcargo, idtipocontrato, fechaingreso, fechaterminacion, emailcorporativo, activo, usuariocreacion, fechacreacion, ipcreacion)
            VALUES
                (v_id_persona, v_cod_emp, v_id_cargo, v_id_contrato_nomina, DATE '2022-01-10', NULL, r_cuenta.email, TRUE, v_user_sys, v_now, v_ip)
            RETURNING id INTO v_id_empleado;
        ELSE
            UPDATE administracion.tbl_administracion_empleado
            SET activo = TRUE,
                fechaterminacion = NULL,
                emailcorporativo = COALESCE(NULLIF(BTRIM(emailcorporativo), ''), r_cuenta.email),
                idtipocontrato = COALESCE(idtipocontrato, v_id_contrato_nomina),
                idcargo = CASE WHEN r_cuenta.email = 'rec1@tmr.com' THEN v_id_cargo_th ELSE COALESCE(idcargo, v_id_cargo) END
            WHERE id = v_id_empleado;
        END IF;

        IF v_id_contrato_nomina IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM administracion.tbl_administracion_empleado_contrato
            WHERE idempleado = v_id_empleado AND activo = TRUE AND fechahasta IS NULL
        ) THEN
            INSERT INTO administracion.tbl_administracion_empleado_contrato
                (idempleado, idtipocontrato, fechadesde, fechahasta, activo, usuariocreacion, fechacreacion, ipcreacion)
            VALUES
                (v_id_empleado, v_id_contrato_nomina, DATE '2022-01-10', NULL, TRUE, v_user_sys, v_now, v_ip);
        END IF;

        IF r_cuenta.email = 'colaborador@tmr.com' THEN
            v_id_emp_colab := v_id_empleado;
        END IF;

        IF r_cuenta.es_lider THEN
            v_id_emp_lider := v_id_empleado;
            SELECT id INTO v_id_lider_prueba
            FROM administracion.tbl_administracion_lider
            WHERE idpersona = v_id_persona
            ORDER BY activo DESC, id
            LIMIT 1;

            IF v_id_lider_prueba IS NULL THEN
                INSERT INTO administracion.tbl_administracion_lider
                    (idpersona, idtipo, activo, usuariocreacion, fechacreacion, ipcreacion)
                VALUES
                    (v_id_persona, v_id_tipo_lider_int, TRUE, v_user_sys, v_now, v_ip)
                RETURNING id INTO v_id_lider_prueba;
            ELSE
                UPDATE administracion.tbl_administracion_lider
                SET activo = TRUE,
                    idtipo = COALESCE(idtipo, v_id_tipo_lider_int)
                WHERE id = v_id_lider_prueba;
            END IF;
        END IF;
    END LOOP;

    -- 6. Asegurar asignación de proyecto entre colaborador@tmr.com y lider@tmr.com
    IF v_id_emp_colab IS NOT NULL AND v_id_lider_prueba IS NOT NULL THEN
        IF EXISTS (
            SELECT 1 FROM time_report.tbl_time_report_asignacion_proyecto
            WHERE idempleado = v_id_emp_colab AND activo = TRUE
        ) THEN
            UPDATE time_report.tbl_time_report_asignacion_proyecto
            SET idlider = v_id_lider_prueba,
                estadoasignacion = TRUE
            WHERE id = (
                SELECT id FROM time_report.tbl_time_report_asignacion_proyecto
                WHERE idempleado = v_id_emp_colab AND activo = TRUE
                ORDER BY (idlider = v_id_lider_prueba) DESC, id DESC
                LIMIT 1
            );
        ELSE
            SELECT p.id INTO v_id_proyecto
            FROM time_report.tbl_time_report_proyecto p
            WHERE p.activo = TRUE
            ORDER BY p.id
            LIMIT 1;

            IF v_id_proyecto IS NULL THEN
                SELECT c.id INTO v_id_cliente
                FROM administracion.tbl_administracion_cliente c
                WHERE c.activo = TRUE
                ORDER BY c.id
                LIMIT 1;

                IF v_id_cliente IS NULL THEN
                    INSERT INTO administracion.tbl_administracion_cliente
                        (numeroidentificacion, razonsocial, nombrecomercial, nombres, apellidos, email, activo, usuariocreacion, fechacreacion, ipcreacion)
                    VALUES
                        ('1799999999001', 'INTEGRITY SOLUTIONS CONSULTING S.A.', 'Integrity Solutions (ISC)', 'Integrity Solutions', 'Consulting', 'contacto@integritysolutions.com.ec', TRUE, v_user_sys, v_now, v_ip)
                    RETURNING id INTO v_id_cliente;
                END IF;

                SELECT d.id INTO v_id_estado_proy
                FROM administracion.tbl_administracion_catalogo_detalle d
                JOIN administracion.tbl_administracion_catalogo c ON c.id = d.idcatalogo
                WHERE c.codigo = 'EPR' AND d.activo = TRUE
                ORDER BY d.id
                LIMIT 1;

                INSERT INTO time_report.tbl_time_report_proyecto
                    (codigo, nombre, descripcion, idcliente, idestadoproyecto, fechainicioplaneada, activo, usuariocreacion, fechacreacion, ipcreacion)
                VALUES
                    ('PRY-ISC-001', 'Operaciones y Desarrollo TMR', 'Proyecto corporativo para gestión de Time Report y Permisos', v_id_cliente, v_id_estado_proy, DATE '2025-01-01', TRUE, v_user_sys, v_now, v_ip)
                RETURNING id INTO v_id_proyecto;
            END IF;

            INSERT INTO time_report.tbl_time_report_asignacion_proyecto
                (idproyecto, idempleado, idlider, rolasignado, fechaasignacion, costoporhora, horasasignadas, estadoasignacion, activo, usuariocreacion, fechacreacion, ipcreacion)
            VALUES
                (v_id_proyecto, v_id_emp_colab, v_id_lider_prueba, 'Analista / Desarrollador', DATE '2025-01-01', 0, 160, TRUE, TRUE, v_user_sys, v_now, v_ip);
        END IF;
    END IF;

    -- 7. Asegurar permisos RBAC para los roles en el módulo Permisos y Seguimiento
    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso (idrol, idpermiso, activo, usuariocreacion, fechacreacion, ipcreacion)
    SELECT r.id, p.id, TRUE, v_user_sys, v_now, v_ip
    FROM autenticacion.tbl_autenticacion_rol r
    JOIN autenticacion.tbl_autenticacion_permiso p ON p.activo = TRUE
    WHERE r.activo = TRUE
      AND (
          p.codigo IN ('PERMISOS_VIEW', 'MIS_PERMISOS_READ', 'MIS_PERMISOS_CREATE')
          OR (p.codigo IN ('PERMISOS_LIDER_READ', 'PERMISOS_LIDER_APPROVE') AND UPPER(r.nombre) IN ('ADMINISTRADOR', 'ADMIN', 'LIDER', 'LÍDER'))
          OR (p.codigo IN ('PERMISOS_RRHH_READ', 'PERMISOS_RRHH_CONFIRM') AND UPPER(r.nombre) IN ('ADMINISTRADOR', 'ADMIN', 'RECURSOS HUMANOS', 'RECURSOS_HUMANOS'))
          OR (p.codigo IN ('PERMISOS_GERENCIA_READ', 'SEGUIMIENTO_READ') AND UPPER(r.nombre) IN ('ADMINISTRADOR', 'ADMIN', 'GERENCIA', 'GERENTE', 'RECURSOS HUMANOS', 'RECURSOS_HUMANOS', 'LIDER', 'LÍDER'))
      )
    ON CONFLICT (idrol, idpermiso) DO UPDATE SET activo = TRUE;
END $$;
