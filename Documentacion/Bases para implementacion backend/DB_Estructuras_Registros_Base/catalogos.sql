SELECT * FROM administracion.tbl_administracion_catalogo;
SELECT *FROM administracion.tbl_administracion_catalogo_detalle;
ROLLBACK;
-- =============================================================================
--  DATOS MAESTROS — administracion.tbl_administracion_catalogo
--  + administracion.tbl_administracion_catalogo_detalle
--  Fábrica de software — Ecuador + Latinoamérica
--  Fecha: 2026-05-22
-- =============================================================================
BEGIN;

-- =============================================================================
-- 1. CATÁLOGOS CABECERA
-- =============================================================================

INSERT INTO administracion.tbl_administracion_catalogo
    (TipoCatalogo, Codigo, Descripcion, Activo, UsuarioCreacion, IpCreacion)
VALUES
    ('ADM', 'GEN',  'Género de la persona',                    TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'NAC',  'Nacionalidad',                            TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'TID',  'Tipo de identificación',                  TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'DEP',  'Departamento de la empresa',              TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'MDT',  'Modo de trabajo',                         TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'CAT',  'Categoría del empleado',                  TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'EMP',  'Empresa del grupo',                       TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'TCT',  'Tipo de contrato',                        TRUE, 'SYSTEM', '127.0.0.1');

-- =============================================================================
-- 2. DETALLES POR CATÁLOGO
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 2.1 GÉNERO  (Codigo = 'GEN')
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('M', 'Masculino',          'Género masculino',          1),
    ('F', 'Femenino',           'Género femenino',           2),
    ('O', 'Otro',               'Otro género',               3),
    ('ND', 'Prefiero no decir',  'No especificado',           4)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'GEN' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.2 NACIONALIDAD  (Codigo = 'NAC')
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('ECU',  'Ecuatoriana',    'Ecuador',            1),
    ('COL',  'Colombiana',     'Colombia',           2),
    ('PER',  'Peruana',        'Perú',               3),
    ('VEN',  'Venezolana',     'Venezuela',          4),
    ('ARG',  'Argentina',      'Argentina',          5),
    ('CHI',  'Chilena',        'Chile',              6),
    ('BOL',  'Boliviana',      'Bolivia',            7),
    ('PAR',  'Paraguaya',      'Paraguay',           8),
    ('URU',  'Uruguaya',       'Uruguay',            9),
    ('BRA',  'Brasileña',      'Brasil',            10),
    ('MEX',  'Mexicana',       'México',            11),
    ('GUA',  'Guatemalteca',   'Guatemala',         12),
    ('HON',  'Hondureña',      'Honduras',          13),
    ('SAL',  'Salvadoreña',    'El Salvador',       14),
    ('NIC',  'Nicaragüense',   'Nicaragua',         15),
    ('COS',  'Costarricense',  'Costa Rica',        16),
    ('PAN',  'Panameña',       'Panamá',            17),
    ('CUB',  'Cubana',         'Cuba',              18),
    ('DOM',  'Dominicana',     'República Dom.',    19),
    ('OTRA', 'Otra',           'Otra nacionalidad', 20)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'NAC' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.3 TIPO DE IDENTIFICACIÓN  (Codigo = 'TID')
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('C',  'Cédula',           'Cédula de identidad Ecuador (10 dígitos)',  1),
    ('R',  'RUC',              'Registro Único de Contribuyentes (13 díg)', 2),
    ('P',  'Pasaporte',        'Pasaporte internacional (hasta 20 chars)',   3),
    ('O', 'Otro documento',   'Otro tipo de documento de identidad',        4)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'TID' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.4 DEPARTAMENTO  (Codigo = 'DEP')
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('DEV',  'Desarrollo',      'Ingeniería y desarrollo de software',        1),
    ('QA',   'QA',              'Control y aseguramiento de calidad',          2),
    ('DVO',  'DevOps',          'Infraestructura, CI/CD y operaciones',        3),
    ('DIS',  'Diseño',          'Diseño UX/UI y experiencia de usuario',       4),
    ('GES',  'Gestión',         'Gestión de proyectos y scrum masters',        5),
    ('VEN',  'Ventas',          'Comercial y desarrollo de negocio',           6),
    ('ADM',  'Administración',  'Recursos humanos, finanzas y legal',          7)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'DEP' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.5 MODO DE TRABAJO  (Codigo = 'MDT')
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('P', 'Presencial',  'Trabajo 100% en oficina',              1),
    ('R',  'Remoto',      'Trabajo 100% desde casa',              2),
    ('H',  'Híbrido',     'Combinación de presencial y remoto',   3)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'MDT' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.6 CATEGORÍA DEL EMPLEADO  (Codigo = 'CAT')
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('COL',   'Colaboradores',    'Menos de 2 años de experiencia',          1),
    ('LID',  'Lideres',       'Entre 2 y 4 años de experiencia',         2),
    ('ANA',   'Analistas',    'Más de 4 años de experiencia',            3),
    ('GER', 'Gerencia',      'Líder técnico de equipo',                  4),
    ('MGR',  'Manager',   'Gerente o jefe de área',                   5)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'CAT' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.7 EMPRESA DEL GRUPO  (Codigo = 'EMP')
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, ValorExtra, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, d.ValorExtra, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('ISC', 'Integrity Solutions',   'Empresa matriz del grupo',         1, 'MATRIZ'),
    ('RPS', 'Risk Process Solutions',      'Operaciones en Ecuador',           2, 'FILIAL')
) AS d(CodigoValor, Valor, Descripcion, Orden, ValorExtra)
WHERE c.Codigo = 'EMP' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.8 TIPO DE CONTRATO  (Codigo = 'TCT')
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('F', 'Fijo',              'Contrato indefinido a tiempo completo',     1),
    ('CON', 'Por contrato',      'Contrato a plazo fijo o por proyecto',      2),
    ('MT', 'Medio tiempo',      'Contrato a tiempo parcial',                 3),
    ('PAS', 'Pasantía',          'Pasante o practicante universitario',       4),
    ('COS', 'Consultor',         'Prestación de servicios / honorarios',      5)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'TCT' AND c.TipoCatalogo = 'ADM';

-- =============================================================================
-- VERIFICACIÓN
-- =============================================================================
DO $$
DECLARE
    v_catalogos INTEGER;
    v_detalles  INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_catalogos
    FROM administracion.tbl_administracion_catalogo
    WHERE UsuarioCreacion = 'SYSTEM';

    SELECT COUNT(*) INTO v_detalles
    FROM administracion.tbl_administracion_catalogo_detalle
    WHERE UsuarioCreacion = 'SYSTEM';

    RAISE NOTICE '================================';
    RAISE NOTICE '  Catálogos insertados : %', v_catalogos;
    RAISE NOTICE '  Detalles insertados  : %', v_detalles;
    RAISE NOTICE '================================';
END $$;

COMMIT;