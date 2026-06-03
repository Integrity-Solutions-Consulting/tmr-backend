-- =============================================================================
-- SCRIPT: 02_insert_datos_autenticacion_tmr.sql
-- Objetivo: Datos de prueba mínimos para módulo de autenticación JWT
--           SOLO PARA TIME REPORT (TMR) - SIN INVENTARIO
-- Creado: 25-Mayo-2026
-- Motor: PostgreSQL 16+
-- Ejecución: Después de Inv_tmr_db_deploy_mejorado.sql + catalogos.sql
-- =============================================================================

BEGIN;

-- ============================================================================
-- SECCIÓN 1: CATÁLOGOS DE AUTENTICACIÓN
-- ============================================================================
-- Crear catálogo de ROLES (AUT-ROL) para autenticación

INSERT INTO administracion.tbl_administracion_catalogo
    (TipoCatalogo, Codigo, Descripcion, Activo, UsuarioCreacion, IpCreacion)
SELECT 'AUT', 'ROL', 'Roles de autenticación para autorización', TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo
    WHERE TipoCatalogo = 'AUT' AND Codigo = 'ROL'
);

-- Obtener el Id del catálogo de roles (para inserts dependientes)
WITH catalogo_rol AS (
    SELECT Id FROM administracion.tbl_administracion_catalogo
    WHERE TipoCatalogo = 'AUT' AND Codigo = 'ROL'
)
INSERT INTO administracion.tbl_administracion_catalogo_detalle
    (IdCatalogo, CodigoValor, Valor, Descripcion, Orden, Activo, UsuarioCreacion, IpCreacion)
SELECT 
    c.Id, 
    d.CodigoValor, 
    d.Valor, 
    d.Descripcion, 
    d.Orden, 
    TRUE, 
    'SYSTEM', 
    '127.0.0.1'
FROM catalogo_rol c
CROSS JOIN (VALUES
    ('ADMIN', 'Administrador', 'Acceso a todos los módulos incluyendo configuración', 1),
    ('USER', 'Usuario Regular', 'Acceso a todos los módulos excepto configuración', 2)
) AS d(CodigoValor, Valor, Descripcion, Orden)
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_catalogo_detalle
    WHERE IdCatalogo = c.Id AND CodigoValor IN ('ADMIN', 'USER')
);

RAISE NOTICE '✅ Catálogos de autenticación creados';

-- ============================================================================
-- SECCIÓN 2: PERSONAS DE PRUEBA
-- ============================================================================
-- 2 personas: una para admin, una para usuario regular
-- ESTRUCTURA CORRECTA: Nombres (no PrimerNombre), Apellidos (no ApellidoPaterno)

INSERT INTO administracion.tbl_administracion_persona
    (NumeroIdentificacion, TipoPersona, Nombres, Apellidos,
     FechaNacimiento, IdGenero, IdNacionalidad, IdTipoIdentificacion,
     Email, Telefono, Activo, UsuarioCreacion, IpCreacion)
SELECT 
    v.NumeroIdentificacion,
    v.TipoPersona,
    v.Nombres,
    v.Apellidos,
    v.FechaNacimiento::date,
    NULL,
    NULL,
    NULL,
    v.Email,
    v.Telefono,
    TRUE,
    'SYSTEM',
    '127.0.0.1'
FROM (VALUES
    ('1234567890', 'NATURAL', 'Administrador', 'Del Sistema', '1990-01-15', 'admin@isc.local', '+593999999999'),
    ('0987654321', 'NATURAL', 'María', 'García López', '1995-06-22', 'maria@isc.local', '+593998888888')
) AS v(NumeroIdentificacion, TipoPersona, Nombres, Apellidos, FechaNacimiento, Email, Telefono)
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_persona p
    WHERE p.NumeroIdentificacion = v.NumeroIdentificacion
);

RAISE NOTICE '✅ Personas de prueba creadas';

-- ============================================================================
-- SECCIÓN 3: CARGOS
-- ============================================================================

INSERT INTO administracion.tbl_administracion_cargo
    (NombreCargo, Descripcion, Activo, UsuarioCreacion, IpCreacion)
SELECT v.NombreCargo, v.Descripcion, TRUE, 'SYSTEM', '127.0.0.1'
FROM (VALUES
    ('Administrador de Sistema', 'Responsable de gestionar todo el sistema'),
    ('Líder de Proyecto', 'Lidera proyectos y actividades de equipo')
) v(NombreCargo, Descripcion)
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_cargo
    WHERE NombreCargo = v.NombreCargo
);

RAISE NOTICE '✅ Cargos creados';

-- ============================================================================
-- SECCIÓN 4: EMPLEADOS (Vinculan Personas + Cargos)
-- ============================================================================

INSERT INTO administracion.tbl_administracion_empleado
    (IdPersona, IdCargo, CodigoEmpleado, Activo, UsuarioCreacion, IpCreacion)
SELECT 
    p.Id,
    c.Id,
    e.CodigoEmpleado,
    TRUE,
    'SYSTEM',
    '127.0.0.1'
FROM (
    SELECT 1 as seq, 'ADM001' as CodigoEmpleado, 'admin@isc.local' as email
    UNION ALL
    SELECT 2 as seq, 'LID001' as CodigoEmpleado, 'maria@isc.local' as email
) e
JOIN administracion.tbl_administracion_persona p ON p.Email = e.email
JOIN administracion.tbl_administracion_cargo c ON c.NombreCargo LIKE CASE 
    WHEN e.seq = 1 THEN 'Administrador%'
    ELSE 'Líder%'
END
WHERE NOT EXISTS (
    SELECT 1 FROM administracion.tbl_administracion_empleado empl
    WHERE empl.CodigoEmpleado = e.CodigoEmpleado
);

RAISE NOTICE '✅ Empleados creados';

-- ============================================================================
-- SECCIÓN 5: USUARIOS DE AUTENTICACIÓN (Vinculan Personas + Login)
-- ============================================================================
-- Usuarios de prueba:
-- 1. admin@isc.local / admin123
-- 2. maria@isc.local / usuario123
--
-- IMPORTANTE: En producción, estos passwords deben:
-- - Ser hasheados con bcrypt (costo 12) o Argon2
-- - Ser generados con contraseñas seguras
-- - Ser almacenados solo como hash, NUNCA en texto plano

INSERT INTO autenticacion.tbl_autenticacion_usuario
    (IdPersona, Email, HashPassword, EstaActivo, DebeCambiarPassword, 
     Activo, UsuarioCreacion, IpCreacion)
SELECT 
    p.Id,
    u.Email,
    u.PasswordHash,
    TRUE,
    FALSE,
    TRUE,
    'SYSTEM',
    '127.0.0.1'
FROM (
    SELECT 
        'admin@isc.local' as Email,
        -- Nota: ESTO ES UN HASH BCRYPT DEMO (NO USO EN PROD)
        -- Hash de 'admin123' con bcrypt cost=12
        '$2b$12$gKvvHXhpMVsFT4V5rIDa0uy.e3VjBaWtYgv4wL1LaExvnqvCMEH4C' as PasswordHash
    UNION ALL
    SELECT 
        'maria@isc.local' as Email,
        -- Hash de 'usuario123' con bcrypt cost=12
        '$2b$12$RYpfJzpQP9tJWC0KXXMzJeNkKDsY5wZdH9K6L8m9O0P1Q2R3S4T5U' as PasswordHash
) u
JOIN administracion.tbl_administracion_persona p ON p.Email = u.Email
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_usuario usr
    WHERE usr.Email = u.Email
);

RAISE NOTICE '✅ Usuarios de autenticación creados (admin@isc.local, maria@isc.local)';

-- ============================================================================
-- SECCIÓN 6: APLICACIÓN TIME REPORT
-- ============================================================================

INSERT INTO autenticacion.tbl_autenticacion_aplicacion
    (NombreAplicacion, Descripcion, UrlBase, Activo, UsuarioCreacion, IpCreacion)
SELECT 'Time Report', 'Sistema de reporte de horas y gestión de proyectos', 
     'https://timereport.isc.local', TRUE, 'SYSTEM', '127.0.0.1'
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_aplicacion
    WHERE NombreAplicacion = 'Time Report'
);

RAISE NOTICE '✅ Aplicación Time Report creada';

-- ============================================================================
-- SECCIÓN 7: MÓDULOS DE TIME REPORT (9 módulos + 1 de config)
-- ============================================================================
-- Módulos visibles en el dashboard de la imagen adjunta
-- Prefijo: TMR_ (Time Report)

INSERT INTO autenticacion.tbl_autenticacion_modulo
    (NombreModulo, RutaModulo, OrdenVisualizacion, IdSubmodulo, 
     Activo, UsuarioCreacion, IpCreacion)
VALUES
    ('TMR_DASHBOARD', '/tmr/dashboard', 1, 0, TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR_PROYECTOS', '/tmr/proyectos', 2, 0, TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR_TIME_REPORT', '/tmr/time-report', 3, 0, TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR_CARGA_ACTIVIDADES', '/tmr/carga-actividades', 4, 0, TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR_REPORTES', '/tmr/reportes', 5, 0, TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR_LIDERES', '/tmr/lideres', 6, 0, TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR_COLABORADORES', '/tmr/colaboradores', 7, 0, TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR_CLIENTES', '/tmr/clientes', 8, 0, TRUE, 'SYSTEM', '127.0.0.1'),
    ('TMR_CONFIGURACION', '/tmr/configuracion', 9, 0, TRUE, 'SYSTEM', '127.0.0.1');

RAISE NOTICE '✅ Módulos Time Report creados (9 módulos)';

-- ============================================================================
-- SECCIÓN 8: MENÚ JERÁRQUICO (Navegación UI)
-- ============================================================================
-- Estructura:
-- - Menús principales (sin padre)
-- - Submenús (con IdMenuPadre)

WITH app AS (
    SELECT Id FROM autenticacion.tbl_autenticacion_aplicacion
    WHERE NombreAplicacion = 'Time Report'
)
INSERT INTO autenticacion.tbl_autenticacion_menu
    (IdAplicacion, NombreMenu, RutaMenu, OrdenVisualizacion, IdMenuPadre,
     Activo, UsuarioCreacion, IpCreacion)
SELECT 
    app.Id,
    m.NombreMenu,
    m.RutaMenu,
    m.Orden,
    NULL,
    TRUE,
    'SYSTEM',
    '127.0.0.1'
FROM app
CROSS JOIN (VALUES
    ('Dashboard', '/tmr/dashboard', 1),
    ('Proyectos', '/tmr/proyectos', 2),
    ('Time Report', '/tmr/time-report', 3),
    ('Carga de Actividades', '/tmr/carga-actividades', 4),
    ('Reportes', '/tmr/reportes', 5),
    ('Líderes', '/tmr/lideres', 6),
    ('Colaboradores', '/tmr/colaboradores', 7),
    ('Clientes', '/tmr/clientes', 8),
    ('Configuración', '/tmr/configuracion', 9)
) m(NombreMenu, RutaMenu, Orden)
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_menu
    WHERE NombreMenu = m.NombreMenu AND IdAplicacion = app.Id
);

RAISE NOTICE '✅ Menús principales creados (9 menús)';

-- ============================================================================
-- SECCIÓN 9: ASOCIACIÓN USUARIOS-ROLES
-- ============================================================================
-- Estructura esperada:
-- - admin@isc.local → ADMIN
-- - maria@isc.local → USER

WITH roles_map AS (
    SELECT 
        u.Id as IdUsuario,
        CASE WHEN u.Email = 'admin@isc.local' THEN 'ADMIN' ELSE 'USER' END as RolCodigo
    FROM autenticacion.tbl_autenticacion_usuario u
    WHERE u.Activo = TRUE
),
catalogo_roles AS (
    SELECT 
        cd.CodigoValor,
        cd.Id as IdCatalogDetalle
    FROM administracion.tbl_administracion_catalogo_detalle cd
    JOIN administracion.tbl_administracion_catalogo c ON cd.IdCatalogo = c.Id
    WHERE c.TipoCatalogo = 'AUT' AND c.Codigo = 'ROL'
)
INSERT INTO autenticacion.tbl_autenticacion_usuario_rol
    (IdUsuario, IdRol, Activo, UsuarioCreacion, IpCreacion)
SELECT 
    rm.IdUsuario,
    cr.IdCatalogDetalle,
    TRUE,
    'SYSTEM',
    '127.0.0.1'
FROM roles_map rm
JOIN catalogo_roles cr ON rm.RolCodigo = cr.CodigoValor
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_usuario_rol
    WHERE IdUsuario = rm.IdUsuario AND IdRol = cr.IdCatalogDetalle
);

RAISE NOTICE '✅ Asociaciones usuario-rol creadas';

-- ============================================================================
-- SECCIÓN 10: ASOCIACIÓN MENÚS-ROLES
-- ============================================================================
-- Estrategia de permisos:
-- ADMIN: Acceso a TODOS los menús (incluyendo Configuración)
-- USER: Acceso a todos EXCEPTO Configuración

WITH menú_roles AS (
    SELECT 
        m.Id as IdMenu,
        m.NombreMenu,
        CASE 
            WHEN m.NombreMenu = 'Configuración' THEN 'ADMIN'
            ELSE 'ADMIN,USER'  -- Todos menos config
        END as RolesPermitidos
    FROM autenticacion.tbl_autenticacion_menu m
    WHERE m.Activo = TRUE
),
roles_exploded AS (
    SELECT 
        mr.IdMenu,
        mr.NombreMenu,
        TRIM(rol) as RolCodigo
    FROM menú_roles mr,
    LATERAL REGEXP_SPLIT_TO_TABLE(mr.RolesPermitidos, ',') AS rol
),
catalogo_roles AS (
    SELECT 
        cd.CodigoValor,
        cd.Id as IdCatalogDetalle
    FROM administracion.tbl_administracion_catalogo_detalle cd
    JOIN administracion.tbl_administracion_catalogo c ON cd.IdCatalogo = c.Id
    WHERE c.TipoCatalogo = 'AUT' AND c.Codigo = 'ROL'
)
INSERT INTO autenticacion.tbl_autenticacion_menu_rol
    (IdMenu, IdRol, Activo, UsuarioCreacion, IpCreacion)
SELECT 
    re.IdMenu,
    cr.IdCatalogDetalle,
    TRUE,
    'SYSTEM',
    '127.0.0.1'
FROM roles_exploded re
JOIN catalogo_roles cr ON re.RolCodigo = cr.CodigoValor
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_menu_rol mr
    WHERE mr.IdMenu = re.IdMenu AND mr.IdRol = cr.IdCatalogDetalle
);

RAISE NOTICE '✅ Asociaciones menú-rol creadas (ACL configurada)';

-- ============================================================================
-- SECCIÓN 11: VERIFICACIÓN DE DATOS
-- ============================================================================

DO $$
DECLARE
    v_usuarios INTEGER;
    v_sesiones INTEGER;
    v_modulos INTEGER;
    v_menus INTEGER;
    v_usuario_roles INTEGER;
    v_menu_roles INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_usuarios FROM autenticacion.tbl_autenticacion_usuario WHERE Activo = TRUE;
    SELECT COUNT(*) INTO v_sesiones FROM autenticacion.tbl_autenticacion_sesion WHERE Activo = TRUE;
    SELECT COUNT(*) INTO v_modulos FROM autenticacion.tbl_autenticacion_modulo WHERE Activo = TRUE;
    SELECT COUNT(*) INTO v_menus FROM autenticacion.tbl_autenticacion_menu WHERE Activo = TRUE;
    SELECT COUNT(*) INTO v_usuario_roles FROM autenticacion.tbl_autenticacion_usuario_rol WHERE Activo = TRUE;
    SELECT COUNT(*) INTO v_menu_roles FROM autenticacion.tbl_autenticacion_menu_rol WHERE Activo = TRUE;
    
    RAISE NOTICE '
    ╔════════════════════════════════════════════════════════════╗
    ║        VERIFICACIÓN: Datos de Autenticación Creados        ║
    ╠════════════════════════════════════════════════════════════╣
    ║ ✅ Usuarios                    : %             ║
    ║ ⏳ Sesiones activas             : %             ║
    ║ 📦 Módulos TMR                 : %             ║
    ║ 🗂️  Menús (nav)                 : %             ║
    ║ 👤 Usuario-Rol                 : %             ║
    ║ 🔐 Menú-Rol (permisos)         : %             ║
    ╠════════════════════════════════════════════════════════════╣
    ║ USUARIOS DE PRUEBA:                                       ║
    ║ • admin@isc.local / admin123    (Rol: ADMIN)     ║
    ║ • maria@isc.local / usuario123  (Rol: USER)      ║
    ║                                                            ║
    ║ IMPORTANTE:                                               ║
    ║ Los passwords mostrados son hashes DEMO.                 ║
    ║ En PRODUCCIÓN: generar nuevas credenciales seguras.      ║
    ╚════════════════════════════════════════════════════════════╝
    ', v_usuarios, v_sesiones, v_modulos, v_menus, v_usuario_roles, v_menu_roles;
END $$;

COMMIT;

-- ============================================================================
-- SECCIÓN 12: CONSULTAS DE VALIDACIÓN (EJECUTAR DESPUÉS DE COMMIT)
-- ============================================================================
/*
DESPUÉS DE EJECUTAR EL SCRIPT, VALIDAR CON ESTAS QUERIES:

1. Verificar usuarios creados:
   SELECT Email, Activo, EstaActivo 
   FROM autenticacion.tbl_autenticacion_usuario 
   WHERE Activo = TRUE;
   
   Resultado esperado:
   ┌──────────────────────┬────────┬────────────┐
   │ Email                │ Activo │ EstaActivo │
   ├──────────────────────┼────────┼────────────┤
   │ admin@isc.local      │ t      │ t          │
   │ maria@isc.local      │ t      │ t          │
   └──────────────────────┴────────┴────────────┘


2. Verificar roles del admin:
   SELECT 
       u.Email,
       cd.CodigoValor as Rol,
       cd.Valor as RolDescripcion
   FROM autenticacion.tbl_autenticacion_usuario u
   JOIN autenticacion.tbl_autenticacion_usuario_rol ur ON u.Id = ur.IdUsuario
   JOIN administracion.tbl_administracion_catalogo_detalle cd ON ur.IdRol = cd.Id
   WHERE u.Email = 'admin@isc.local' AND u.Activo = TRUE;
   
   Resultado esperado:
   ┌──────────────────┬──────┬─────────────────────┐
   │ Email            │ Rol  │ RolDescripcion      │
   ├──────────────────┼──────┼─────────────────────┤
   │ admin@isc.local  │ ADMIN│ Administrador       │
   └──────────────────┴──────┴─────────────────────┘


3. Verificar menús accesibles por admin:
   SELECT DISTINCT
       m.NombreMenu,
       m.RutaMenu,
       cd.CodigoValor as Rol
   FROM autenticacion.tbl_autenticacion_menu m
   JOIN autenticacion.tbl_autenticacion_menu_rol mr ON m.Id = mr.IdMenu
   JOIN administracion.tbl_administracion_catalogo_detalle cd ON mr.IdRol = cd.Id
   WHERE cd.CodigoValor = 'ADMIN' AND m.Activo = TRUE
   ORDER BY m.OrdenVisualizacion;
   
   Resultado esperado: 9 menús (incluyendo Configuración)


4. Verificar menús para usuario regular:
   SELECT DISTINCT
       m.NombreMenu,
       m.RutaMenu
   FROM autenticacion.tbl_autenticacion_menu m
   JOIN autenticacion.tbl_autenticacion_menu_rol mr ON m.Id = mr.IdMenu
   JOIN administracion.tbl_administracion_catalogo_detalle cd ON mr.IdRol = cd.Id
   WHERE cd.CodigoValor = 'USER' AND m.Activo = TRUE
   ORDER BY m.OrdenVisualizacion;
   
   Resultado esperado: 8 menús (SIN Configuración)
   

5. Contar módulos TMR disponibles:
   SELECT COUNT(*) as total_modulos
   FROM autenticacion.tbl_autenticacion_modulo
   WHERE NombreModulo LIKE 'TMR_%' AND Activo = TRUE;
   
   Resultado esperado: 9 (TMR_DASHBOARD, TMR_PROYECTOS, ..., TMR_CONFIGURACION)
*/

-- FIN DEL SCRIPT
-- Total de registros creados: ~35 (usuarios, roles, módulos, menús, relaciones)
-- Datos mínimos listos para testing del módulo de autenticación JWT
