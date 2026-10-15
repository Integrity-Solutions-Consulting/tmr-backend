-- =============================================================================
-- 13 - MIGRACION OFICIAL: tipos de proveedor
-- Motor: PostgreSQL
-- Requiere: 01-Inv_tmr_db_deploy_29052026.sql y los scripts previos.
--
-- Es segura para instalaciones existentes: no borra proveedores ni modifica sus
-- datos. Conserva los IdTipoProveedor actuales copiando los tipos referidos desde
-- el catalogo administrativo antes de mover la llave foranea.
-- =============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS inventario.tbl_inventario_tipo_proveedor (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    Nombre              VARCHAR(150) NOT NULL,
    Activo              BOOLEAN NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50) NOT NULL DEFAULT 'SYSTEM',
    FechaCreacion       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    CONSTRAINT pk_inventario_tipo_proveedor PRIMARY KEY (Id),
    CONSTRAINT uq_inventario_tipo_proveedor_nombre UNIQUE (Nombre)
);

-- Conserva los identificadores usados por proveedores existentes que todavia
-- apuntan al catalogo administrativo del modelo anterior.
INSERT INTO inventario.tbl_inventario_tipo_proveedor
    (Id, Nombre, Activo, UsuarioCreacion, FechaCreacion)
OVERRIDING SYSTEM VALUE
SELECT DISTINCT d.Id, d.Valor, d.Activo, 'MIGRACION', NOW()
FROM inventario.tbl_inventario_proveedor p
JOIN administracion.tbl_administracion_catalogo_detalle d ON d.Id = p.IdTipoProveedor
WHERE p.IdTipoProveedor IS NOT NULL
ON CONFLICT (Id) DO NOTHING;

-- Tipos iniciales para una instalacion nueva, sin sobrescribir catalogos ya
-- existentes ni los tipos migrados de una instalacion anterior.
INSERT INTO inventario.tbl_inventario_tipo_proveedor
    (Nombre, Activo, UsuarioCreacion, FechaCreacion)
VALUES
    ('Proveedor de equipo', TRUE, 'SYSTEM', NOW()),
    ('Proveedor de servicios profesionales', TRUE, 'SYSTEM', NOW())
ON CONFLICT (Nombre) DO NOTHING;

-- Nunca se cambia la FK si algun proveedor quedaria sin tipo valido. La
-- excepcion revierte toda la transaccion y obliga a corregir el dato origen.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM inventario.tbl_inventario_proveedor p
        LEFT JOIN inventario.tbl_inventario_tipo_proveedor t ON t.Id = p.IdTipoProveedor
        WHERE p.IdTipoProveedor IS NOT NULL AND t.Id IS NULL
    ) THEN
        RAISE EXCEPTION 'No se puede migrar Proveedores: existen IdTipoProveedor sin tipo equivalente.';
    END IF;
END $$;

-- Elimina unicamente la FK que usa IdTipoProveedor, sin depender del nombre que
-- haya recibido en instalaciones antiguas.
DO $$
DECLARE
    constraint_name TEXT;
BEGIN
    FOR constraint_name IN
        SELECT c.conname
        FROM pg_constraint c
        JOIN pg_class r ON r.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = r.relnamespace
        WHERE c.contype = 'f'
          AND n.nspname = 'inventario'
          AND r.relname = 'tbl_inventario_proveedor'
          AND EXISTS (
              SELECT 1
              FROM unnest(c.conkey) AS key_column(attnum)
              JOIN pg_attribute a ON a.attrelid = r.oid AND a.attnum = key_column.attnum
              WHERE a.attname = 'idtipoproveedor'
          )
    LOOP
        EXECUTE format('ALTER TABLE inventario.tbl_inventario_proveedor DROP CONSTRAINT %I', constraint_name);
    END LOOP;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint c
        JOIN pg_class r ON r.oid = c.conrelid
        JOIN pg_namespace n ON n.oid = r.relnamespace
        WHERE c.conname = 'fk_inventario_proveedor_tipo_proveedor'
          AND n.nspname = 'inventario'
          AND r.relname = 'tbl_inventario_proveedor'
    ) THEN
        ALTER TABLE inventario.tbl_inventario_proveedor
            ADD CONSTRAINT fk_inventario_proveedor_tipo_proveedor
            FOREIGN KEY (IdTipoProveedor)
            REFERENCES inventario.tbl_inventario_tipo_proveedor(Id)
            ON DELETE RESTRICT;
    END IF;
END $$;

SELECT setval(
    pg_get_serial_sequence('inventario.tbl_inventario_tipo_proveedor', 'id'),
    COALESCE((SELECT MAX(Id) FROM inventario.tbl_inventario_tipo_proveedor), 1),
    TRUE
);

COMMIT;
