/*
  Instalación manual del módulo Mis datos.
  Derivado de las migraciones:
    20261111160000_AddActualizacionDatosColaboradores
    20261111173000_AddInformacionFamiliar
    20261111180000_AddTieneHijos
    20261112120000_ActualizacionDirectaYSolicitudes

  Este script no crea public, __EFMigrationsHistory ni
  public.tmr_installation_history. No elimina ni reinicializa datos.
*/

BEGIN;

DO $$
BEGIN
    IF to_regclass('administracion.tbl_administracion_empleado') IS NULL
       OR NOT EXISTS (
           SELECT 1 FROM information_schema.columns
           WHERE table_schema = 'administracion'
             AND table_name = 'tbl_administracion_empleado'
             AND column_name = 'id'
       ) THEN
        RAISE EXCEPTION 'INCONSISTENCIA: falta administracion.tbl_administracion_empleado(id), dependencia de Mis datos';
    END IF;

END $$;

/* Migración 20261111160000 */
CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_solicitud (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id),
    estado varchar(20) NOT NULL,
    observacion text NULL,
    idrevisor integer NULL,
    fechacreacion timestamptz NOT NULL,
    fechamodificacion timestamptz NOT NULL,
    fecharevision timestamptz NULL,
    version integer NOT NULL,
    datospropuestosjson jsonb NOT NULL
);

CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_detalle (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE,
    seccion varchar(40) NOT NULL,
    campo varchar(80) NOT NULL,
    valorvigente text NULL,
    valorpropuesto text NULL
);

CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_formacion (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE,
    nivel varchar(80) NOT NULL,
    institucion varchar(200) NOT NULL,
    titulo varchar(200) NOT NULL,
    fechainicio date NULL,
    fechafin date NULL
);

CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_certificacion (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE,
    nombre varchar(200) NOT NULL,
    entidademisora varchar(200) NOT NULL,
    fechaemision date NOT NULL,
    fechavencimiento date NULL
);

CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_formacion (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id),
    nivel varchar(80) NOT NULL,
    institucion varchar(200) NOT NULL,
    titulo varchar(200) NOT NULL,
    fechainicio date NULL,
    fechafin date NULL,
    activo boolean NOT NULL
);

CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_certificacion (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id),
    nombre varchar(200) NOT NULL,
    entidademisora varchar(200) NOT NULL,
    fechaemision date NOT NULL,
    fechavencimiento date NULL,
    activo boolean NOT NULL
);

CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_archivo (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE,
    nombreoriginal text NOT NULL,
    contenttype text NOT NULL,
    tamano bigint NOT NULL,
    storagekey varchar(255) NOT NULL,
    provider varchar(30) NOT NULL,
    sha256 text NOT NULL,
    fechacreacion timestamptz NOT NULL,
    seccion varchar(40) NOT NULL DEFAULT 'General',
    referenciaid bigint NULL
);

CREATE INDEX IF NOT EXISTS ix_actualizacion_solicitud_empleado_estado
    ON administracion.tbl_actualizacion_solicitud (idempleado, estado);
CREATE INDEX IF NOT EXISTS ix_actualizacion_solicitud_empleado_version
    ON administracion.tbl_actualizacion_solicitud (idempleado, version);
CREATE UNIQUE INDEX IF NOT EXISTS ix_actualizacion_archivo_solicitud_key
    ON administracion.tbl_actualizacion_archivo (idsolicitud, storagekey);
CREATE INDEX IF NOT EXISTS ix_colaborador_formacion_empleado_activo
    ON administracion.tbl_colaborador_formacion (idempleado, activo);
CREATE INDEX IF NOT EXISTS ix_colaborador_certificacion_empleado_activo
    ON administracion.tbl_colaborador_certificacion (idempleado, activo);

/* Migración 20261111173000 */
CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_informacion_familiar (
    idempleado integer PRIMARY KEY REFERENCES administracion.tbl_administracion_empleado(id),
    estadocivil varchar(80) NULL,
    tienediscapacidad varchar(20) NULL,
    tipodiscapacidad varchar(120) NULL,
    porcentajediscapacidad numeric(5,2) NULL,
    CONSTRAINT ck_colab_familiar_discapacidad CHECK (
        tienediscapacidad IS NULL OR tienediscapacidad IN ('Si','No','NoInformado')
    ),
    CONSTRAINT ck_colab_familiar_porcentaje CHECK (
        porcentajediscapacidad IS NULL OR (porcentajediscapacidad >= 0 AND porcentajediscapacidad <= 100)
    )
);

CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_familiar (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idempleado integer NOT NULL REFERENCES administracion.tbl_colaborador_informacion_familiar(idempleado),
    tiporelacion varchar(30) NOT NULL,
    nombres varchar(100) NOT NULL,
    apellidos varchar(100) NOT NULL,
    fechanacimiento date NULL,
    tienediscapacidad varchar(20) NULL,
    tipodiscapacidad varchar(120) NULL,
    porcentajediscapacidad numeric(5,2) NULL,
    activo boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_colab_familiar_hijo_discapacidad CHECK (
        tienediscapacidad IS NULL OR tienediscapacidad IN ('Si','No','NoInformado')
    ),
    CONSTRAINT ck_colab_familiar_hijo_porcentaje CHECK (
        porcentajediscapacidad IS NULL OR (porcentajediscapacidad >= 0 AND porcentajediscapacidad <= 100)
    )
);

CREATE TABLE IF NOT EXISTS administracion.tbl_actualizacion_familiar (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idsolicitud bigint NOT NULL REFERENCES administracion.tbl_actualizacion_solicitud(id) ON DELETE CASCADE,
    tiporelacion varchar(30) NOT NULL,
    nombres varchar(100) NOT NULL,
    apellidos varchar(100) NOT NULL,
    fechanacimiento date NULL,
    tienediscapacidad varchar(20) NULL,
    tipodiscapacidad varchar(120) NULL,
    porcentajediscapacidad numeric(5,2) NULL,
    CONSTRAINT ck_actualizacion_familiar_discapacidad CHECK (
        tienediscapacidad IS NULL OR tienediscapacidad IN ('Si','No','NoInformado')
    ),
    CONSTRAINT ck_actualizacion_familiar_porcentaje CHECK (
        porcentajediscapacidad IS NULL OR (porcentajediscapacidad >= 0 AND porcentajediscapacidad <= 100)
    )
);

ALTER TABLE administracion.tbl_actualizacion_archivo
    ADD COLUMN IF NOT EXISTS seccion varchar(40) NOT NULL DEFAULT 'General';
ALTER TABLE administracion.tbl_actualizacion_archivo
    ADD COLUMN IF NOT EXISTS referenciaid bigint NULL;

CREATE INDEX IF NOT EXISTS ix_colaborador_familiar_empleado_activo
    ON administracion.tbl_colaborador_familiar (idempleado, activo);
CREATE INDEX IF NOT EXISTS ix_actualizacion_familiar_solicitud
    ON administracion.tbl_actualizacion_familiar (idsolicitud);

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'administracion.tbl_colaborador_informacion_familiar'::regclass
          AND conname = 'ck_colab_familiar_discapacidad'
    ) THEN
        ALTER TABLE administracion.tbl_colaborador_informacion_familiar
            ADD CONSTRAINT ck_colab_familiar_discapacidad CHECK (
                tienediscapacidad IS NULL OR tienediscapacidad IN ('Si','No','NoInformado')
            );
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'administracion.tbl_colaborador_informacion_familiar'::regclass
          AND conname = 'ck_colab_familiar_porcentaje'
    ) THEN
        ALTER TABLE administracion.tbl_colaborador_informacion_familiar
            ADD CONSTRAINT ck_colab_familiar_porcentaje CHECK (
                porcentajediscapacidad IS NULL OR (porcentajediscapacidad >= 0 AND porcentajediscapacidad <= 100)
            );
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'administracion.tbl_colaborador_familiar'::regclass
          AND conname = 'ck_colab_familiar_hijo_discapacidad'
    ) THEN
        ALTER TABLE administracion.tbl_colaborador_familiar
            ADD CONSTRAINT ck_colab_familiar_hijo_discapacidad CHECK (
                tienediscapacidad IS NULL OR tienediscapacidad IN ('Si','No','NoInformado')
            );
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'administracion.tbl_colaborador_familiar'::regclass
          AND conname = 'ck_colab_familiar_hijo_porcentaje'
    ) THEN
        ALTER TABLE administracion.tbl_colaborador_familiar
            ADD CONSTRAINT ck_colab_familiar_hijo_porcentaje CHECK (
                porcentajediscapacidad IS NULL OR (porcentajediscapacidad >= 0 AND porcentajediscapacidad <= 100)
            );
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'administracion.tbl_actualizacion_familiar'::regclass
          AND conname = 'ck_actualizacion_familiar_discapacidad'
    ) THEN
        ALTER TABLE administracion.tbl_actualizacion_familiar
            ADD CONSTRAINT ck_actualizacion_familiar_discapacidad CHECK (
                tienediscapacidad IS NULL OR tienediscapacidad IN ('Si','No','NoInformado')
            );
    END IF;
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'administracion.tbl_actualizacion_familiar'::regclass
          AND conname = 'ck_actualizacion_familiar_porcentaje'
    ) THEN
        ALTER TABLE administracion.tbl_actualizacion_familiar
            ADD CONSTRAINT ck_actualizacion_familiar_porcentaje CHECK (
                porcentajediscapacidad IS NULL OR (porcentajediscapacidad >= 0 AND porcentajediscapacidad <= 100)
            );
    END IF;
END $$;

/* Migración 20261111180000 */
ALTER TABLE administracion.tbl_colaborador_informacion_familiar
    ADD COLUMN IF NOT EXISTS tienehijos varchar(20) NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'administracion.tbl_colaborador_informacion_familiar'::regclass
          AND conname = 'ck_colab_familiar_tienehijos'
    ) THEN
        ALTER TABLE administracion.tbl_colaborador_informacion_familiar
            ADD CONSTRAINT ck_colab_familiar_tienehijos CHECK (
                tienehijos IS NULL OR tienehijos IN ('Si','No','NoInformado')
            );
    END IF;
END $$;

/* Migración 20261112120000 */
ALTER TABLE administracion.tbl_colaborador_formacion
    ADD COLUMN IF NOT EXISTS clave varchar(40);
UPDATE administracion.tbl_colaborador_formacion
SET clave = md5(random()::text || id::text)
WHERE clave IS NULL;
ALTER TABLE administracion.tbl_colaborador_formacion ALTER COLUMN clave SET NOT NULL;

ALTER TABLE administracion.tbl_colaborador_certificacion
    ADD COLUMN IF NOT EXISTS clave varchar(40);
UPDATE administracion.tbl_colaborador_certificacion
SET clave = md5(random()::text || id::text)
WHERE clave IS NULL;
ALTER TABLE administracion.tbl_colaborador_certificacion ALTER COLUMN clave SET NOT NULL;

ALTER TABLE administracion.tbl_colaborador_familiar
    ADD COLUMN IF NOT EXISTS identificacion varchar(40),
    ADD COLUMN IF NOT EXISTS clave varchar(40);
UPDATE administracion.tbl_colaborador_familiar
SET clave = md5(random()::text || id::text)
WHERE clave IS NULL;
ALTER TABLE administracion.tbl_colaborador_familiar ALTER COLUMN clave SET NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_colab_formacion_empleado_clave
    ON administracion.tbl_colaborador_formacion (idempleado, clave);
CREATE UNIQUE INDEX IF NOT EXISTS ux_colab_cert_empleado_clave
    ON administracion.tbl_colaborador_certificacion (idempleado, clave);
CREATE UNIQUE INDEX IF NOT EXISTS ux_colab_familiar_empleado_clave
    ON administracion.tbl_colaborador_familiar (idempleado, clave);

CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_datos_control (
    idempleado integer PRIMARY KEY REFERENCES administracion.tbl_administracion_empleado(id),
    version bigint NOT NULL DEFAULT 1,
    fechamodificacion timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_documento (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id),
    seccion varchar(40) NOT NULL,
    referenciaid bigint NULL,
    referenciaclave varchar(80) NULL,
    nombreoriginal varchar(255) NOT NULL,
    contenttype varchar(120) NOT NULL,
    tamano bigint NOT NULL,
    storagekey varchar(255) NOT NULL,
    provider varchar(30) NOT NULL,
    sha256 varchar(128) NOT NULL,
    fechacreacion timestamptz NOT NULL,
    activo boolean NOT NULL DEFAULT true
);
CREATE INDEX IF NOT EXISTS ix_colaborador_documento_owner
    ON administracion.tbl_colaborador_documento (idempleado, seccion, referenciaid, referenciaclave);

CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_solicitud (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idempleado integer NOT NULL REFERENCES administracion.tbl_administracion_empleado(id),
    asunto varchar(200) NOT NULL,
    descripcion varchar(4000) NOT NULL,
    fechacreacion timestamptz NOT NULL,
    fechaenvio timestamptz NULL,
    enviada boolean NOT NULL DEFAULT false
);
CREATE INDEX IF NOT EXISTS ix_colaborador_solicitud_owner
    ON administracion.tbl_colaborador_solicitud (idempleado, fechacreacion);

CREATE TABLE IF NOT EXISTS administracion.tbl_colaborador_solicitud_archivo (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    idsolicitud bigint NOT NULL REFERENCES administracion.tbl_colaborador_solicitud(id) ON DELETE CASCADE,
    nombreoriginal varchar(255) NOT NULL,
    contenttype varchar(120) NOT NULL,
    tamano bigint NOT NULL,
    storagekey varchar(255) NOT NULL,
    provider varchar(30) NOT NULL,
    sha256 varchar(128) NOT NULL,
    fechacreacion timestamptz NOT NULL
);

/* Las tablas se validan y confirman antes de tocar permisos. */

/* Validaciones estructurales y de relaciones antes de confirmar. */
DO $$
DECLARE
    r record;
    v_missing text;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('tbl_actualizacion_solicitud','id'),('tbl_actualizacion_solicitud','idempleado'),('tbl_actualizacion_solicitud','estado'),('tbl_actualizacion_solicitud','observacion'),('tbl_actualizacion_solicitud','idrevisor'),('tbl_actualizacion_solicitud','fechacreacion'),('tbl_actualizacion_solicitud','fechamodificacion'),('tbl_actualizacion_solicitud','fecharevision'),('tbl_actualizacion_solicitud','version'),('tbl_actualizacion_solicitud','datospropuestosjson'),
            ('tbl_actualizacion_detalle','id'),('tbl_actualizacion_detalle','idsolicitud'),('tbl_actualizacion_detalle','seccion'),('tbl_actualizacion_detalle','campo'),('tbl_actualizacion_detalle','valorvigente'),('tbl_actualizacion_detalle','valorpropuesto'),
            ('tbl_actualizacion_formacion','id'),('tbl_actualizacion_formacion','idsolicitud'),('tbl_actualizacion_formacion','nivel'),('tbl_actualizacion_formacion','institucion'),('tbl_actualizacion_formacion','titulo'),('tbl_actualizacion_formacion','fechainicio'),('tbl_actualizacion_formacion','fechafin'),
            ('tbl_actualizacion_certificacion','id'),('tbl_actualizacion_certificacion','idsolicitud'),('tbl_actualizacion_certificacion','nombre'),('tbl_actualizacion_certificacion','entidademisora'),('tbl_actualizacion_certificacion','fechaemision'),('tbl_actualizacion_certificacion','fechavencimiento'),
            ('tbl_colaborador_formacion','id'),('tbl_colaborador_formacion','idempleado'),('tbl_colaborador_formacion','nivel'),('tbl_colaborador_formacion','institucion'),('tbl_colaborador_formacion','titulo'),('tbl_colaborador_formacion','fechainicio'),('tbl_colaborador_formacion','fechafin'),('tbl_colaborador_formacion','activo'),('tbl_colaborador_formacion','clave'),
            ('tbl_colaborador_certificacion','id'),('tbl_colaborador_certificacion','idempleado'),('tbl_colaborador_certificacion','nombre'),('tbl_colaborador_certificacion','entidademisora'),('tbl_colaborador_certificacion','fechaemision'),('tbl_colaborador_certificacion','fechavencimiento'),('tbl_colaborador_certificacion','activo'),('tbl_colaborador_certificacion','clave'),
            ('tbl_actualizacion_archivo','id'),('tbl_actualizacion_archivo','idsolicitud'),('tbl_actualizacion_archivo','nombreoriginal'),('tbl_actualizacion_archivo','contenttype'),('tbl_actualizacion_archivo','tamano'),('tbl_actualizacion_archivo','storagekey'),('tbl_actualizacion_archivo','provider'),('tbl_actualizacion_archivo','sha256'),('tbl_actualizacion_archivo','fechacreacion'),('tbl_actualizacion_archivo','seccion'),('tbl_actualizacion_archivo','referenciaid'),
            ('tbl_colaborador_informacion_familiar','idempleado'),('tbl_colaborador_informacion_familiar','estadocivil'),('tbl_colaborador_informacion_familiar','tienehijos'),('tbl_colaborador_informacion_familiar','tienediscapacidad'),('tbl_colaborador_informacion_familiar','tipodiscapacidad'),('tbl_colaborador_informacion_familiar','porcentajediscapacidad'),
            ('tbl_colaborador_familiar','id'),('tbl_colaborador_familiar','idempleado'),('tbl_colaborador_familiar','tiporelacion'),('tbl_colaborador_familiar','nombres'),('tbl_colaborador_familiar','apellidos'),('tbl_colaborador_familiar','fechanacimiento'),('tbl_colaborador_familiar','tienediscapacidad'),('tbl_colaborador_familiar','tipodiscapacidad'),('tbl_colaborador_familiar','porcentajediscapacidad'),('tbl_colaborador_familiar','activo'),('tbl_colaborador_familiar','identificacion'),('tbl_colaborador_familiar','clave'),
            ('tbl_actualizacion_familiar','id'),('tbl_actualizacion_familiar','idsolicitud'),('tbl_actualizacion_familiar','tiporelacion'),('tbl_actualizacion_familiar','nombres'),('tbl_actualizacion_familiar','apellidos'),('tbl_actualizacion_familiar','fechanacimiento'),('tbl_actualizacion_familiar','tienediscapacidad'),('tbl_actualizacion_familiar','tipodiscapacidad'),('tbl_actualizacion_familiar','porcentajediscapacidad'),
            ('tbl_colaborador_datos_control','idempleado'),('tbl_colaborador_datos_control','version'),('tbl_colaborador_datos_control','fechamodificacion'),
            ('tbl_colaborador_documento','id'),('tbl_colaborador_documento','idempleado'),('tbl_colaborador_documento','seccion'),('tbl_colaborador_documento','referenciaid'),('tbl_colaborador_documento','referenciaclave'),('tbl_colaborador_documento','nombreoriginal'),('tbl_colaborador_documento','contenttype'),('tbl_colaborador_documento','tamano'),('tbl_colaborador_documento','storagekey'),('tbl_colaborador_documento','provider'),('tbl_colaborador_documento','sha256'),('tbl_colaborador_documento','fechacreacion'),('tbl_colaborador_documento','activo'),
            ('tbl_colaborador_solicitud','id'),('tbl_colaborador_solicitud','idempleado'),('tbl_colaborador_solicitud','asunto'),('tbl_colaborador_solicitud','descripcion'),('tbl_colaborador_solicitud','fechacreacion'),('tbl_colaborador_solicitud','fechaenvio'),('tbl_colaborador_solicitud','enviada'),
            ('tbl_colaborador_solicitud_archivo','id'),('tbl_colaborador_solicitud_archivo','idsolicitud'),('tbl_colaborador_solicitud_archivo','nombreoriginal'),('tbl_colaborador_solicitud_archivo','contenttype'),('tbl_colaborador_solicitud_archivo','tamano'),('tbl_colaborador_solicitud_archivo','storagekey'),('tbl_colaborador_solicitud_archivo','provider'),('tbl_colaborador_solicitud_archivo','sha256'),('tbl_colaborador_solicitud_archivo','fechacreacion')
        ) AS x(table_name, column_name)
    LOOP
        IF to_regclass(format('administracion.%I', r.table_name)) IS NULL THEN
            RAISE EXCEPTION 'VALIDACIÓN FALLIDA: falta la tabla administracion.%', r.table_name;
        END IF;
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'administracion'
              AND table_name = r.table_name
              AND column_name = r.column_name
        ) THEN
            RAISE EXCEPTION 'VALIDACIÓN FALLIDA: falta administracion.%.%', r.table_name, r.column_name;
        END IF;
    END LOOP;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'administracion.tbl_actualizacion_solicitud'::regclass AND confrelid = 'administracion.tbl_administracion_empleado'::regclass AND contype = 'f')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'administracion.tbl_colaborador_informacion_familiar'::regclass AND confrelid = 'administracion.tbl_administracion_empleado'::regclass AND contype = 'f')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'administracion.tbl_colaborador_familiar'::regclass AND confrelid = 'administracion.tbl_colaborador_informacion_familiar'::regclass AND contype = 'f')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'administracion.tbl_colaborador_datos_control'::regclass AND confrelid = 'administracion.tbl_administracion_empleado'::regclass AND contype = 'f')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'administracion.tbl_colaborador_documento'::regclass AND confrelid = 'administracion.tbl_administracion_empleado'::regclass AND contype = 'f')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'administracion.tbl_colaborador_solicitud'::regclass AND confrelid = 'administracion.tbl_administracion_empleado'::regclass AND contype = 'f') THEN
        RAISE EXCEPTION 'VALIDACIÓN FALLIDA: falta una relación de Mis datos con su tabla padre';
    END IF;

END $$;

/* Compatibilidad estricta con el DDL de las migraciones y el modelo EF. */
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('tbl_actualizacion_solicitud','id','bigint',NULL,'NO','YES'),('tbl_actualizacion_solicitud','idempleado','integer',NULL,'NO','NO'),('tbl_actualizacion_solicitud','estado','character varying',20,'NO','NO'),('tbl_actualizacion_solicitud','observacion','text',NULL,'YES','NO'),('tbl_actualizacion_solicitud','idrevisor','integer',NULL,'YES','NO'),('tbl_actualizacion_solicitud','fechacreacion','timestamp with time zone',NULL,'NO','NO'),('tbl_actualizacion_solicitud','fechamodificacion','timestamp with time zone',NULL,'NO','NO'),('tbl_actualizacion_solicitud','fecharevision','timestamp with time zone',NULL,'YES','NO'),('tbl_actualizacion_solicitud','version','integer',NULL,'NO','NO'),('tbl_actualizacion_solicitud','datospropuestosjson','jsonb',NULL,'NO','NO'),
            ('tbl_actualizacion_detalle','id','bigint',NULL,'NO','YES'),('tbl_actualizacion_detalle','idsolicitud','bigint',NULL,'NO','NO'),('tbl_actualizacion_detalle','seccion','character varying',40,'NO','NO'),('tbl_actualizacion_detalle','campo','character varying',80,'NO','NO'),('tbl_actualizacion_detalle','valorvigente','text',NULL,'YES','NO'),('tbl_actualizacion_detalle','valorpropuesto','text',NULL,'YES','NO'),
            ('tbl_actualizacion_formacion','id','bigint',NULL,'NO','YES'),('tbl_actualizacion_formacion','idsolicitud','bigint',NULL,'NO','NO'),('tbl_actualizacion_formacion','nivel','character varying',80,'NO','NO'),('tbl_actualizacion_formacion','institucion','character varying',200,'NO','NO'),('tbl_actualizacion_formacion','titulo','character varying',200,'NO','NO'),('tbl_actualizacion_formacion','fechainicio','date',NULL,'YES','NO'),('tbl_actualizacion_formacion','fechafin','date',NULL,'YES','NO'),
            ('tbl_actualizacion_certificacion','id','bigint',NULL,'NO','YES'),('tbl_actualizacion_certificacion','idsolicitud','bigint',NULL,'NO','NO'),('tbl_actualizacion_certificacion','nombre','character varying',200,'NO','NO'),('tbl_actualizacion_certificacion','entidademisora','character varying',200,'NO','NO'),('tbl_actualizacion_certificacion','fechaemision','date',NULL,'NO','NO'),('tbl_actualizacion_certificacion','fechavencimiento','date',NULL,'YES','NO'),
            ('tbl_colaborador_formacion','id','bigint',NULL,'NO','YES'),('tbl_colaborador_formacion','idempleado','integer',NULL,'NO','NO'),('tbl_colaborador_formacion','nivel','character varying',80,'NO','NO'),('tbl_colaborador_formacion','institucion','character varying',200,'NO','NO'),('tbl_colaborador_formacion','titulo','character varying',200,'NO','NO'),('tbl_colaborador_formacion','fechainicio','date',NULL,'YES','NO'),('tbl_colaborador_formacion','fechafin','date',NULL,'YES','NO'),('tbl_colaborador_formacion','activo','boolean',NULL,'NO','NO'),('tbl_colaborador_formacion','clave','character varying',40,'NO','NO'),
            ('tbl_colaborador_certificacion','id','bigint',NULL,'NO','YES'),('tbl_colaborador_certificacion','idempleado','integer',NULL,'NO','NO'),('tbl_colaborador_certificacion','nombre','character varying',200,'NO','NO'),('tbl_colaborador_certificacion','entidademisora','character varying',200,'NO','NO'),('tbl_colaborador_certificacion','fechaemision','date',NULL,'NO','NO'),('tbl_colaborador_certificacion','fechavencimiento','date',NULL,'YES','NO'),('tbl_colaborador_certificacion','activo','boolean',NULL,'NO','NO'),('tbl_colaborador_certificacion','clave','character varying',40,'NO','NO'),
            ('tbl_actualizacion_archivo','id','bigint',NULL,'NO','YES'),('tbl_actualizacion_archivo','idsolicitud','bigint',NULL,'NO','NO'),('tbl_actualizacion_archivo','nombreoriginal','text',NULL,'NO','NO'),('tbl_actualizacion_archivo','contenttype','text',NULL,'NO','NO'),('tbl_actualizacion_archivo','tamano','bigint',NULL,'NO','NO'),('tbl_actualizacion_archivo','storagekey','character varying',255,'NO','NO'),('tbl_actualizacion_archivo','provider','character varying',30,'NO','NO'),('tbl_actualizacion_archivo','sha256','text',NULL,'NO','NO'),('tbl_actualizacion_archivo','fechacreacion','timestamp with time zone',NULL,'NO','NO'),('tbl_actualizacion_archivo','seccion','character varying',40,'NO','NO'),('tbl_actualizacion_archivo','referenciaid','bigint',NULL,'YES','NO'),
            ('tbl_colaborador_informacion_familiar','idempleado','integer',NULL,'NO','NO'),('tbl_colaborador_informacion_familiar','estadocivil','character varying',80,'YES','NO'),('tbl_colaborador_informacion_familiar','tienehijos','character varying',20,'YES','NO'),('tbl_colaborador_informacion_familiar','tienediscapacidad','character varying',20,'YES','NO'),('tbl_colaborador_informacion_familiar','tipodiscapacidad','character varying',120,'YES','NO'),('tbl_colaborador_informacion_familiar','porcentajediscapacidad','numeric',NULL,'YES','NO'),
            ('tbl_colaborador_familiar','id','bigint',NULL,'NO','YES'),('tbl_colaborador_familiar','idempleado','integer',NULL,'NO','NO'),('tbl_colaborador_familiar','tiporelacion','character varying',30,'NO','NO'),('tbl_colaborador_familiar','nombres','character varying',100,'NO','NO'),('tbl_colaborador_familiar','apellidos','character varying',100,'NO','NO'),('tbl_colaborador_familiar','fechanacimiento','date',NULL,'YES','NO'),('tbl_colaborador_familiar','tienediscapacidad','character varying',20,'YES','NO'),('tbl_colaborador_familiar','tipodiscapacidad','character varying',120,'YES','NO'),('tbl_colaborador_familiar','porcentajediscapacidad','numeric',NULL,'YES','NO'),('tbl_colaborador_familiar','activo','boolean',NULL,'NO','NO'),('tbl_colaborador_familiar','identificacion','character varying',40,'YES','NO'),('tbl_colaborador_familiar','clave','character varying',40,'NO','NO'),
            ('tbl_actualizacion_familiar','id','bigint',NULL,'NO','YES'),('tbl_actualizacion_familiar','idsolicitud','bigint',NULL,'NO','NO'),('tbl_actualizacion_familiar','tiporelacion','character varying',30,'NO','NO'),('tbl_actualizacion_familiar','nombres','character varying',100,'NO','NO'),('tbl_actualizacion_familiar','apellidos','character varying',100,'NO','NO'),('tbl_actualizacion_familiar','fechanacimiento','date',NULL,'YES','NO'),('tbl_actualizacion_familiar','tienediscapacidad','character varying',20,'YES','NO'),('tbl_actualizacion_familiar','tipodiscapacidad','character varying',120,'YES','NO'),('tbl_actualizacion_familiar','porcentajediscapacidad','numeric',NULL,'YES','NO'),
            ('tbl_colaborador_datos_control','idempleado','integer',NULL,'NO','NO'),('tbl_colaborador_datos_control','version','bigint',NULL,'NO','NO'),('tbl_colaborador_datos_control','fechamodificacion','timestamp with time zone',NULL,'NO','NO'),
            ('tbl_colaborador_documento','id','bigint',NULL,'NO','YES'),('tbl_colaborador_documento','idempleado','integer',NULL,'NO','NO'),('tbl_colaborador_documento','seccion','character varying',40,'NO','NO'),('tbl_colaborador_documento','referenciaid','bigint',NULL,'YES','NO'),('tbl_colaborador_documento','referenciaclave','character varying',80,'YES','NO'),('tbl_colaborador_documento','nombreoriginal','character varying',255,'NO','NO'),('tbl_colaborador_documento','contenttype','character varying',120,'NO','NO'),('tbl_colaborador_documento','tamano','bigint',NULL,'NO','NO'),('tbl_colaborador_documento','storagekey','character varying',255,'NO','NO'),('tbl_colaborador_documento','provider','character varying',30,'NO','NO'),('tbl_colaborador_documento','sha256','character varying',128,'NO','NO'),('tbl_colaborador_documento','fechacreacion','timestamp with time zone',NULL,'NO','NO'),('tbl_colaborador_documento','activo','boolean',NULL,'NO','NO'),
            ('tbl_colaborador_solicitud','id','bigint',NULL,'NO','YES'),('tbl_colaborador_solicitud','idempleado','integer',NULL,'NO','NO'),('tbl_colaborador_solicitud','asunto','character varying',200,'NO','NO'),('tbl_colaborador_solicitud','descripcion','character varying',4000,'NO','NO'),('tbl_colaborador_solicitud','fechacreacion','timestamp with time zone',NULL,'NO','NO'),('tbl_colaborador_solicitud','fechaenvio','timestamp with time zone',NULL,'YES','NO'),('tbl_colaborador_solicitud','enviada','boolean',NULL,'NO','NO'),
            ('tbl_colaborador_solicitud_archivo','id','bigint',NULL,'NO','YES'),('tbl_colaborador_solicitud_archivo','idsolicitud','bigint',NULL,'NO','NO'),('tbl_colaborador_solicitud_archivo','nombreoriginal','character varying',255,'NO','NO'),('tbl_colaborador_solicitud_archivo','contenttype','character varying',120,'NO','NO'),('tbl_colaborador_solicitud_archivo','tamano','bigint',NULL,'NO','NO'),('tbl_colaborador_solicitud_archivo','storagekey','character varying',255,'NO','NO'),('tbl_colaborador_solicitud_archivo','provider','character varying',30,'NO','NO'),('tbl_colaborador_solicitud_archivo','sha256','character varying',128,'NO','NO'),('tbl_colaborador_solicitud_archivo','fechacreacion','timestamp with time zone',NULL,'NO','NO')
        ) AS x(table_name, column_name, expected_type, expected_length, expected_nullable, expected_identity)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns c
            WHERE c.table_schema='administracion' AND c.table_name=r.table_name AND c.column_name=r.column_name
              AND c.data_type=r.expected_type
              AND (r.expected_length IS NULL OR c.character_maximum_length=r.expected_length)
              AND c.is_nullable=r.expected_nullable AND c.is_identity=r.expected_identity
        ) THEN
            RAISE EXCEPTION 'VALIDACIÓN FALLIDA: tipo/longitud/nulabilidad/identidad incompatible en administracion.%.%', r.table_name, r.column_name;
        END IF;
    END LOOP;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns c
        WHERE c.table_schema='administracion'
          AND c.column_default IS NOT NULL
          AND NOT (
              (c.table_name='tbl_actualizacion_archivo' AND c.column_name='seccion' AND c.column_default ILIKE '%General%')
           OR (c.table_name='tbl_colaborador_familiar' AND c.column_name='activo' AND c.column_default ILIKE '%true%')
           OR (c.table_name='tbl_colaborador_datos_control' AND c.column_name='version' AND c.column_default ILIKE '%1%')
           OR (c.table_name='tbl_colaborador_datos_control' AND c.column_name='fechamodificacion' AND c.column_default ILIKE '%now()%')
           OR (c.table_name='tbl_colaborador_documento' AND c.column_name='activo' AND c.column_default ILIKE '%true%')
           OR (c.table_name='tbl_colaborador_solicitud' AND c.column_name='enviada' AND c.column_default ILIKE '%false%')
          )
          AND c.table_name IN ('tbl_actualizacion_solicitud','tbl_actualizacion_detalle','tbl_actualizacion_formacion','tbl_actualizacion_certificacion','tbl_colaborador_formacion','tbl_colaborador_certificacion','tbl_actualizacion_archivo','tbl_colaborador_informacion_familiar','tbl_colaborador_familiar','tbl_actualizacion_familiar','tbl_colaborador_datos_control','tbl_colaborador_documento','tbl_colaborador_solicitud','tbl_colaborador_solicitud_archivo')
    ) THEN
        RAISE EXCEPTION 'VALIDACIÓN FALLIDA: existe un default no definido por las migraciones en Mis datos';
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns c
        WHERE c.table_schema='administracion' AND c.column_name='porcentajediscapacidad'
          AND c.table_name IN ('tbl_colaborador_informacion_familiar','tbl_colaborador_familiar','tbl_actualizacion_familiar')
          AND (c.numeric_precision <> 5 OR c.numeric_scale <> 2)
    ) THEN
        RAISE EXCEPTION 'VALIDACIÓN FALLIDA: porcentajediscapacidad debe ser numeric(5,2)';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='administracion.tbl_actualizacion_solicitud'::regclass AND c.contype='p' AND pg_get_constraintdef(c.oid) ILIKE 'PRIMARY KEY (id)')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='administracion.tbl_colaborador_informacion_familiar'::regclass AND c.contype='p' AND pg_get_constraintdef(c.oid) ILIKE 'PRIMARY KEY (idempleado)')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='administracion.tbl_colaborador_datos_control'::regclass AND c.contype='p' AND pg_get_constraintdef(c.oid) ILIKE 'PRIMARY KEY (idempleado)') THEN
        RAISE EXCEPTION 'VALIDACIÓN FALLIDA: falta una clave primaria requerida por el modelo EF';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='administracion.tbl_actualizacion_solicitud'::regclass AND c.contype='f' AND c.confrelid='administracion.tbl_administracion_empleado'::regclass AND c.confdeltype='a' AND pg_get_constraintdef(c.oid) ILIKE '%FOREIGN KEY (idempleado)%')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='administracion.tbl_actualizacion_detalle'::regclass AND c.contype='f' AND c.confrelid='administracion.tbl_actualizacion_solicitud'::regclass AND c.confdeltype='c' AND pg_get_constraintdef(c.oid) ILIKE '%FOREIGN KEY (idsolicitud)%')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='administracion.tbl_colaborador_familiar'::regclass AND c.contype='f' AND c.confrelid='administracion.tbl_colaborador_informacion_familiar'::regclass AND c.confdeltype='a' AND pg_get_constraintdef(c.oid) ILIKE '%FOREIGN KEY (idempleado)%')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='administracion.tbl_colaborador_solicitud_archivo'::regclass AND c.contype='f' AND c.confrelid='administracion.tbl_colaborador_solicitud'::regclass AND c.confdeltype='c' AND pg_get_constraintdef(c.oid) ILIKE '%FOREIGN KEY (idsolicitud)%') THEN
        RAISE EXCEPTION 'VALIDACIÓN FALLIDA: FK o acción ON DELETE incompatible';
    END IF;

    FOR r IN
        SELECT * FROM (VALUES
            ('tbl_actualizacion_formacion','tbl_actualizacion_solicitud','idsolicitud','c'),('tbl_actualizacion_certificacion','tbl_actualizacion_solicitud','idsolicitud','c'),('tbl_colaborador_formacion','tbl_administracion_empleado','idempleado','a'),('tbl_colaborador_certificacion','tbl_administracion_empleado','idempleado','a'),('tbl_actualizacion_archivo','tbl_actualizacion_solicitud','idsolicitud','c'),('tbl_colaborador_informacion_familiar','tbl_administracion_empleado','idempleado','a'),('tbl_actualizacion_familiar','tbl_actualizacion_solicitud','idsolicitud','c'),('tbl_colaborador_datos_control','tbl_administracion_empleado','idempleado','a'),('tbl_colaborador_documento','tbl_administracion_empleado','idempleado','a'),('tbl_colaborador_solicitud','tbl_administracion_empleado','idempleado','a')
        ) AS x(child_table, parent_table, child_column, delete_type)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint c
            WHERE c.conrelid=format('administracion.%I',r.child_table)::regclass
              AND c.confrelid=format('administracion.%I',r.parent_table)::regclass
              AND c.contype='f' AND c.confdeltype = r.delete_type::"char"
              AND pg_get_constraintdef(c.oid) ILIKE '%FOREIGN KEY ('||r.child_column||')%'
        ) THEN
            RAISE EXCEPTION 'VALIDACIÓN FALLIDA: FK incompatible o ausente en administracion.%', r.child_table;
        END IF;
    END LOOP;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='administracion' AND indexname='ix_actualizacion_solicitud_empleado_estado' AND indexdef ILIKE '%(idempleado, estado)%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='administracion' AND indexname='ix_actualizacion_solicitud_empleado_version' AND indexdef ILIKE '%(idempleado, version)%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='administracion' AND indexname='ix_actualizacion_archivo_solicitud_key' AND indexdef ILIKE 'CREATE UNIQUE%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='administracion' AND indexname='ux_colab_formacion_empleado_clave' AND indexdef ILIKE 'CREATE UNIQUE%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='administracion' AND indexname='ux_colab_cert_empleado_clave' AND indexdef ILIKE 'CREATE UNIQUE%')
       OR NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='administracion' AND indexname='ux_colab_familiar_empleado_clave' AND indexdef ILIKE 'CREATE UNIQUE%') THEN
        RAISE EXCEPTION 'VALIDACIÓN FALLIDA: falta un índice requerido por las migraciones';
    END IF;
END $$;

COMMIT;

/*
  Sección independiente de permisos. Ejecutarla después del COMMIT anterior.
  Conserva asignaciones existentes y no depende de que exista un nombre de rol:
  si no hay roles reconocidos, no inserta asignaciones y la instalación de tablas
  ya quedó completada.
*/
BEGIN;

DO $$
DECLARE
    r record;
    v_modulo_id integer;
    v_permiso_id integer;
    v_activo boolean;
BEGIN
    FOR r IN
        SELECT * FROM (VALUES
            ('tbl_autenticacion_modulo','id','integer','NO','YES'),('tbl_autenticacion_modulo','nombremodulo','character varying','NO','NO'),('tbl_autenticacion_modulo','rutamodulo','character varying','YES','NO'),('tbl_autenticacion_modulo','ordenvisualizacion','integer','YES','NO'),('tbl_autenticacion_modulo','activo','boolean','NO','NO'),('tbl_autenticacion_modulo','usuariocreacion','character varying','NO','NO'),('tbl_autenticacion_modulo','fechacreacion','timestamp with time zone','NO','NO'),('tbl_autenticacion_modulo','ipcreacion','character varying','NO','NO'),
            ('tbl_autenticacion_permiso','id','integer','NO','YES'),('tbl_autenticacion_permiso','idmodulo','integer','NO','NO'),('tbl_autenticacion_permiso','codigo','character varying','NO','NO'),('tbl_autenticacion_permiso','accion','character varying','NO','NO'),('tbl_autenticacion_permiso','descripcion','text','NO','NO'),('tbl_autenticacion_permiso','activo','boolean','NO','NO'),('tbl_autenticacion_permiso','usuariocreacion','character varying','NO','NO'),('tbl_autenticacion_permiso','fechacreacion','timestamp with time zone','NO','NO'),('tbl_autenticacion_permiso','ipcreacion','character varying','NO','NO'),
            ('tbl_autenticacion_rol','id','integer','NO','YES'),('tbl_autenticacion_rol','nombre','character varying','NO','NO'),('tbl_autenticacion_rol','activo','boolean','NO','NO'),
            ('tbl_autenticacion_rol_permiso','idrol','integer','NO','NO'),('tbl_autenticacion_rol_permiso','idpermiso','integer','NO','NO'),('tbl_autenticacion_rol_permiso','otorgado','timestamp with time zone','NO','NO'),('tbl_autenticacion_rol_permiso','activo','boolean','NO','NO'),('tbl_autenticacion_rol_permiso','usuariocreacion','character varying','NO','NO'),('tbl_autenticacion_rol_permiso','fechacreacion','timestamp with time zone','NO','NO'),('tbl_autenticacion_rol_permiso','ipcreacion','character varying','NO','NO')
        ) AS x(table_name, column_name, expected_type, expected_nullable, expected_identity)
    LOOP
        IF NOT EXISTS (
            SELECT 1 FROM information_schema.columns c
            WHERE c.table_schema = 'autenticacion' AND c.table_name = r.table_name AND c.column_name = r.column_name
              AND c.data_type = r.expected_type AND c.is_nullable = r.expected_nullable AND c.is_identity = r.expected_identity
        ) THEN
            RAISE EXCEPTION 'AUTENTICACIÓN INCOMPATIBLE: autenticacion.%.%', r.table_name, r.column_name;
        END IF;
    END LOOP;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns c
        WHERE c.table_schema='autenticacion'
          AND ((c.table_name='tbl_autenticacion_modulo' AND c.column_name='nombremodulo' AND c.character_maximum_length<>100)
            OR (c.table_name='tbl_autenticacion_modulo' AND c.column_name='rutamodulo' AND c.character_maximum_length<>255)
            OR (c.table_name='tbl_autenticacion_permiso' AND c.column_name='codigo' AND c.character_maximum_length<>100)
            OR (c.table_name='tbl_autenticacion_permiso' AND c.column_name='accion' AND c.character_maximum_length<>40)
            OR (c.table_name='tbl_autenticacion_rol' AND c.column_name='nombre' AND c.character_maximum_length<>50)
            OR (c.table_name IN ('tbl_autenticacion_modulo','tbl_autenticacion_permiso','tbl_autenticacion_rol_permiso') AND c.column_name='usuariocreacion' AND c.character_maximum_length<>50)
            OR (c.table_name IN ('tbl_autenticacion_modulo','tbl_autenticacion_permiso','tbl_autenticacion_rol_permiso') AND c.column_name='ipcreacion' AND c.character_maximum_length<>45))
    ) THEN
        RAISE EXCEPTION 'AUTENTICACIÓN INCOMPATIBLE: longitud de columna distinta al DDL real';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='autenticacion' AND table_name='tbl_autenticacion_modulo' AND column_name='activo' AND column_default ILIKE '%true%')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='autenticacion' AND table_name='tbl_autenticacion_permiso' AND column_name='activo' AND column_default ILIKE '%true%')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='autenticacion' AND table_name='tbl_autenticacion_rol_permiso' AND column_name='otorgado' AND column_default ILIKE '%now()%')
       OR NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='autenticacion' AND table_name='tbl_autenticacion_rol_permiso' AND column_name='activo' AND column_default ILIKE '%true%') THEN
        RAISE EXCEPTION 'AUTENTICACIÓN INCOMPATIBLE: falta un valor por defecto requerido por el esquema';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_indexes i
        JOIN pg_class ic ON ic.relname = i.indexname
        JOIN pg_index pi ON pi.indexrelid = ic.oid
        WHERE i.schemaname = 'autenticacion'
          AND i.tablename = 'tbl_autenticacion_rol_permiso'
          AND pi.indisunique
          AND pi.indpred IS NULL
          AND i.indexdef ILIKE '%(idrol, idpermiso)%'
    ) THEN
        RAISE EXCEPTION 'AUTENTICACIÓN INCOMPATIBLE: falta índice UNIQUE no parcial para ON CONFLICT (idrol,idpermiso)';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='autenticacion.tbl_autenticacion_modulo'::regclass AND c.contype='p' AND pg_get_constraintdef(c.oid) ILIKE 'PRIMARY KEY (id)')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='autenticacion.tbl_autenticacion_permiso'::regclass AND c.contype='p' AND pg_get_constraintdef(c.oid) ILIKE 'PRIMARY KEY (id)')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='autenticacion.tbl_autenticacion_rol'::regclass AND c.contype='p' AND pg_get_constraintdef(c.oid) ILIKE 'PRIMARY KEY (id)')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='autenticacion.tbl_autenticacion_rol_permiso'::regclass AND c.contype='p' AND pg_get_constraintdef(c.oid) ILIKE 'PRIMARY KEY (idrol, idpermiso)') THEN
        RAISE EXCEPTION 'AUTENTICACIÓN INCOMPATIBLE: falta una clave primaria requerida';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='autenticacion' AND tablename='tbl_autenticacion_permiso' AND indexdef ILIKE 'CREATE UNIQUE%' AND indexdef ILIKE '%(codigo)%')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='autenticacion.tbl_autenticacion_permiso'::regclass AND c.contype='f' AND c.confrelid='autenticacion.tbl_autenticacion_modulo'::regclass AND c.confdeltype='r' AND pg_get_constraintdef(c.oid) ILIKE '%FOREIGN KEY (idmodulo)%')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='autenticacion.tbl_autenticacion_rol_permiso'::regclass AND c.contype='f' AND c.confrelid='autenticacion.tbl_autenticacion_rol'::regclass AND pg_get_constraintdef(c.oid) ILIKE '%FOREIGN KEY (idrol)%')
       OR NOT EXISTS (SELECT 1 FROM pg_constraint c WHERE c.conrelid='autenticacion.tbl_autenticacion_rol_permiso'::regclass AND c.contype='f' AND c.confrelid='autenticacion.tbl_autenticacion_permiso'::regclass AND pg_get_constraintdef(c.oid) ILIKE '%FOREIGN KEY (idpermiso)%') THEN
        RAISE EXCEPTION 'AUTENTICACIÓN INCOMPATIBLE: falta una relación o unicidad requerida';
    END IF;

    SELECT id, activo INTO v_modulo_id, v_activo
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE nombremodulo = 'Actualización de datos' LIMIT 1;
    IF v_modulo_id IS NULL THEN
        INSERT INTO autenticacion.tbl_autenticacion_modulo
            (nombremodulo, rutamodulo, ordenvisualizacion, activo, usuariocreacion, fechacreacion, ipcreacion)
        VALUES ('Actualización de datos','/actualizaciones-datos',20,true,'SYSTEM',now(),'127.0.0.1')
        RETURNING id INTO v_modulo_id;
    ELSIF NOT v_activo THEN
        RAISE EXCEPTION 'AUTENTICACIÓN INCONSISTENTE: el módulo Actualización de datos está inactivo';
    END IF;

    SELECT id, activo INTO v_permiso_id, v_activo
    FROM autenticacion.tbl_autenticacion_permiso
    WHERE codigo = 'COLABORADORES_DATOS_REVISAR' LIMIT 1;
    IF v_permiso_id IS NULL THEN
        INSERT INTO autenticacion.tbl_autenticacion_permiso
            (idmodulo, codigo, accion, descripcion, activo, usuariocreacion, fechacreacion, ipcreacion)
        VALUES (v_modulo_id,'COLABORADORES_DATOS_REVISAR','REVIEW','Revisar y aprobar actualizaciones de datos de colaboradores',true,'SYSTEM',now(),'127.0.0.1')
        RETURNING id INTO v_permiso_id;
    ELSIF NOT v_activo THEN
        RAISE EXCEPTION 'AUTENTICACIÓN INCONSISTENTE: COLABORADORES_DATOS_REVISAR existe pero está inactivo';
    ELSIF NOT EXISTS (SELECT 1 FROM autenticacion.tbl_autenticacion_permiso WHERE id = v_permiso_id AND idmodulo = v_modulo_id) THEN
        RAISE EXCEPTION 'AUTENTICACIÓN INCONSISTENTE: COLABORADORES_DATOS_REVISAR pertenece a otro módulo';
    END IF;

    INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
        (idrol, idpermiso, otorgado, activo, usuariocreacion, fechacreacion, ipcreacion)
    SELECT role_row.id, v_permiso_id, now(), true, 'SYSTEM', now(), '127.0.0.1'
    FROM autenticacion.tbl_autenticacion_rol role_row
    WHERE role_row.activo AND upper(role_row.nombre) IN ('ADMINISTRADOR','RECURSOS HUMANOS','RECURSOS_HUMANOS')
    ON CONFLICT (idrol, idpermiso) DO NOTHING;
END $$;

COMMIT;

/* Consultas finales de verificación (ejecutar después del COMMIT). */
SELECT current_database() AS base_actual;

SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema = 'administracion'
  AND table_name IN (
    'tbl_actualizacion_solicitud','tbl_actualizacion_detalle','tbl_actualizacion_formacion',
    'tbl_actualizacion_certificacion','tbl_colaborador_formacion','tbl_colaborador_certificacion',
    'tbl_actualizacion_archivo','tbl_colaborador_informacion_familiar','tbl_colaborador_familiar',
    'tbl_actualizacion_familiar','tbl_colaborador_datos_control','tbl_colaborador_documento',
    'tbl_colaborador_solicitud','tbl_colaborador_solicitud_archivo'
  )
ORDER BY table_name;

SELECT table_name, column_name, data_type, is_nullable, column_default
FROM information_schema.columns
WHERE table_schema = 'administracion'
  AND table_name IN (
    'tbl_actualizacion_solicitud','tbl_actualizacion_detalle','tbl_actualizacion_formacion',
    'tbl_actualizacion_certificacion','tbl_colaborador_formacion','tbl_colaborador_certificacion',
    'tbl_actualizacion_archivo','tbl_colaborador_informacion_familiar','tbl_colaborador_familiar',
    'tbl_actualizacion_familiar','tbl_colaborador_datos_control','tbl_colaborador_documento',
    'tbl_colaborador_solicitud','tbl_colaborador_solicitud_archivo'
  )
ORDER BY table_name, ordinal_position;

SELECT
    tc.table_name,
    tc.constraint_name,
    tc.constraint_type,
    rc.table_schema AS referenced_schema,
    rc.table_name AS referenced_table
FROM information_schema.table_constraints tc
LEFT JOIN information_schema.referential_constraints rcon
  ON rcon.constraint_schema = tc.constraint_schema
 AND rcon.constraint_name = tc.constraint_name
LEFT JOIN information_schema.table_constraints rc
  ON rc.constraint_schema = rcon.unique_constraint_schema
 AND rc.constraint_name = rcon.unique_constraint_name
WHERE tc.constraint_schema = 'administracion'
  AND tc.table_name IN (
    'tbl_actualizacion_solicitud','tbl_actualizacion_detalle','tbl_actualizacion_formacion',
    'tbl_actualizacion_certificacion','tbl_colaborador_formacion','tbl_colaborador_certificacion',
    'tbl_actualizacion_archivo','tbl_colaborador_informacion_familiar','tbl_colaborador_familiar',
    'tbl_actualizacion_familiar','tbl_colaborador_datos_control','tbl_colaborador_documento',
    'tbl_colaborador_solicitud','tbl_colaborador_solicitud_archivo'
  )
ORDER BY tc.table_name, tc.constraint_type, tc.constraint_name;

SELECT indexname, tablename
FROM pg_indexes
WHERE schemaname = 'administracion'
  AND tablename IN (
    'tbl_actualizacion_solicitud','tbl_actualizacion_archivo','tbl_colaborador_formacion',
    'tbl_colaborador_certificacion','tbl_colaborador_familiar','tbl_actualizacion_familiar',
    'tbl_colaborador_documento','tbl_colaborador_solicitud'
  )
ORDER BY tablename, indexname;

SELECT p.codigo, p.accion, p.activo AS permiso_activo,
       r.nombre AS rol, r.activo AS rol_activo, rp.activo AS asignacion_activa
FROM autenticacion.tbl_autenticacion_permiso p
LEFT JOIN autenticacion.tbl_autenticacion_rol_permiso rp ON rp.idpermiso = p.id
LEFT JOIN autenticacion.tbl_autenticacion_rol r ON r.id = rp.idrol
WHERE p.codigo = 'COLABORADORES_DATOS_REVISAR'
ORDER BY r.nombre;

SELECT to_regclass('public."__EFMigrationsHistory"') AS public_ef_history,
       to_regclass('administracion."__EFMigrationsHistory"') AS admin_ef_history,
       to_regclass('public.tmr_installation_history') AS installation_history;
