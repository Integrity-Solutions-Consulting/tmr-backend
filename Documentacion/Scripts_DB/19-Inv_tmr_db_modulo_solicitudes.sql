-- ============================================================================
-- SCRIPT: 19-Inv_tmr_db_modulo_solicitudes.sql
-- Módulo de Solicitudes (Permisos, Licencias y Vacaciones) - Diseño Normalizado (3FN)
--
-- Propósito (alineado con docs/requerimientos-solicitudes-v2.docx + tmr-frontend):
--   0. Revertir limpiamente las columnas, FKs, índices y catálogos extra que se
--      habían agregado sobre time_report.tbl_time_report_permiso en la versión
--      no normalizada (respaldando cualquier fila de prueba previa en "respaldo").
--   1. time_report.tbl_time_report_estado_solicitud   -> Estados y semáforo (RF-006)
--   2. time_report.tbl_time_report_tipo_permiso       -> 8 tipos oficiales ISC (RF-002)
--   3. time_report.tbl_time_report_solicitud          -> Cabecera en 3FN (RF-001, RF-003)
--   4. time_report.tbl_time_report_solicitud_revision -> Decisiones Paso 2 y 3 (RF-007..009)
--   5. time_report.tbl_time_report_solicitud_adjunto  -> Metadatos y llave S3/MinIO (RF-005, RNF-004)
--   6. notificacion.notificacion_usuario              -> Avisos campana SSE con leído (RF-010)
--   7. Permisos RBAC de navegación para el menú /permisos (sin alterar DDL de autenticacion)
--
-- Garantías:
--   * Transaccional (BEGIN / COMMIT) e idempotente.
--   * No altera la estructura (DDL) de los esquemas autenticacion ni administracion.
--   * Reversa disponible en: ROLLBACK_19-Inv_tmr_db_modulo_solicitudes.sql
-- ============================================================================

BEGIN;

CREATE SCHEMA IF NOT EXISTS notificacion;

-- ============================================================================
-- 0. REVERSIÓN SEGURA DE LA VERSIÓN DESNORMALIZADA ANTERIOR (si fue aplicada)
--    Deja time_report.tbl_time_report_permiso exactamente como en el script 01.
-- ============================================================================
DO $$
DECLARE
    v_tiene_col_amigo BOOLEAN;
    v_filas_previas   BIGINT := 0;
    v_sufijo          TEXT := to_char(clock_timestamp(), 'YYYYMMDD_HH24MISS');
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'time_report'
          AND table_name   = 'tbl_time_report_permiso'
          AND column_name IN ('codigo', 'evidenciaurl', 'aprobadoporlider', 'confirmadoporrrhh', 'tokenexterno')
    ) INTO v_tiene_col_amigo;

    IF v_tiene_col_amigo THEN
        EXECUTE 'SELECT COUNT(*) FROM time_report.tbl_time_report_permiso' INTO v_filas_previas;
        IF v_filas_previas > 0 THEN
            CREATE SCHEMA IF NOT EXISTS respaldo;
            EXECUTE format(
                'CREATE TABLE respaldo.%I AS TABLE time_report.tbl_time_report_permiso',
                'rb_permiso_desnormalizado_' || v_sufijo
            );
            RAISE NOTICE 'Respaldo preventivo creado en respaldo.rb_permiso_desnormalizado_% (% filas).',
                v_sufijo, v_filas_previas;

            -- Eliminar filas que apunten a los catálogos temporales TPE/EPE antes de borrarlos
            DELETE FROM time_report.tbl_time_report_permiso p
            USING administracion.tbl_administracion_catalogo_detalle d
            JOIN administracion.tbl_administracion_catalogo c ON c.id = d.idcatalogo
            WHERE (p.idtipopermiso = d.id OR p.idestadoaprobacion = d.id)
              AND c.tipocatalogo = 'TMR'
              AND c.codigo IN ('TPE', 'EPE', 'HPE');
        END IF;

        -- Quitar índices y FKs agregados a tbl_time_report_permiso
        DROP INDEX IF EXISTS time_report.uq_idx_tr_permiso_codigo;
        DROP INDEX IF EXISTS time_report.uq_idx_tr_permiso_tokenexterno;
        DROP INDEX IF EXISTS time_report.idx_tr_permiso_lider;
        DROP INDEX IF EXISTS time_report.idx_tr_permiso_fechas;

        ALTER TABLE time_report.tbl_time_report_permiso
            DROP CONSTRAINT IF EXISTS fk_time_report_permiso_proyecto,
            DROP CONSTRAINT IF EXISTS fk_time_report_permiso_cliente,
            DROP CONSTRAINT IF EXISTS fk_time_report_permiso_lider,
            DROP CONSTRAINT IF EXISTS fk_time_report_permiso_rrhh;

        -- Quitar las columnas agregadas sobre tbl_time_report_permiso
        ALTER TABLE time_report.tbl_time_report_permiso
            DROP COLUMN IF EXISTS codigo,
            DROP COLUMN IF EXISTS idproyecto,
            DROP COLUMN IF EXISTS idcliente,
            DROP COLUMN IF EXISTS idlider,
            DROP COLUMN IF EXISTS fecharegreso,
            DROP COLUMN IF EXISTS horario,
            DROP COLUMN IF EXISTS detallehorario,
            DROP COLUMN IF EXISTS evidencianombre,
            DROP COLUMN IF EXISTS evidenciaurl,
            DROP COLUMN IF EXISTS evidenciamimetype,
            DROP COLUMN IF EXISTS aprobadoporlider,
            DROP COLUMN IF EXISTS fechaaprobacionlider,
            DROP COLUMN IF EXISTS metodolider,
            DROP COLUMN IF EXISTS comentariolider,
            DROP COLUMN IF EXISTS tokenexterno,
            DROP COLUMN IF EXISTS tokenexternoexpira,
            DROP COLUMN IF EXISTS tokenexternousadoen,
            DROP COLUMN IF EXISTS confirmadoporrrhh,
            DROP COLUMN IF EXISTS idempleadorrhh,
            DROP COLUMN IF EXISTS fechaconfirmacionrrhh,
            DROP COLUMN IF EXISTS observacionrrhh,
            DROP COLUMN IF EXISTS impactotimereport;

        RAISE NOTICE 'time_report.tbl_time_report_permiso restaurada a su estructura original del script 01.';
    END IF;

    -- Limpiar catálogos TPE / EPE / HPE en administracion si no tienen referencias externas
    IF NOT EXISTS (
        SELECT 1
        FROM time_report.tbl_time_report_permiso p
        JOIN administracion.tbl_administracion_catalogo_detalle d
          ON d.id IN (p.idtipopermiso, p.idestadoaprobacion)
        JOIN administracion.tbl_administracion_catalogo c ON c.id = d.idcatalogo
        WHERE c.tipocatalogo = 'TMR' AND c.codigo IN ('TPE', 'EPE', 'HPE')
    ) THEN
        DELETE FROM administracion.tbl_administracion_catalogo_detalle d
        USING administracion.tbl_administracion_catalogo c
        WHERE d.idcatalogo = c.id
          AND c.tipocatalogo = 'TMR'
          AND c.codigo IN ('TPE', 'EPE', 'HPE');

        DELETE FROM administracion.tbl_administracion_catalogo
        WHERE tipocatalogo = 'TMR'
          AND codigo IN ('TPE', 'EPE', 'HPE');
    END IF;

    -- Limpiar tablas intermedias si se hubiera probado algún borrador del script 20
    DROP TABLE IF EXISTS time_report.tbl_time_report_solicitud_adjunto CASCADE;
    DROP TABLE IF EXISTS time_report.tbl_time_report_solicitud_revision CASCADE;

    -- Limpiar entrada antigua del instalador si existiera con el nombre viejo
    IF to_regclass('public.tmr_installation_history') IS NOT NULL THEN
        DELETE FROM public.tmr_installation_history
        WHERE script IN (
            '19-Inv_tmr_db_modulo_permisos_ausencias.sql',
            '20-Inv_tmr_db_solicitudes_normalizacion.sql'
        );
    END IF;
END $$;


-- ============================================================================
-- 1. EstadoSolicitud: time_report.tbl_time_report_estado_solicitud
-- ============================================================================
CREATE TABLE IF NOT EXISTS time_report.tbl_time_report_estado_solicitud (
    id           INT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    codigo       VARCHAR(30) NOT NULL,
    codigo_corto VARCHAR(5)  NOT NULL,
    nombre       VARCHAR(60) NOT NULL,
    semaforo     VARCHAR(15) NOT NULL,
    orden        SMALLINT    NOT NULL,
    activo       BOOLEAN     NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_tr_estado_solicitud_codigo       UNIQUE (codigo),
    CONSTRAINT uq_tr_estado_solicitud_codigo_corto UNIQUE (codigo_corto),
    CONSTRAINT ck_tr_estado_solicitud_semaforo     CHECK (semaforo IN ('AMARILLO', 'VERDE', 'ROJO'))
);

INSERT INTO time_report.tbl_time_report_estado_solicitud
    (codigo, codigo_corto, nombre, semaforo, orden, activo)
VALUES
    ('PENDIENTE_JEFE', 'PEN', 'Pendiente Jefe',    'AMARILLO', 1, TRUE),
    ('PENDIENTE_TH',   'RRH', 'En Talento Humano', 'AMARILLO', 2, TRUE),
    ('OBSERVADA',      'OBS', 'Observado',         'AMARILLO', 3, TRUE),
    ('APROBADA',       'CON', 'Confirmado',        'VERDE',    4, TRUE),
    ('RECHAZADA',      'REC', 'Rechazado',         'ROJO',     5, TRUE)
ON CONFLICT (codigo) DO UPDATE
SET codigo_corto = EXCLUDED.codigo_corto,
    nombre       = EXCLUDED.nombre,
    semaforo     = EXCLUDED.semaforo,
    orden        = EXCLUDED.orden,
    activo       = TRUE;


-- ============================================================================
-- 2. TipoPermiso: time_report.tbl_time_report_tipo_permiso
-- ============================================================================
CREATE TABLE IF NOT EXISTS time_report.tbl_time_report_tipo_permiso (
    id               INT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    codigo           VARCHAR(30)  NOT NULL,
    codigo_corto     VARCHAR(5)   NOT NULL,
    nombre           VARCHAR(80)  NOT NULL,
    descripcion      VARCHAR(200) NULL,
    requiere_adjunto BOOLEAN      NOT NULL DEFAULT FALSE,
    orden            SMALLINT     NOT NULL DEFAULT 1,
    activo           BOOLEAN      NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_tr_tipo_permiso_codigo       UNIQUE (codigo),
    CONSTRAINT uq_tr_tipo_permiso_codigo_corto UNIQUE (codigo_corto)
);

INSERT INTO time_report.tbl_time_report_tipo_permiso
    (codigo, codigo_corto, nombre, descripcion, requiere_adjunto, orden, activo)
VALUES
    ('VACACIONES',             'VAC', 'Vacaciones',                   'Vacaciones anuales programadas',          FALSE, 1, TRUE),
    ('LICENCIA_ENFERMEDAD',    'ENF', 'Licencia por enfermedad',      'Licencia médica (obligatorio >1d Nómina)',FALSE, 2, TRUE),
    ('CITAS_MEDICAS',          'CIT', 'Citas médicas',                'Permiso por cita o control médico',       FALSE, 3, TRUE),
    ('MATRIMONIO',             'MAT', 'Matrimonio',                   'Licencia por matrimonio civil',           FALSE, 4, TRUE),
    ('NACIMIENTO_HIJO',        'NAC', 'Nacimiento de un Hijo/a',      'Licencia por paternidad / nacimiento',    FALSE, 5, TRUE),
    ('OTROS',                  'OTR', 'Otros',                        'Otros permisos laborales o personales',   FALSE, 6, TRUE),
    ('MATERNIDAD',             'MTR', 'Maternidad',                   'Licencia legal por maternidad',           FALSE, 7, TRUE),
    ('FALLECIMIENTO_FAMILIAR', 'FAL', 'Fallecimiento de un familiar', 'Licencia por calamidad doméstica / duelo',FALSE, 8, TRUE)
ON CONFLICT (codigo) DO UPDATE
SET codigo_corto     = EXCLUDED.codigo_corto,
    nombre           = EXCLUDED.nombre,
    descripcion      = EXCLUDED.descripcion,
    requiere_adjunto = EXCLUDED.requiere_adjunto,
    orden            = EXCLUDED.orden,
    activo           = TRUE;


-- ============================================================================
-- 3. Solicitud (Cabecera en 3FN): time_report.tbl_time_report_solicitud
-- ============================================================================
CREATE TABLE IF NOT EXISTS time_report.tbl_time_report_solicitud (
    id                     INT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    codigo                 VARCHAR(30)   NOT NULL,
    id_empleado            INT           NOT NULL,
    id_asignacion_proyecto INT           NULL,
    id_proyecto            INT           NULL,
    id_cliente             INT           NULL,
    id_lider               INT           NULL,
    id_tipo_permiso        INT           NOT NULL,
    id_estado              INT           NOT NULL,
    fecha_solicitud        DATE          NOT NULL DEFAULT CURRENT_DATE,
    motivo                 TEXT          NOT NULL,
    fecha_inicio           DATE          NOT NULL,
    fecha_fin              DATE          NOT NULL,
    fecha_regreso          DATE          NOT NULL,
    dias_solicitados       NUMERIC(5, 2) NOT NULL,
    horario                VARCHAR(20)   NULL DEFAULT 'COMPLETA',
    detalle_horario        VARCHAR(120)  NULL,
    horas_solicitadas      NUMERIC(5, 2) NULL,
    impacto_timereport     VARCHAR(30)   NOT NULL DEFAULT 'RESTA_META',
    token_externo          VARCHAR(120)  NULL,
    token_externo_expira   TIMESTAMPTZ   NULL,
    token_externo_usado_en TIMESTAMPTZ   NULL,
    activo                 BOOLEAN       NOT NULL DEFAULT TRUE,
    usuario_creacion       VARCHAR(50)   NOT NULL,
    fecha_creacion         TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    usuario_modificacion   VARCHAR(50)   NULL,
    fecha_modificacion     TIMESTAMPTZ   NULL,
    ip_creacion            VARCHAR(45)   NOT NULL,
    ip_modificacion        VARCHAR(45)   NULL,

    CONSTRAINT uq_tr_solicitud_codigo UNIQUE (codigo),
    CONSTRAINT fk_tr_solicitud_empleado
        FOREIGN KEY (id_empleado) REFERENCES administracion.tbl_administracion_empleado(id),
    CONSTRAINT fk_tr_solicitud_asignacion_proyecto
        FOREIGN KEY (id_asignacion_proyecto) REFERENCES time_report.tbl_time_report_asignacion_proyecto(id) ON DELETE SET NULL,
    CONSTRAINT fk_tr_solicitud_proyecto
        FOREIGN KEY (id_proyecto) REFERENCES time_report.tbl_time_report_proyecto(id) ON DELETE SET NULL,
    CONSTRAINT fk_tr_solicitud_cliente
        FOREIGN KEY (id_cliente) REFERENCES administracion.tbl_administracion_cliente(id) ON DELETE SET NULL,
    CONSTRAINT fk_tr_solicitud_lider
        FOREIGN KEY (id_lider) REFERENCES administracion.tbl_administracion_lider(id) ON DELETE SET NULL,
    CONSTRAINT fk_tr_solicitud_tipo_permiso
        FOREIGN KEY (id_tipo_permiso) REFERENCES time_report.tbl_time_report_tipo_permiso(id),
    CONSTRAINT fk_tr_solicitud_estado
        FOREIGN KEY (id_estado) REFERENCES time_report.tbl_time_report_estado_solicitud(id),
    CONSTRAINT ck_tr_solicitud_fechas
        CHECK (fecha_fin >= fecha_inicio),
    CONSTRAINT ck_tr_solicitud_horario
        CHECK (horario IS NULL OR horario IN ('COMPLETA', 'MANANA', 'TARDE', 'OTROS')),
    CONSTRAINT ck_tr_solicitud_impacto
        CHECK (impacto_timereport IN ('RESTA_META', 'SUMA_REPORTADAS', 'SIN_IMPACTO'))
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_idx_tr_solicitud_token_externo
    ON time_report.tbl_time_report_solicitud (token_externo)
    WHERE token_externo IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_tr_solicitud_empleado
    ON time_report.tbl_time_report_solicitud (id_empleado, fecha_creacion DESC);

CREATE INDEX IF NOT EXISTS idx_tr_solicitud_lider_estado
    ON time_report.tbl_time_report_solicitud (id_lider, id_estado);

CREATE INDEX IF NOT EXISTS idx_tr_solicitud_estado
    ON time_report.tbl_time_report_solicitud (id_estado, fecha_creacion DESC);

CREATE INDEX IF NOT EXISTS idx_tr_solicitud_fechas
    ON time_report.tbl_time_report_solicitud (fecha_inicio, fecha_fin);


-- ============================================================================
-- 4. SolicitudRevision (Paso 2 Jefe / Paso 3 TH): time_report.tbl_time_report_solicitud_revision
-- ============================================================================
CREATE TABLE IF NOT EXISTS time_report.tbl_time_report_solicitud_revision (
    id                 INT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    id_solicitud       INT         NOT NULL,
    etapa_revision     VARCHAR(30) NOT NULL,
    id_usuario_revisor INT         NULL,
    metodo_revision    VARCHAR(30) NOT NULL DEFAULT 'CUENTA_TMR',
    decision           VARCHAR(20) NOT NULL,
    fecha_revision     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    comentario         TEXT        NULL,

    CONSTRAINT fk_tr_solicitud_revision_solicitud
        FOREIGN KEY (id_solicitud) REFERENCES time_report.tbl_time_report_solicitud(id) ON DELETE CASCADE,
    CONSTRAINT fk_tr_solicitud_revision_usuario
        FOREIGN KEY (id_usuario_revisor) REFERENCES autenticacion.tbl_autenticacion_usuario(id),
    CONSTRAINT ck_tr_solicitud_revision_etapa
        CHECK (etapa_revision IN ('JEFE_INMEDIATO', 'TALENTO_HUMANO')),
    CONSTRAINT ck_tr_solicitud_revision_metodo
        CHECK (metodo_revision IN ('CUENTA_TMR', 'MAGIC_LINK_EXTERNO')),
    CONSTRAINT ck_tr_solicitud_revision_decision
        CHECK (decision IN ('APROBADO', 'OBSERVADO', 'RECHAZADO')),
    -- RF-007: comentario obligatorio cuando se rechaza o se observa
    CONSTRAINT ck_tr_solicitud_revision_comentario
        CHECK (decision = 'APROBADO' OR NULLIF(BTRIM(comentario), '') IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS idx_tr_solicitud_revision_solicitud
    ON time_report.tbl_time_report_solicitud_revision (id_solicitud, etapa_revision, fecha_revision DESC);


-- ============================================================================
-- 5. SolicitudAdjunto (Archivos en MinIO S3): time_report.tbl_time_report_solicitud_adjunto
-- ============================================================================
CREATE TABLE IF NOT EXISTS time_report.tbl_time_report_solicitud_adjunto (
    id                    INT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    id_solicitud          INT          NOT NULL,
    nombre_original       VARCHAR(255) NOT NULL,
    bucket_name           VARCHAR(63)  NOT NULL,
    object_key            VARCHAR(255) NOT NULL,
    content_type          VARCHAR(100) NOT NULL,
    tamano_bytes          BIGINT       NOT NULL,
    sha256                CHAR(64)     NULL,
    subido_por_usuario_id INT          NOT NULL,
    activo                BOOLEAN      NOT NULL DEFAULT TRUE,
    fecha_creacion        TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_tr_solicitud_adjunto_solicitud
        FOREIGN KEY (id_solicitud) REFERENCES time_report.tbl_time_report_solicitud(id) ON DELETE CASCADE,
    CONSTRAINT fk_tr_solicitud_adjunto_usuario
        FOREIGN KEY (subido_por_usuario_id) REFERENCES autenticacion.tbl_autenticacion_usuario(id),
    -- RNF-003: PDF, PNG o JPG y máximo 5 MB (5,242,880 bytes)
    CONSTRAINT ck_tr_solicitud_adjunto_mime
        CHECK (content_type IN ('application/pdf', 'image/png', 'image/jpeg')),
    CONSTRAINT ck_tr_solicitud_adjunto_tamano
        CHECK (tamano_bytes BETWEEN 1 AND 5242880)
);

CREATE INDEX IF NOT EXISTS idx_tr_solicitud_adjunto_solicitud
    ON time_report.tbl_time_report_solicitud_adjunto (id_solicitud)
    WHERE activo = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS uq_idx_tr_solicitud_adjunto_objeto
    ON time_report.tbl_time_report_solicitud_adjunto (bucket_name, object_key);


-- ============================================================================
-- 6. NotificacionUsuario (Campana SSE RF-010): notificacion.notificacion_usuario
-- ============================================================================
CREATE TABLE IF NOT EXISTS notificacion.notificacion_usuario (
    id                 BIGINT GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    id_usuario_destino INT          NOT NULL,
    id_solicitud       INT          NULL,
    titulo             VARCHAR(160) NOT NULL,
    mensaje            VARCHAR(500) NOT NULL,
    url_destino        VARCHAR(255) NULL,
    leida              BOOLEAN      NOT NULL DEFAULT FALSE,
    fecha_lectura      TIMESTAMPTZ  NULL,
    fecha_creacion     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_notificacion_usuario_destino
        FOREIGN KEY (id_usuario_destino) REFERENCES autenticacion.tbl_autenticacion_usuario(id) ON DELETE CASCADE,
    CONSTRAINT fk_notificacion_usuario_solicitud
        FOREIGN KEY (id_solicitud) REFERENCES time_report.tbl_time_report_solicitud(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_notificacion_usuario_destino_fecha
    ON notificacion.notificacion_usuario (id_usuario_destino, fecha_creacion DESC);

CREATE INDEX IF NOT EXISTS idx_notificacion_usuario_no_leidas
    ON notificacion.notificacion_usuario (id_usuario_destino)
    WHERE leida = FALSE;


-- ============================================================================
-- 7. Módulos y Permisos RBAC para el menú lateral "/permisos" (sin tocar DDL)
-- ============================================================================
DO $$
DECLARE
    v_padre_id          INT;
    v_mod_mis_permisos  INT;
    v_mod_aprob_lider   INT;
    v_mod_val_rrhh      INT;
    v_mod_hist_gerencia INT;
    v_perm_id           INT;
BEGIN
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

    -- Submódulo 1: Mis Permisos (todos los roles activos)
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
        (v_mod_mis_permisos, 'MIS_PERMISOS_READ', 'READ', 'Permite ver sus propias solicitudes de permiso', TRUE, 'SYSTEM', '127.0.0.1'),
        (v_mod_mis_permisos, 'MIS_PERMISOS_CREATE', 'CREATE', 'Permite registrar solicitudes de permiso y vacaciones', TRUE, 'SYSTEM', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE SET idmodulo = EXCLUDED.idmodulo, activo = TRUE;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT r.id, p.id, TRUE, 'SYSTEM', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    JOIN autenticacion.tbl_autenticacion_permiso p ON p.idmodulo = v_mod_mis_permisos
    WHERE r.activo = TRUE
    ON CONFLICT (idrol, idpermiso) DO UPDATE SET activo = TRUE;

    -- Submódulo 2: Aprobacion Lider
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
        (v_mod_aprob_lider, 'PERMISOS_LIDER_APPROVE', 'UPDATE', 'Permite aprobar, observar o rechazar solicitudes del equipo', TRUE, 'SYSTEM', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE SET idmodulo = EXCLUDED.idmodulo, activo = TRUE;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT r.id, p.id, TRUE, 'SYSTEM', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    JOIN autenticacion.tbl_autenticacion_permiso p ON p.idmodulo = v_mod_aprob_lider
    WHERE r.activo = TRUE
      AND UPPER(r.nombre) IN ('ADMINISTRADOR', 'LIDER', 'LÍDER')
    ON CONFLICT (idrol, idpermiso) DO UPDATE SET activo = TRUE;

    -- Submódulo 3: Validacion RRHH
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

    -- Submódulo 4: Historial Gerencia
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
        (v_mod_hist_gerencia, 'PERMISOS_GERENCIA_READ', 'READ', 'Permite consultar el historial general de solicitudes', TRUE, 'SYSTEM', '127.0.0.1')
    ON CONFLICT (codigo) DO UPDATE SET idmodulo = EXCLUDED.idmodulo, activo = TRUE;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso (idrol, idpermiso, activo, usuariocreacion, ipcreacion)
    SELECT r.id, p.id, TRUE, 'SYSTEM', '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol r
    JOIN autenticacion.tbl_autenticacion_permiso p ON p.idmodulo = v_mod_hist_gerencia
    WHERE r.activo = TRUE
      AND UPPER(r.nombre) IN ('ADMINISTRADOR', 'GERENCIA', 'GERENTE', 'RECURSOS HUMANOS', 'RECURSOS_HUMANOS')
    ON CONFLICT (idrol, idpermiso) DO UPDATE SET activo = TRUE;
END $$;

COMMIT;

