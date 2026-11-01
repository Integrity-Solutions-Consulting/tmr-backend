-- ============================================================================
-- SCRIPT: 19-Inv_tmr_db_modulo_permisos_ausencias.sql
-- Propósito:
--   1. Ampliar time_report.tbl_time_report_permiso para soportar el flujo
--      completo de Permisos, Licencias y Vacaciones (Colaborador -> Líder
--      Interno/Externo vía Magic Link -> Confirmación Talento Humano RRHH).
--   2. Crear los catálogos:
--        - TMR / TPE (Tipos de permiso oficial ISC)
--        - TMR / EPE (Estados de aprobación de permiso)
--        - TMR / HPE (Horarios de permiso)
--   3. Crear el módulo raíz "Permisos" y sus 4 submódulos en autenticacion,
--      con sus respectivos permisos RBAC y asignaciones por rol:
--        - Mis Permisos        -> TODOS los roles activos
--        - Aprobacion Lider    -> ADMINISTRADOR, LIDER
--        - Validacion RRHH     -> ADMINISTRADOR, RECURSOS HUMANOS
--        - Historial Gerencia  -> ADMINISTRADOR, GERENCIA/GERENTE, RECURSOS HUMANOS
-- Estrategia: 100% idempotente (seguro de ejecutar múltiples veces).
-- ============================================================================

-- 1. Ampliar columnas en time_report.tbl_time_report_permiso
ALTER TABLE time_report.tbl_time_report_permiso
    ADD COLUMN IF NOT EXISTS codigo VARCHAR(30) NULL,
    ADD COLUMN IF NOT EXISTS idproyecto INT NULL,
    ADD COLUMN IF NOT EXISTS idcliente INT NULL,
    ADD COLUMN IF NOT EXISTS idlider INT NULL,
    ADD COLUMN IF NOT EXISTS fecharegreso DATE NULL,
    ADD COLUMN IF NOT EXISTS horario VARCHAR(20) NOT NULL DEFAULT 'COMPLETA',
    ADD COLUMN IF NOT EXISTS detallehorario VARCHAR(120) NULL,
    ADD COLUMN IF NOT EXISTS evidencianombre VARCHAR(255) NULL,
    ADD COLUMN IF NOT EXISTS evidenciaurl TEXT NULL,
    ADD COLUMN IF NOT EXISTS evidenciamimetype VARCHAR(100) NULL,
    ADD COLUMN IF NOT EXISTS aprobadoporlider BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS fechaaprobacionlider TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS metodolider VARCHAR(50) NULL,
    ADD COLUMN IF NOT EXISTS comentariolider TEXT NULL,
    ADD COLUMN IF NOT EXISTS tokenexterno VARCHAR(120) NULL,
    ADD COLUMN IF NOT EXISTS tokenexternoexpira TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS confirmadoporrrhh BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN IF NOT EXISTS idempleadorrhh INT NULL,
    ADD COLUMN IF NOT EXISTS fechaconfirmacionrrhh TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS observacionrrhh TEXT NULL,
    ADD COLUMN IF NOT EXISTS impactotimereport VARCHAR(30) NOT NULL DEFAULT 'RESTA_META';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_time_report_permiso_proyecto') THEN
        ALTER TABLE time_report.tbl_time_report_permiso
            ADD CONSTRAINT fk_time_report_permiso_proyecto
            FOREIGN KEY (idproyecto) REFERENCES time_report.tbl_time_report_proyecto(id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_time_report_permiso_cliente') THEN
        ALTER TABLE time_report.tbl_time_report_permiso
            ADD CONSTRAINT fk_time_report_permiso_cliente
            FOREIGN KEY (idcliente) REFERENCES administracion.tbl_administracion_cliente(id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_time_report_permiso_lider') THEN
        ALTER TABLE time_report.tbl_time_report_permiso
            ADD CONSTRAINT fk_time_report_permiso_lider
            FOREIGN KEY (idlider) REFERENCES administracion.tbl_administracion_lider(id);
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_time_report_permiso_rrhh') THEN
        ALTER TABLE time_report.tbl_time_report_permiso
            ADD CONSTRAINT fk_time_report_permiso_rrhh
            FOREIGN KEY (idempleadorrhh) REFERENCES administracion.tbl_administracion_empleado(id);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_idx_tr_permiso_codigo
    ON time_report.tbl_time_report_permiso (codigo)
    WHERE codigo IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS uq_idx_tr_permiso_tokenexterno
    ON time_report.tbl_time_report_permiso (tokenexterno)
    WHERE tokenexterno IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_tr_permiso_lider
    ON time_report.tbl_time_report_permiso (idlider);

CREATE INDEX IF NOT EXISTS idx_tr_permiso_fechas
    ON time_report.tbl_time_report_permiso (fechainicio, fechafin);

-- 2. Catálogos TMR para Permisos (TPE, EPE, HPE)
INSERT INTO administracion.tbl_administracion_catalogo
    (tipocatalogo, codigo, descripcion, activo, usuariocreacion, ipcreacion)
VALUES
    ('TMR', 'TPE', 'Tipos de permiso, licencia y vacaciones', TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR', 'EPE', 'Estados de aprobación de solicitud de permiso', TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR', 'HPE', 'Horario de solicitud de permiso', TRUE, 'SYSTEM', '127.0.0.1')
ON CONFLICT (tipocatalogo, codigo) DO UPDATE
SET activo = TRUE,
    descripcion = EXCLUDED.descripcion;

-- 2.1 Detalles de Tipos de Permiso (TPE) - CodigoValor máx 5 chars
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (idcatalogo, codigovalor, valor, descripcion, orden, valorextra, activo, usuariocreacion, ipcreacion)
SELECT c.id, v.codigovalor, v.valor, v.descripcion, v.orden, v.valorextra, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('VAC',  'Vacaciones',                  'Vacaciones anuales programadas',        1, 'VACACIONES'),
    ('ENF',  'Licencia por enfermedad',     'Licencia médica o reposo por salud',    2, 'LICENCIA_ENFERMEDAD'),
    ('CIT',  'Citas médicas',               'Permiso por cita o control médico',     3, 'CITAS_MEDICAS'),
    ('MAT',  'Matrimonio',                  'Licencia por matrimonio civil',         4, 'MATRIMONIO'),
    ('NAC',  'Nacimiento de un Hijo/a',     'Licencia por paternidad/nacimiento',    5, 'NACIMIENTO_HIJO'),
    ('OTR',  'Otros',                       'Otros permisos laborales o personales', 6, 'OTROS'),
    ('MTR',  'Maternidad',                  'Licencia legal por maternidad',         7, 'MATERNIDAD'),
    ('FAL',  'Fallecimiento de un familiar','Licencia por calamidad doméstica/duelo',8, 'FALLECIMIENTO_FAMILIAR')
) AS v(codigovalor, valor, descripcion, orden, valorextra)
WHERE c.tipocatalogo = 'TMR' AND c.codigo = 'TPE'
ON CONFLICT (idcatalogo, codigovalor) DO UPDATE
SET valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    orden = EXCLUDED.orden,
    valorextra = EXCLUDED.valorextra,
    activo = TRUE;

-- 2.2 Detalles de Estados de Aprobación de Permiso (EPE)
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (idcatalogo, codigovalor, valor, descripcion, orden, valorextra, activo, usuariocreacion, ipcreacion)
SELECT c.id, v.codigovalor, v.valor, v.descripcion, v.orden, v.valorextra, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('PEN', 'Pendiente Jefe',    'En espera de aprobación del jefe inmediato',            1, 'pendiente'),
    ('RRH', 'En Talento Humano', 'Aprobado por líder, pendiente confirmación de RRHH',    2, 'rrhh'),
    ('CON', 'Confirmado',        'Confirmado definitivamente por Talento Humano (RRHH)',  3, 'confirmado'),
    ('OBS', 'Observado',         'Observado o devuelto para subsanación',                 4, 'observado'),
    ('REC', 'Rechazado',         'Solicitud rechazada',                                   5, 'rechazado')
) AS v(codigovalor, valor, descripcion, orden, valorextra)
WHERE c.tipocatalogo = 'TMR' AND c.codigo = 'EPE'
ON CONFLICT (idcatalogo, codigovalor) DO UPDATE
SET valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    orden = EXCLUDED.orden,
    valorextra = EXCLUDED.valorextra,
    activo = TRUE;

-- 2.3 Detalles de Horario de Permiso (HPE)
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (idcatalogo, codigovalor, valor, descripcion, orden, valorextra, activo, usuariocreacion, ipcreacion)
SELECT c.id, v.codigovalor, v.valor, v.descripcion, v.orden, v.valorextra, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('COM', 'Completa', 'Jornada completa',        1, 'COMPLETA'),
    ('MAN', 'Mañana',   'Media jornada matutina',  2, 'MANANA'),
    ('TAR', 'Tarde',    'Media jornada vespertina',3, 'TARDE'),
    ('OTR', 'Otros',    'Horario específico',      4, 'OTROS')
) AS v(codigovalor, valor, descripcion, orden, valorextra)
WHERE c.tipocatalogo = 'TMR' AND c.codigo = 'HPE'
ON CONFLICT (idcatalogo, codigovalor) DO UPDATE
SET valor = EXCLUDED.valor,
    descripcion = EXCLUDED.descripcion,
    orden = EXCLUDED.orden,
    valorextra = EXCLUDED.valorextra,
    activo = TRUE;

-- 3. Módulos y Permisos RBAC para la nueva pestaña "Permisos"
DO $$
DECLARE
    v_padre_id INT;
    v_mod_mis_permisos INT;
    v_mod_aprob_lider INT;
    v_mod_val_rrhh INT;
    v_mod_hist_gerencia INT;
    v_perm_id INT;
BEGIN
    -- 3.1 Módulo raíz "Permisos"
    SELECT id INTO v_padre_id
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE nombremodulo = 'Permisos' AND idmodulopadre IS NULL;

    IF v_padre_id IS NULL THEN
        INSERT INTO autenticacion.tbl_autenticacion_modulo
            (nombremodulo, rutamodulo, icono, ordenvisualizacion, idmodulopadre, activo, usuariocreacion, ipcreacion)
        VALUES
            ('Permisos', '/permisos', 'beach_access', 4, NULL, TRUE, 'SYSTEM', '127.0.0.1')
        RETURNING id INTO v_padre_id;
    ELSE
        UPDATE autenticacion.tbl_autenticacion_modulo
        SET activo = TRUE, rutamodulo = '/permisos', icono = 'beach_access'
        WHERE id = v_padre_id;
    END IF;

    -- Permiso VIEW del contenedor padre Permisos
    INSERT INTO autenticacion.tbl_autenticacion_permiso
        (idmodulo, codigo, accion, descripcion, activo, usuariocreacion, ipcreacion)
    VALUES
        (v_padre_id, 'PERMISOS_VIEW', 'VIEW', 'Permite visualizar el menú de Permisos', TRUE, 'SYSTEM', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE SET idmodulo = EXCLUDED.idmodulo, activo = TRUE
    RETURNING id INTO v_perm_id;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT r.id, v_perm_id, TRUE, 'SYSTEM', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    WHERE r.activo = TRUE
    ON CONFLICT (idrol, idpermiso) DO UPDATE SET activo = TRUE;

    -- 3.2 Submódulo 1: "Mis Permisos" (Todos los roles activos)
    SELECT id INTO v_mod_mis_permisos
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE nombremodulo = 'Mis Permisos';

    IF v_mod_mis_permisos IS NULL THEN
        INSERT INTO autenticacion.tbl_autenticacion_modulo
            (nombremodulo, rutamodulo, icono, ordenvisualizacion, idmodulopadre, activo, usuariocreacion, ipcreacion)
        VALUES
            ('Mis Permisos', '/permisos', 'beach_access', 1, v_padre_id, TRUE, 'SYSTEM', '127.0.0.1')
        RETURNING id INTO v_mod_mis_permisos;
    ELSE
        UPDATE autenticacion.tbl_autenticacion_modulo
        SET activo = TRUE, idmodulopadre = v_padre_id, rutamodulo = '/permisos', ordenvisualizacion = 1
        WHERE id = v_mod_mis_permisos;
    END IF;

    INSERT INTO autenticacion.tbl_autenticacion_permiso
        (idmodulo, codigo, accion, descripcion, activo, usuariocreacion, ipcreacion)
    VALUES
        (v_mod_mis_permisos, 'MIS_PERMISOS_READ', 'READ', 'Permite ver y gestionar sus propios permisos', TRUE, 'SYSTEM', '127.0.0.1'),
        (v_mod_mis_permisos, 'MIS_PERMISOS_CREATE', 'CREATE', 'Permite solicitar permisos y vacaciones', TRUE, 'SYSTEM', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE SET idmodulo = EXCLUDED.idmodulo, activo = TRUE;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT r.id, p.id, TRUE, 'SYSTEM', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    JOIN autenticacion.tbl_autenticacion_permiso p ON p.idmodulo = v_mod_mis_permisos
    WHERE r.activo = TRUE
    ON CONFLICT (idrol, idpermiso) DO UPDATE SET activo = TRUE;

    -- 3.3 Submódulo 2: "Aprobacion Lider" (Administrador y Lider)
    SELECT id INTO v_mod_aprob_lider
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE nombremodulo = 'Aprobacion Lider';

    IF v_mod_aprob_lider IS NULL THEN
        INSERT INTO autenticacion.tbl_autenticacion_modulo
            (nombremodulo, rutamodulo, icono, ordenvisualizacion, idmodulopadre, activo, usuariocreacion, ipcreacion)
        VALUES
            ('Aprobacion Lider', '/permisos/equipo', 'fact_check', 2, v_padre_id, TRUE, 'SYSTEM', '127.0.0.1')
        RETURNING id INTO v_mod_aprob_lider;
    ELSE
        UPDATE autenticacion.tbl_autenticacion_modulo
        SET activo = TRUE, idmodulopadre = v_padre_id, rutamodulo = '/permisos/equipo', ordenvisualizacion = 2
        WHERE id = v_mod_aprob_lider;
    END IF;

    INSERT INTO autenticacion.tbl_autenticacion_permiso
        (idmodulo, codigo, accion, descripcion, activo, usuariocreacion, ipcreacion)
    VALUES
        (v_mod_aprob_lider, 'PERMISOS_LIDER_READ', 'READ', 'Permite revisar solicitudes de permiso del equipo', TRUE, 'SYSTEM', '127.0.0.1'),
        (v_mod_aprob_lider, 'PERMISOS_LIDER_APPROVE', 'UPDATE', 'Permite aprobar, observar o rechazar permisos del equipo', TRUE, 'SYSTEM', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE SET idmodulo = EXCLUDED.idmodulo, activo = TRUE;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT r.id, p.id, TRUE, 'SYSTEM', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    JOIN autenticacion.tbl_autenticacion_permiso p ON p.idmodulo = v_mod_aprob_lider
    WHERE r.activo = TRUE
      AND UPPER(r.nombre) IN ('ADMINISTRADOR', 'LIDER', 'LÍDER')
    ON CONFLICT (idrol, idpermiso) DO UPDATE SET activo = TRUE;

    -- 3.4 Submódulo 3: "Validacion RRHH" (Administrador y Recursos Humanos)
    SELECT id INTO v_mod_val_rrhh
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE nombremodulo = 'Validacion RRHH';

    IF v_mod_val_rrhh IS NULL THEN
        INSERT INTO autenticacion.tbl_autenticacion_modulo
            (nombremodulo, rutamodulo, icono, ordenvisualizacion, idmodulopadre, activo, usuariocreacion, ipcreacion)
        VALUES
            ('Validacion RRHH', '/permisos/rrhh', 'verified_user', 3, v_padre_id, TRUE, 'SYSTEM', '127.0.0.1')
        RETURNING id INTO v_mod_val_rrhh;
    ELSE
        UPDATE autenticacion.tbl_autenticacion_modulo
        SET activo = TRUE, idmodulopadre = v_padre_id, rutamodulo = '/permisos/rrhh', ordenvisualizacion = 3
        WHERE id = v_mod_val_rrhh;
    END IF;

    INSERT INTO autenticacion.tbl_autenticacion_permiso
        (idmodulo, codigo, accion, descripcion, activo, usuariocreacion, ipcreacion)
    VALUES
        (v_mod_val_rrhh, 'PERMISOS_RRHH_READ', 'READ', 'Permite ver la bandeja de validación de Talento Humano', TRUE, 'SYSTEM', '127.0.0.1'),
        (v_mod_val_rrhh, 'PERMISOS_RRHH_CONFIRM', 'UPDATE', 'Permite confirmar o devolver solicitudes en Talento Humano', TRUE, 'SYSTEM', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE SET idmodulo = EXCLUDED.idmodulo, activo = TRUE;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT r.id, p.id, TRUE, 'SYSTEM', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    JOIN autenticacion.tbl_autenticacion_permiso p ON p.idmodulo = v_mod_val_rrhh
    WHERE r.activo = TRUE
      AND UPPER(r.nombre) IN ('ADMINISTRADOR', 'RECURSOS HUMANOS', 'RECURSOS_HUMANOS')
    ON CONFLICT (idrol, idpermiso) DO UPDATE SET activo = TRUE;

    -- 3.5 Submódulo 4: "Historial Gerencia" (Administrador, Gerencia/Gerente y Recursos Humanos)
    SELECT id INTO v_mod_hist_gerencia
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE nombremodulo = 'Historial Gerencia';

    IF v_mod_hist_gerencia IS NULL THEN
        INSERT INTO autenticacion.tbl_autenticacion_modulo
            (nombremodulo, rutamodulo, icono, ordenvisualizacion, idmodulopadre, activo, usuariocreacion, ipcreacion)
        VALUES
            ('Historial Gerencia', '/permisos/gerencia', 'manage_search', 4, v_padre_id, TRUE, 'SYSTEM', '127.0.0.1')
        RETURNING id INTO v_mod_hist_gerencia;
    ELSE
        UPDATE autenticacion.tbl_autenticacion_modulo
        SET activo = TRUE, idmodulopadre = v_padre_id, rutamodulo = '/permisos/gerencia', ordenvisualizacion = 4
        WHERE id = v_mod_hist_gerencia;
    END IF;

    INSERT INTO autenticacion.tbl_autenticacion_permiso
        (idmodulo, codigo, accion, descripcion, activo, usuariocreacion, ipcreacion)
    VALUES
        (v_mod_hist_gerencia, 'PERMISOS_GERENCIA_READ', 'READ', 'Permite consultar el historial general de permisos', TRUE, 'SYSTEM', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE SET idmodulo = EXCLUDED.idmodulo, activo = TRUE;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT r.id, p.id, TRUE, 'SYSTEM', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    JOIN autenticacion.tbl_autenticacion_permiso p ON p.idmodulo = v_mod_hist_gerencia
    WHERE r.activo = TRUE
      AND UPPER(r.nombre) IN ('ADMINISTRADOR', 'GERENCIA', 'GERENTE', 'RECURSOS HUMANOS', 'RECURSOS_HUMANOS')
    ON CONFLICT (idrol, idpermiso) DO UPDATE SET activo = TRUE;

    RAISE NOTICE 'Módulo Permisos (ID %) y sus 4 submódulos configurados exitosamente.', v_padre_id;
END $$;
