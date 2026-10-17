-- =============================================================================
--  SEED COMPLETO — tmr-backend
--  Esquemas: administracion + autenticacion
--  Motor: PostgreSQL 16
--  Estrategia: idempotente, transaccional, sin magic numbers
--  Fecha: 2026-05-29
--  Correcciones aplicadas:
--    - 'Paasante' → 'Pasante'
--    - Colaborador cambia a 'CBD'
--    - Orden CAT: Pasante(1) > Colaborador(2) > Analista(3) > Líder(4) > Manager(5) > Gerencia(6)
--    - Gerente: acceso a Proyectos, Actividades, Seguimiento, Clientes, Lideres
--    - Colaborador: acceso solo a Actividades
--    - 'Líderes' → 'Lideres' (sin tilde, estándar para datos técnicos en DB)
--    - Eliminado carácter '¿' en solicitud de requerimiento
-- =============================================================================

BEGIN;

-- =============================================================================
-- BLOQUE 1 — administracion.tbl_administracion_catalogo  (cabeceras)
-- =============================================================================

INSERT INTO administracion.tbl_administracion_catalogo
    (TipoCatalogo, Codigo, Descripcion, Activo, UsuarioCreacion, IpCreacion)
VALUES
    ('ADM', 'GEN', 'Género de la persona',       TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'NAC', 'Nacionalidad',                TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'TID', 'Tipo de identificación',     TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'DEP', 'Departamento de la empresa',  TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'MDT', 'Modo de trabajo',             TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'CAT', 'Categoría del empleado',     TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'EMP', 'Empresa del grupo',           TRUE, 'SYSTEM', '127.0.0.1'),
    ('ADM', 'TCT', 'Tipo de contrato',            TRUE, 'SYSTEM', '127.0.0.1');

-- =============================================================================
-- BLOQUE 2 — administracion.tbl_administracion_catalogo_detalle
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 2.1  GÉNERO  (GEN)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('M',  'Masculino',         'Género masculino', 1),
    ('F',  'Femenino',          'Género femenino',  2),
    ('O',  'Otro',              'Otro género',      3),
    ('ND', 'Prefiero no decir', 'No especificado',  4)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'GEN' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.2  NACIONALIDAD  (NAC)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('ECU',  'Ecuatoriana',   'Ecuador',            1),
    ('COL',  'Colombiana',    'Colombia',            2),
    ('PER',  'Peruana',       'Perú',                3),
    ('VEN',  'Venezolana',    'Venezuela',           4),
    ('ARG',  'Argentina',     'Argentina',           5),
    ('CHI',  'Chilena',       'Chile',               6),
    ('BOL',  'Boliviana',     'Bolivia',             7),
    ('PAR',  'Paraguaya',     'Paraguay',            8),
    ('URU',  'Uruguaya',      'Uruguay',             9),
    ('BRA',  'Brasileña',     'Brasil',             10),
    ('MEX',  'Mexicana',      'México',             11),
    ('GUA',  'Guatemalteca',  'Guatemala',          12),
    ('HON',  'Hondureña',     'Honduras',           13),
    ('SAL',  'Salvadoreña',   'El Salvador',        14),
    ('NIC',  'Nicaragüense',  'Nicaragua',          15),
    ('COS',  'Costarricense', 'Costa Rica',         16),
    ('PAN',  'Panameña',      'Panamá',             17),
    ('CUB',  'Cubana',        'Cuba',               18),
    ('DOM',  'Dominicana',    'República Dom.',     19),
    ('OTRA', 'Otra',          'Otra nacionalidad',  20)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'NAC' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.3  TIPO DE IDENTIFICACIÓN  (TID)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('C', 'Cédula',         'Cédula de identidad Ecuador (10 dígitos)',   1),
    ('R', 'RUC',            'Registro Único de Contribuyentes (13 díg)',  2),
    ('P', 'Pasaporte',      'Pasaporte internacional (hasta 20 chars)',   3),
    ('O', 'Otro documento', 'Otro tipo de documento de identidad',        4)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'TID' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.4  DEPARTAMENTO  (DEP)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('DEV',  'Desarrollo',                'Ingeniería y desarrollo de software',  1),
    ('SEG',  'Seguridad e Informática',   'Seguridad, soporte e infraestructura', 2),
    ('PRO',  'Procesos',                  'Análisis de procesos y funcional',     3),
    ('PRY',  'Proyectos',                 'Gestión de proyectos y productos',     4),
    ('ADM',  'Administración',            'Administración, marketing y contable', 5),
    ('RRHH', 'Recursos Humanos',          'Gestión del talento humano',           6),
    ('COM',  'Comercial',                 'Ventas y desarrollo de negocio',       7)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'DEP' AND c.TipoCatalogo = 'ADM';

-- -----------------------------------------------------------------------------
-- 2.5 POBLAR CARGOS POR DEPARTAMENTO (tbl_administracion_cargo)
--     El IdDepartamento apunta al detalle del catálogo DEP corregido arriba.
--     Habilita el flujo Departamento → Cargo del frontend.
-- -----------------------------------------------------------------------------
-- Desarrollo
INSERT INTO administracion.tbl_administracion_cargo (IdDepartamento, NombreCargo, Activo, UsuarioCreacion, IpCreacion)
SELECT d.Id, x.NombreCargo, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo_detalle d
CROSS JOIN (VALUES
    ('Desarrollador Fullstack'),
    ('Analista QA'),
    ('DevOps'),
    ('Desarrollador Backend'),
    ('Desarrollador Frontend'),
    ('Desarrollador Web'),
    ('Desarrollador Android'),
    ('Desarrollador Cobol'),
    ('Desarrollador iOS'),
    ('Desarrollador Java'),
    ('Desarrollador PHP'),
    ('Desarrollador Visual FoxPro')
) AS x(NombreCargo)
WHERE d.CodigoValor = 'DEV'
  AND d.IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo='DEP' AND TipoCatalogo='ADM');

-- Seguridad e Informática
INSERT INTO administracion.tbl_administracion_cargo (IdDepartamento, NombreCargo, Activo, UsuarioCreacion, IpCreacion)
SELECT d.Id, x.NombreCargo, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo_detalle d
CROSS JOIN (VALUES
    ('Analista Middleware'),
    ('Soporte Técnico'),
    ('Líder de Seguridad e Informática'),
    ('Help Desk')
) AS x(NombreCargo)
WHERE d.CodigoValor = 'SEG'
  AND d.IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo='DEP' AND TipoCatalogo='ADM');

-- Procesos
INSERT INTO administracion.tbl_administracion_cargo (IdDepartamento, NombreCargo, Activo, UsuarioCreacion, IpCreacion)
SELECT d.Id, x.NombreCargo, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo_detalle d
CROSS JOIN (VALUES
    ('Analista de Procesos'),
    ('Analista Funcional')
) AS x(NombreCargo)
WHERE d.CodigoValor = 'PRO'
  AND d.IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo='DEP' AND TipoCatalogo='ADM');

-- Proyectos
INSERT INTO administracion.tbl_administracion_cargo (IdDepartamento, NombreCargo, Activo, UsuarioCreacion, IpCreacion)
SELECT d.Id, x.NombreCargo, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo_detalle d
CROSS JOIN (VALUES
    ('Gerente de Proyectos y Producto'),
    ('Coordinador de Proyectos'),
    ('Gestor de Proyectos'),
    ('Líder de Proyectos y Productos'),
    ('Líder Técnico')
) AS x(NombreCargo)
WHERE d.CodigoValor = 'PRY'
  AND d.IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo='DEP' AND TipoCatalogo='ADM');

-- Administración
INSERT INTO administracion.tbl_administracion_cargo (IdDepartamento, NombreCargo, Activo, UsuarioCreacion, IpCreacion)
SELECT d.Id, x.NombreCargo, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo_detalle d
CROSS JOIN (VALUES
    ('Jefe Administrativo'),
    ('Asistente de Marketing'),
    ('Asistente Administrativo'),
    ('Asistente Contable')
) AS x(NombreCargo)
WHERE d.CodigoValor = 'ADM'
  AND d.IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo='DEP' AND TipoCatalogo='ADM');

-- Recursos Humanos
INSERT INTO administracion.tbl_administracion_cargo (IdDepartamento, NombreCargo, Activo, UsuarioCreacion, IpCreacion)
SELECT d.Id, x.NombreCargo, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo_detalle d
CROSS JOIN (VALUES
    ('Analista de Talento Humano'),
    ('Líder de Talento Humano')
) AS x(NombreCargo)
WHERE d.CodigoValor = 'RRHH'
  AND d.IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo='DEP' AND TipoCatalogo='ADM');

-- Comercial
INSERT INTO administracion.tbl_administracion_cargo (IdDepartamento, NombreCargo, Activo, UsuarioCreacion, IpCreacion)
SELECT d.Id, x.NombreCargo, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo_detalle d
CROSS JOIN (VALUES
    ('Gerente Comercial'),
    ('Ejecutivo Comercial'),
    ('Asistente Comercial')
) AS x(NombreCargo)
WHERE d.CodigoValor = 'COM'
  AND d.IdCatalogo = (SELECT Id FROM administracion.tbl_administracion_catalogo WHERE Codigo='DEP' AND TipoCatalogo='ADM');

-- ---------------------------------------------------------------------------
-- 2.6  MODO DE TRABAJO  (MDT)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('P', 'Presencial', 'Trabajo 100% en oficina',            1),
    ('R', 'Remoto',     'Trabajo 100% desde casa',            2),
    ('H', 'Híbrido',    'Combinación de presencial y remoto', 3)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'MDT' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.7  CATEGORÍA DEL EMPLEADO  (CAT)
-- Junior, Semi-Senior, Senior, Especialista, Especialista Plus
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('JR',  'Junior',           'Nivel inicial',                     1),
    ('SSR', 'Semi-Senior',      'Nivel intermedio',                  2),
    ('SR',  'Senior',           'Nivel avanzado',                    3),
    ('ESP', 'Especialista',     'Especialista en su área',           4),
    ('ESP2','Especialista Plus','Especialista de alto nivel',        5)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'CAT' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.8  EMPRESA DEL GRUPO  (EMP)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, ValorExtra, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, d.ValorExtra, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('ISC', 'Integrity Solutions',   'Empresa matriz del grupo', 1, 'MATRIZ'),
    ('RPS', 'Risk Process Solutions','Operaciones en Ecuador',   2, 'FILIAL'),
	('RISC', 'RPS E ISC', 'Pertenece a ambas empresas del grupo', 3, 'AMBAS')
) AS d(CodigoValor, Valor, Descripcion, Orden, ValorExtra)
WHERE c.Codigo = 'EMP' AND c.TipoCatalogo = 'ADM';

-- ---------------------------------------------------------------------------
-- 2.9  TIPO DE CONTRATO  (TCT)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('FJ',  'Fijo',        'Contrato indefinido a tiempo completo',  1),
    ('CON', 'Por contrato','Contrato a plazo fijo o por proyecto',   2),
    ('MT',  'Medio tiempo','Contrato a tiempo parcial',              3),
    ('PAS', 'Pasantía',    'Pasante o practicante universitario',    4),
    ('COS', 'Consultor',   'Prestación de servicios / honorarios',   5)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'TCT' AND c.TipoCatalogo = 'ADM';

-- =============================================================================
-- BLOQUE 3 — autenticacion.tbl_autenticacion_modulo
-- NOTA: 'Lideres' sin tilde — estándar para datos técnicos en DB
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 3.1  MÓDULOS RAÍZ
-- ---------------------------------------------------------------------------
INSERT INTO autenticacion.tbl_autenticacion_modulo
    (NombreModulo, RutaModulo, OrdenVisualizacion, IdModuloPadre, Activo, UsuarioCreacion, IpCreacion)
SELECT v.NombreModulo, v.RutaModulo, v.OrdenVisualizacion, NULL, TRUE, 'SYSTEM', '127.0.0.1'
FROM (VALUES
    ('Dashboard',      '/menu/dashboard',   1),
    ('Proyectos',      '/menu/projects',    2),
    ('Time Report',    NULL,                3),
    ('Requerimientos', NULL,                4),
    ('Colaboradores',  '/menu/employees',   5),
    ('Clientes',       '/menu/clients',     6),
    ('Lideres',        '/menu/leaders',     7),
    ('Reportes',       NULL,                8),
    ('Configuracion',  NULL,                9)
) AS v(NombreModulo, RutaModulo, OrdenVisualizacion)
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_modulo m
    WHERE m.NombreModulo = v.NombreModulo
);

-- ---------------------------------------------------------------------------
-- 3.2  SUBMÓDULOS — TIME REPORT
-- ---------------------------------------------------------------------------
INSERT INTO autenticacion.tbl_autenticacion_modulo
    (NombreModulo, RutaModulo, OrdenVisualizacion, IdModuloPadre, Activo, UsuarioCreacion, IpCreacion)
SELECT v.NombreModulo, v.RutaModulo, v.Orden, p.Id, TRUE, 'SYSTEM', '127.0.0.1'
FROM (VALUES
    ('Actividades', '/menu/time-reports/activities', 1),
    ('Seguimiento', '/menu/time-reports/tracking',   2)
) AS v(NombreModulo, RutaModulo, Orden)
JOIN autenticacion.tbl_autenticacion_modulo p ON p.NombreModulo = 'Time Report'
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_modulo m
    WHERE m.NombreModulo = v.NombreModulo
);

-- ---------------------------------------------------------------------------
-- 3.3  SUBMÓDULOS — REQUERIMIENTOS
-- ---------------------------------------------------------------------------
INSERT INTO autenticacion.tbl_autenticacion_modulo
    (NombreModulo, RutaModulo, OrdenVisualizacion, IdModuloPadre, Activo, UsuarioCreacion, IpCreacion)
SELECT v.NombreModulo, v.RutaModulo, v.Orden, p.Id, TRUE, 'SYSTEM', '127.0.0.1'
FROM (VALUES
    ('Solicitud de requerimiento',  '/menu/requirements/request-requirements',   1),
    ('Historial de requerimiento',  '/menu/requirements/historial-requirements', 2)
) AS v(NombreModulo, RutaModulo, Orden)
JOIN autenticacion.tbl_autenticacion_modulo p ON p.NombreModulo = 'Requerimientos'
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_modulo m
    WHERE m.NombreModulo = v.NombreModulo
);

-- ---------------------------------------------------------------------------
-- 3.4  SUBMÓDULOS — REPORTES
-- ---------------------------------------------------------------------------
INSERT INTO autenticacion.tbl_autenticacion_modulo
    (NombreModulo, RutaModulo, OrdenVisualizacion, IdModuloPadre, Activo, UsuarioCreacion, IpCreacion)
SELECT v.NombreModulo, v.RutaModulo, v.Orden, p.Id, TRUE, 'SYSTEM', '127.0.0.1'
FROM (VALUES
    ('Proyecto por horas',  '/menu/reports/hours', 1),
    ('Proyecto por fechas', '/menu/reports/dates',  2)
) AS v(NombreModulo, RutaModulo, Orden)
JOIN autenticacion.tbl_autenticacion_modulo p ON p.NombreModulo = 'Reportes'
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_modulo m
    WHERE m.NombreModulo = v.NombreModulo
);

-- ---------------------------------------------------------------------------
-- 3.5  SUBMÓDULOS — CONFIGURACIÓN
-- ---------------------------------------------------------------------------
INSERT INTO autenticacion.tbl_autenticacion_modulo
    (NombreModulo, RutaModulo, OrdenVisualizacion, IdModuloPadre, Activo, UsuarioCreacion, IpCreacion)
SELECT v.NombreModulo, v.RutaModulo, v.Orden, p.Id, TRUE, 'SYSTEM', '127.0.0.1'
FROM (VALUES
    ('Roles',         '/menu/settings/rols',     1),
    ('Usuarios',      '/menu/settings/users',    2),
    ('Dias Festivos', '/menu/settings/holidays', 3)
) AS v(NombreModulo, RutaModulo, Orden)
JOIN autenticacion.tbl_autenticacion_modulo p ON p.NombreModulo = 'Configuracion'
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_modulo m
    WHERE m.NombreModulo = v.NombreModulo
);

-- =============================================================================
-- BLOQUE 4 — autenticacion.tbl_autenticacion_rol
-- =============================================================================

INSERT INTO autenticacion.tbl_autenticacion_rol
    (Nombre, Descripcion, EsSistema, Activo, UsuarioCreacion, IpCreacion)
VALUES
    ('Administrador',    'Acceso completo al sistema',                             TRUE, TRUE, 'SYSTEM', '127.0.0.1'),
    ('Gerente',          'Puede ver reportes y aprobar solicitudes',               TRUE, TRUE, 'SYSTEM', '127.0.0.1'),
    ('Lider',            'Puede gestionar proyectos y actividades de su equipo',   TRUE, TRUE, 'SYSTEM', '127.0.0.1'),
    ('Colaborador',      'Puede registrar actividades y ver sus reportes',         TRUE, TRUE, 'SYSTEM', '127.0.0.1'),
    ('Recursos Humanos', 'Puede gestionar los colaboradores y usuarios',           TRUE, TRUE, 'SYSTEM', '127.0.0.1'),
    ('Administrativo',   'Puede gestionar clientes y dar seguimiento de recursos', TRUE, TRUE, 'SYSTEM', '127.0.0.1')
ON CONFLICT (Nombre) DO NOTHING;

-- =============================================================================
-- BLOQUE 5 — autenticacion.tbl_autenticacion_permiso
-- =============================================================================

-- DASHBOARD
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'DASHBOARD_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ', 'Permite ver el dashboard')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Dashboard'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'DASHBOARD_' || a.Accion
);

-- PROYECTOS
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'PROYECTOS_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver proyectos'),
    ('CREATE', 'Permite crear proyectos'),
    ('UPDATE', 'Permite editar proyectos'),
    ('DELETE', 'Permite eliminar proyectos')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Proyectos'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'PROYECTOS_' || a.Accion
);

-- COLABORADORES
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'COLABORADORES_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver colaboradores'),
    ('CREATE', 'Permite crear colaboradores'),
    ('UPDATE', 'Permite editar colaboradores'),
    ('DELETE', 'Permite eliminar colaboradores')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Colaboradores'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'COLABORADORES_' || a.Accion
);

-- CLIENTES
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'CLIENTES_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver clientes'),
    ('CREATE', 'Permite crear clientes'),
    ('UPDATE', 'Permite editar clientes'),
    ('DELETE', 'Permite eliminar clientes')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Clientes'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'CLIENTES_' || a.Accion
);

-- LIDERES (sin tilde)
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'LIDERES_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver lideres'),
    ('CREATE', 'Permite crear lideres'),
    ('UPDATE', 'Permite editar lideres'),
    ('DELETE', 'Permite eliminar lideres')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Lideres'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'LIDERES_' || a.Accion
);

-- ACTIVIDADES
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'ACTIVIDADES_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver actividades'),
    ('CREATE', 'Permite crear actividades'),
    ('UPDATE', 'Permite editar actividades'),
    ('DELETE', 'Permite eliminar actividades')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Actividades'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'ACTIVIDADES_' || a.Accion
);

-- SEGUIMIENTO
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'SEGUIMIENTO_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver seguimiento'),
    ('CREATE', 'Permite crear seguimiento'),
    ('UPDATE', 'Permite editar seguimiento'),
    ('DELETE', 'Permite eliminar seguimiento')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Seguimiento'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'SEGUIMIENTO_' || a.Accion
);

-- SOLICITUD DE REQUERIMIENTO
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'REQUERIMIENTO_SOLICITUD_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver solicitudes de requerimiento'),
    ('CREATE', 'Permite crear solicitudes de requerimiento'),
    ('UPDATE', 'Permite editar solicitudes de requerimiento'),
    ('DELETE', 'Permite eliminar solicitudes de requerimiento')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Solicitud de requerimiento'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'REQUERIMIENTO_SOLICITUD_' || a.Accion
);

-- HISTORIAL DE REQUERIMIENTO
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'REQUERIMIENTO_HISTORIAL_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver historial de requerimientos'),
    ('CREATE', 'Permite crear historial de requerimientos'),
    ('UPDATE', 'Permite editar historial de requerimientos'),
    ('DELETE', 'Permite eliminar historial de requerimientos')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Historial de requerimiento'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'REQUERIMIENTO_HISTORIAL_' || a.Accion
);

-- PROYECTO POR HORAS
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'REPORTE_HORAS_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver reporte por horas'),
    ('EXPORT', 'Permite exportar reporte por horas')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Proyecto por horas'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'REPORTE_HORAS_' || a.Accion
);

-- PROYECTO POR FECHAS
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'REPORTE_FECHAS_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver reporte por fechas'),
    ('EXPORT', 'Permite exportar reporte por fechas')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Proyecto por fechas'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'REPORTE_FECHAS_' || a.Accion
);

-- ROLES
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'ROLES_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver roles'),
    ('CREATE', 'Permite crear roles'),
    ('UPDATE', 'Permite editar roles'),
    ('DELETE', 'Permite eliminar roles')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Roles'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'ROLES_' || a.Accion
);

-- USUARIOS
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'USUARIOS_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver usuarios'),
    ('CREATE', 'Permite crear usuarios'),
    ('UPDATE', 'Permite editar usuarios'),
    ('DELETE', 'Permite eliminar usuarios')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Usuarios'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'USUARIOS_' || a.Accion
);

-- DIAS FESTIVOS (sin tilde)
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (IdModulo, Codigo, Accion, Descripcion, UsuarioCreacion, IpCreacion)
SELECT m.Id, 'FESTIVOS_' || a.Accion, a.Accion, a.Descripcion, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_modulo m
CROSS JOIN (VALUES
    ('READ',   'Permite ver dias festivos'),
    ('CREATE', 'Permite crear dias festivos'),
    ('UPDATE', 'Permite editar dias festivos'),
    ('DELETE', 'Permite eliminar dias festivos')
) AS a(Accion, Descripcion)
WHERE m.NombreModulo = 'Dias Festivos'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_permiso
    WHERE Codigo = 'FESTIVOS_' || a.Accion
);

-- =============================================================================
-- BLOQUE 6 — autenticacion.tbl_autenticacion_rol_permiso
--
--  Matriz de accesos:
--  ┌─────────────────┬──────────────────────────────────────────────────────────┐
--  │ Rol             │ Módulos                                                  │
--  ├─────────────────┼──────────────────────────────────────────────────────────┤
--  │ Administrador   │ TODOS                                                    │
--  │ Gerente         │ Proyectos, Actividades, Seguimiento, Clientes, Lideres   │
--  │ Lider           │ Proyectos, Actividades, Seguimiento,                     │
--  │                 │ Colaboradores, Proyecto por horas, Proyecto por fechas   │
--  │ Colaborador     │ Actividades                                              │
--  │ Recursos Humanos│ Dashboard, Seguimiento, Colaboradores                    │
--  │ Administrativo  │ Dashboard, Seguimiento, Clientes, Actividades,           │
--  │                 │ Dias Festivos                                            │
--  └─────────────────┴──────────────────────────────────────────────────────────┘
-- =============================================================================

-- ADMINISTRADOR — todos los permisos
INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
    (IdRol, IdPermiso, UsuarioCreacion, IpCreacion)
SELECT r.Id, p.Id, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_rol r
CROSS JOIN autenticacion.tbl_autenticacion_permiso p
WHERE r.Nombre = 'Administrador'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_rol_permiso
    WHERE IdRol = r.Id AND IdPermiso = p.Id
);

-- GERENTE — Proyectos, Actividades, Seguimiento, Clientes, Lideres
INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
    (IdRol, IdPermiso, UsuarioCreacion, IpCreacion)
SELECT r.Id, p.Id, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_rol r
JOIN autenticacion.tbl_autenticacion_permiso p ON TRUE
JOIN autenticacion.tbl_autenticacion_modulo m ON m.Id = p.IdModulo
WHERE r.Nombre = 'Gerente'
AND m.NombreModulo IN ('Proyectos', 'Actividades', 'Seguimiento', 'Clientes', 'Lideres')
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_rol_permiso
    WHERE IdRol = r.Id AND IdPermiso = p.Id
);

-- LÍDER — Proyectos, Actividades, Seguimiento, Colaboradores, Reportes por horas y fechas
INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
    (IdRol, IdPermiso, UsuarioCreacion, IpCreacion)
SELECT r.Id, p.Id, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_rol r
JOIN autenticacion.tbl_autenticacion_permiso p ON TRUE
JOIN autenticacion.tbl_autenticacion_modulo m ON m.Id = p.IdModulo
WHERE r.Nombre = 'Lider'
AND m.NombreModulo IN (
    'Proyectos', 'Actividades', 'Seguimiento',
    'Colaboradores', 'Proyecto por horas', 'Proyecto por fechas'
)
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_rol_permiso
    WHERE IdRol = r.Id AND IdPermiso = p.Id
);

-- COLABORADOR — solo Actividades
INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
    (IdRol, IdPermiso, UsuarioCreacion, IpCreacion)
SELECT r.Id, p.Id, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_rol r
JOIN autenticacion.tbl_autenticacion_permiso p ON TRUE
JOIN autenticacion.tbl_autenticacion_modulo m ON m.Id = p.IdModulo
WHERE r.Nombre = 'Colaborador'
AND m.NombreModulo = 'Actividades'
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_rol_permiso
    WHERE IdRol = r.Id AND IdPermiso = p.Id
);

-- RECURSOS HUMANOS — Dashboard, Seguimiento, Colaboradores
INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
    (IdRol, IdPermiso, UsuarioCreacion, IpCreacion)
SELECT r.Id, p.Id, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_rol r
JOIN autenticacion.tbl_autenticacion_permiso p ON TRUE
JOIN autenticacion.tbl_autenticacion_modulo m ON m.Id = p.IdModulo
WHERE r.Nombre = 'Recursos Humanos'
AND m.NombreModulo IN ('Dashboard', 'Seguimiento', 'Colaboradores')
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_rol_permiso
    WHERE IdRol = r.Id AND IdPermiso = p.Id
);

-- ADMINISTRATIVO — Dashboard, Seguimiento, Clientes, Actividades, Dias Festivos
INSERT INTO autenticacion.tbl_autenticacion_rol_permiso
    (IdRol, IdPermiso, UsuarioCreacion, IpCreacion)
SELECT r.Id, p.Id, 'SYSTEM', '127.0.0.1'
FROM autenticacion.tbl_autenticacion_rol r
JOIN autenticacion.tbl_autenticacion_permiso p ON TRUE
JOIN autenticacion.tbl_autenticacion_modulo m ON m.Id = p.IdModulo
WHERE r.Nombre = 'Administrativo'
AND m.NombreModulo IN ('Dashboard', 'Seguimiento', 'Clientes', 'Actividades', 'Dias Festivos')
AND NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_rol_permiso
    WHERE IdRol = r.Id AND IdPermiso = p.Id
);

-- =============================================================================
-- BLOQUE 7 — VERIFICACIÓN FINAL
-- =============================================================================
DO $$
DECLARE
    v_catalogos   INTEGER;
    v_detalles    INTEGER;
    v_modulos     INTEGER;
    v_roles       INTEGER;
    v_permisos    INTEGER;
    v_rol_permiso INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_catalogos
    FROM administracion.tbl_administracion_catalogo
    WHERE UsuarioCreacion = 'SYSTEM';

    SELECT COUNT(*) INTO v_detalles
    FROM administracion.tbl_administracion_catalogo_detalle
    WHERE UsuarioCreacion = 'SYSTEM';

    SELECT COUNT(*) INTO v_modulos
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE UsuarioCreacion = 'SYSTEM';

    SELECT COUNT(*) INTO v_roles
    FROM autenticacion.tbl_autenticacion_rol
    WHERE UsuarioCreacion = 'SYSTEM';

    SELECT COUNT(*) INTO v_permisos
    FROM autenticacion.tbl_autenticacion_permiso
    WHERE UsuarioCreacion = 'SYSTEM';

    SELECT COUNT(*) INTO v_rol_permiso
    FROM autenticacion.tbl_autenticacion_rol_permiso
    WHERE UsuarioCreacion = 'SYSTEM';

    RAISE NOTICE '===========================================';
    RAISE NOTICE '  SEED COMPLETO — RESUMEN';
    RAISE NOTICE '===========================================';
    RAISE NOTICE '  Catálogos cabecera   : % (esperado: 11)',  v_catalogos;
    RAISE NOTICE '  Detalles catálogo    : % (esperado: 75)',  v_detalles;
    RAISE NOTICE '  Módulos              : % (esperado: 16)',  v_modulos;
    RAISE NOTICE '  Roles                : % (esperado: 6)',   v_roles;
    RAISE NOTICE '  Permisos             : % (esperado: 53)',  v_permisos;
    RAISE NOTICE '  Rol-Permiso          : %',                 v_rol_permiso;
    RAISE NOTICE '===========================================';
END $$;

-- =============================================================================
-- BLOQUE 8 — administracion.tbl_administracion_catalogo  (cabeceras TMR)
-- Catálogos propios del módulo TMR (Time Report)
-- =============================================================================

INSERT INTO administracion.tbl_administracion_catalogo
    (TipoCatalogo, Codigo, Descripcion, Activo, UsuarioCreacion, IpCreacion)
VALUES
    ('TMR', 'EST', 'Estados de proyecto para carga de actividades', TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR', 'TPR', 'Tipos de proyecto',                             TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR', 'EPR', 'Estado proyectos',                              TRUE, 'SYSTEM', '127.0.0.1')
ON CONFLICT (TipoCatalogo, Codigo) DO NOTHING;

-- =============================================================================
-- BLOQUE 9 — administracion.tbl_administracion_catalogo_detalle  (detalles TMR)
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 9.1  ESTADOS DE PROYECTO PARA CARGA DE ACTIVIDADES  (EST)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('ACT', 'Activo',   'Proyecto activo para carga de actividades',   1),
    ('INA', 'Inactivo', 'Proyecto inactivo para carga de actividades', 2),
    ('CER', 'Cerrado',  'Proyecto cerrado para carga de actividades',  3)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'EST' AND c.TipoCatalogo = 'TMR'
ON CONFLICT (IdCatalogo, CodigoValor) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 9.2  TIPOS DE PROYECTO  (TPR)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('ADM', 'Administración',        'Proyecto administrativo', 1),
    ('CAP', 'Capacitación',          'Proyecto capacitación',  2),
    ('FSC', 'Fact. Subcontratación', 'Proyecto subcontratado',  3),
    ('FLM', 'Fact. Llave Mano',      'Proyecto llave en mano',  4)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'TPR' AND c.TipoCatalogo = 'TMR'
ON CONFLICT (IdCatalogo, CodigoValor) DO NOTHING;

-- ---------------------------------------------------------------------------
-- 9.3  ESTADO PROYECTOS  (EPR)
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('APL', 'Aplazado',      'Estado aplazado',      1),
    ('APR', 'Aprobado',      'Estado aprobado',      2),
    ('CAN', 'Cancelado',     'Estado cancelado',     3),
    ('COM', 'Completado',    'Estado completado',    4),
    ('ESP', 'En Espera',     'Estado en espera',     5),
    ('PRO', 'En Progreso',   'Estado en progreso',   6),
    ('PLN', 'Planificación', 'Estado planificación', 7)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'EPR' AND c.TipoCatalogo = 'TMR'
ON CONFLICT (IdCatalogo, CodigoValor) DO NOTHING;


INSERT INTO administracion.tbl_administracion_catalogo
    (TipoCatalogo, Codigo, Descripcion, Activo, UsuarioCreacion, IpCreacion)
VALUES
    ('ADM', 'TLI', 'Tipo de líder', TRUE, 'SYSTEM', '127.0.0.1');
-- ---------------------------------------------------------------------------
-- 10  TIPO DE LIDER  (TLI) tbl_administracion_catalogo_detalle
-- ---------------------------------------------------------------------------
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT c.Id, d.CodigoValor, d.Valor, d.Descripcion, d.Orden, TRUE, 'SYSTEM', '127.0.0.1'
FROM administracion.tbl_administracion_catalogo c
CROSS JOIN (VALUES
    ('INT', 'Interno', 'Líder interno de la empresa', 1),
    ('EXT', 'Externo', 'Líder externo de la empresa', 2)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE c.Codigo = 'TLI' AND c.TipoCatalogo = 'ADM';

COMMIT;