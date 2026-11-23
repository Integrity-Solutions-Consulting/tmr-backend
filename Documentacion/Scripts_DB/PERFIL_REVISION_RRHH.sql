ALTER TABLE administracion.tbl_colaborador_solicitud
    ADD COLUMN IF NOT EXISTS "Seccion" text,
    ADD COLUMN IF NOT EXISTS "Propuesta" text,
    ADD COLUMN IF NOT EXISTS "EstadoRevision" text,
    ADD COLUMN IF NOT EXISTS "ObservacionRevision" text,
    ADD COLUMN IF NOT EXISTS "Revisor" text,
    ADD COLUMN IF NOT EXISTS "FechaRevision" timestamptz;
CREATE UNIQUE INDEX IF NOT EXISTS ux_colaborador_revision_pendiente
    ON administracion.tbl_colaborador_solicitud (idempleado, "Seccion")
    WHERE "EstadoRevision" IN ('EnRevision', 'RequiereCorreccion');
