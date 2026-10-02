-- =============================================================================
-- HISTORIAL DE TIPO DE CONTRATO DEL EMPLEADO
-- sm - La jornada esperada depende del tipo de contrato (8 h; 6 h Pasantía, código PAS). Antes solo se guardaba el
-- tipo actual en tbl_administracion_empleado, así que si alguien pasaba de pasante a fijo, el histórico del
-- dashboard le exigía 8 h también en los meses en que era pasante. Esta tabla guarda cada tipo con su vigencia.
-- La aplicación la llena sola al crear un colaborador o al cambiarle el tipo de contrato.
-- =============================================================================

CREATE TABLE IF NOT EXISTS administracion.tbl_administracion_empleado_contrato (
    Id                  INTEGER GENERATED ALWAYS AS IDENTITY,
    IdEmpleado          INTEGER      NOT NULL,
    IdTipoContrato      INTEGER      NOT NULL,
    FechaDesde          DATE         NOT NULL,
    FechaHasta          DATE         NULL,          -- NULL = vigente
    Activo              BOOLEAN      NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)  NOT NULL,
    FechaCreacion       TIMESTAMPTZ  NOT NULL DEFAULT now(),
    IpCreacion          VARCHAR(45)  NOT NULL,
    UsuarioModificacion VARCHAR(50)  NULL,
    FechaModificacion   TIMESTAMPTZ  NULL,
    IpModificacion      VARCHAR(45)  NULL,
    CONSTRAINT pk_administracion_empleado_contrato PRIMARY KEY (Id),
    CONSTRAINT fk_empleado_contrato_empleado FOREIGN KEY (IdEmpleado)
        REFERENCES administracion.tbl_administracion_empleado(Id),
    CONSTRAINT fk_empleado_contrato_tipo FOREIGN KEY (IdTipoContrato)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT ck_empleado_contrato_fechas CHECK (FechaHasta IS NULL OR FechaHasta >= FechaDesde)
);

CREATE INDEX IF NOT EXISTS idx_adm_empleado_contrato_empleado
    ON administracion.tbl_administracion_empleado_contrato(IdEmpleado, FechaDesde);

-- ---------------------------------------------------------------------------
-- Carga inicial: el tipo de contrato actual de cada empleado, vigente desde su ingreso.
-- Los empleados sin tipo de contrato no se cargan: hay que completarlo en su ficha (el dashboard les asume 8 h).
-- Se puede volver a ejecutar sin duplicar.
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_empleado_contrato
    (IdEmpleado, IdTipoContrato, FechaDesde, FechaHasta, Activo, UsuarioCreacion, IpCreacion)
SELECT e.Id,
       e.IdTipoContrato,
       COALESCE(e.FechaIngreso, e.FechaCreacion::date),
       NULL,
       TRUE,
       'SYSTEM',
       '127.0.0.1'
FROM administracion.tbl_administracion_empleado e
WHERE e.IdTipoContrato IS NOT NULL
  AND NOT EXISTS (
      SELECT 1 FROM administracion.tbl_administracion_empleado_contrato c
      WHERE c.IdEmpleado = e.Id AND c.Activo
  );
