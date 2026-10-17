-- =============================================================================
-- NUEVAS COLUMNAS PARA GESTIÓN DE SALIDA Y REEMPLAZO
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Agregar columnas para salida del empleado
-- ---------------------------------------------------------------------------
ALTER TABLE administracion.tbl_administracion_empleado ADD COLUMN IdTipoSalida INTEGER NULL;
ALTER TABLE administracion.tbl_administracion_empleado ADD COLUMN IdCausaSalida INTEGER NULL;
ALTER TABLE administracion.tbl_administracion_empleado ADD COLUMN ComentarioSalida TEXT NULL;
ALTER TABLE administracion.tbl_administracion_empleado ADD COLUMN IdEmpleadoReemplazo INTEGER NULL;

-- ---------------------------------------------------------------------------
-- Agregar restricciones FOREIGN KEY
-- ---------------------------------------------------------------------------
ALTER TABLE administracion.tbl_administracion_empleado
ADD CONSTRAINT fk_empleado_tipo_salida
FOREIGN KEY (IdTipoSalida)
REFERENCES administracion.tbl_administracion_catalogo_detalle(Id);

ALTER TABLE administracion.tbl_administracion_empleado
ADD CONSTRAINT fk_empleado_causa_salida
FOREIGN KEY (IdCausaSalida)
REFERENCES administracion.tbl_administracion_catalogo_detalle(Id);

ALTER TABLE administracion.tbl_administracion_empleado
ADD CONSTRAINT fk_empleado_reemplazo
FOREIGN KEY (IdEmpleadoReemplazo)
REFERENCES administracion.tbl_administracion_empleado(Id);

-- ---------------------------------------------------------------------------
-- Agregar índices para consultas rápidas
-- ---------------------------------------------------------------------------
CREATE INDEX idx_adm_empleado_tipo_salida ON administracion.tbl_administracion_empleado(IdTipoSalida);
CREATE INDEX idx_adm_empleado_causa_salida ON administracion.tbl_administracion_empleado(IdCausaSalida);
CREATE INDEX idx_adm_empleado_reemplazo ON administracion.tbl_administracion_empleado(IdEmpleadoReemplazo);
CREATE INDEX idx_adm_empleado_terminacion ON administracion.tbl_administracion_empleado(FechaTerminacion);








-- =============================================================================
-- CATÁLOGOS PARA GESTIÓN DE SALIDA DE COLABORADORES
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 1.1 Cabecera: Tipo de Salida (TOS)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo
    (TipoCatalogo, Codigo, Descripcion, Activo, UsuarioCreacion, IpCreacion)
SELECT 'ADM', 'TOS', 'Tipo de salida del empleado', TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo
    WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM'
);

-- ---------------------------------------------------------------------------
-- 1.2 Detalles: Tipo de Salida (TOS)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM'),
    'REN', 'Renuncia', 'Renuncia voluntaria del colaborador', 1, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'REN'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM'),
    'DES', 'Despido', 'Despido por decision de la empresa', 2, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'DES'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM'),
    'JUB', 'Jubilacion', 'Jubilacion por edad o anos de servicio', 3, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'JUB'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM'),
    'FIN', 'Fin de contrato', 'Finalizacion de contrato a plazo fijo', 4, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'FIN'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM'),
    'OTR', 'Otro', 'Otro motivo no clasificado', 5, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'TOS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'OTR'
);

-- ---------------------------------------------------------------------------
-- 2.1 Cabecera: Causa de Salida (CAS)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo
    (TipoCatalogo, Codigo, Descripcion, Activo, UsuarioCreacion, IpCreacion)
SELECT 'ADM', 'CAS', 'Causa de salida del empleado', TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo
    WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM'
);

-- ---------------------------------------------------------------------------
-- 2.2 Detalles: Causa de Salida (CAS)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM'),
    'CLI', 'Contratacion directa por cliente', 'El colaborador fue contratado directamente por un cliente', 1, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'CLI'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM'),
    'MEJ', 'Mejor oferta laboral', 'El colaborador encontro una mejor oferta en otra empresa', 2, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'MEJ'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM'),
    'RET', 'Retiro voluntario', 'Decision personal de retiro del mercado laboral', 3, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'RET'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM'),
    'BAJ', 'Bajo rendimiento', 'Desvinculacion por bajo desempeno', 4, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'BAJ'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM'),
    'RES', 'Reestructuracion', 'Cese por reestructuracion de la empresa', 5, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'RES'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM'),
    'CON', 'Fin de proyecto', 'Finalizacion del proyecto para el que fue contratado', 6, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'CON'
);

INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT
    (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM'),
    'OTR', 'Otra causa', 'Causa no especificada', 7, TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo = 'CAS' AND TipoCatalogo = 'ADM')
    AND CodigoValor = 'OTR'
);