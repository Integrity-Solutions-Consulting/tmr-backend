-- =============================================================================
-- SCRIPT: 03_insert_permisos_rol_modulo_tmr.sql
-- Objetivo: Definir permisos CRUD de roles sobre módulos Time Report
-- Creado: 26-Mayo-2026
-- Motor: PostgreSQL 16+
-- Prerequisito: Ejecutar antes:
--   1. Inv_tmr_db_deploy_mejorado.sql
--   2. catalogos.sql
--   3. 02_insert_datos_autenticacion_tmr.sql
-- =============================================================================

BEGIN;

-- ============================================================================
-- SECCIÓN 1: PERMISOS ROL → MÓDULO (tbl_autenticacion_rol_modulo)
-- ============================================================================
-- Estrategia de permisos:
-- 
-- ADMIN:
--   - Todos los módulos con permisos CRUD completos (Ver, Crear, Editar, Eliminar)
--
-- USER:
--   - TMR_DASHBOARD: Ver (lectura de dashboard personal)
--   - TMR_PROYECTOS: Ver, Crear (crear reportes de proyectos)
--   - TMR_TIME_REPORT: Ver, Crear, Editar (crear y editar sus propios reportes)
--   - TMR_CARGA_ACTIVIDADES: Ver, Crear, Editar (carga de actividades diarias)
--   - TMR_REPORTES: Ver (reportes)
--   - TMR_LIDERES: Ver (solo consulta, sin edición)
--   - TMR_COLABORADORES: Ver (solo consulta)
--   - TMR_CLIENTES: Ver (solo consulta)
--   - TMR_CONFIGURACION: NO (no tiene acceso)

WITH roles_map AS (
    SELECT 
        cd.Id as IdRol,
        cd.CodigoValor as RolCodigo
    FROM administracion.tbl_administracion_catalogo_detalle cd
    JOIN administracion.tbl_administracion_catalogo c ON cd.IdCatalogo = c.Id
    WHERE c.TipoCatalogo = 'AUT' AND c.Codigo = 'ROL' AND cd.Activo = TRUE
),
modulos_map AS (
    SELECT 
        Id as IdModulo,
        NombreModulo
    FROM autenticacion.tbl_autenticacion_modulo
    WHERE Activo = TRUE
),
permisos_data AS (
    SELECT 
        r.IdRol,
        r.RolCodigo,
        m.IdModulo,
        m.NombreModulo,
        CASE 
            -- ADMIN: Acceso completo a TODO
            WHEN r.RolCodigo = 'ADMIN' THEN TRUE
            -- USER: Acceso a todos excepto Configuración
            WHEN r.RolCodigo = 'USER' AND m.NombreModulo NOT LIKE '%CONFIGURACION%' THEN TRUE
            ELSE FALSE
        END AS PuedeVer,
        CASE 
            -- ADMIN: Puede crear en TODO
            WHEN r.RolCodigo = 'ADMIN' THEN TRUE
            -- USER: Puede crear en Proyectos, TimeReport, CargaActividades
            WHEN r.RolCodigo = 'USER' AND m.NombreModulo IN ('TMR_PROYECTOS', 'TMR_TIME_REPORT', 'TMR_CARGA_ACTIVIDADES') THEN TRUE
            ELSE FALSE
        END AS PuedeCrear,
        CASE 
            -- ADMIN: Puede editar en TODO
            WHEN r.RolCodigo = 'ADMIN' THEN TRUE
            -- USER: Puede editar en TimeReport, CargaActividades
            WHEN r.RolCodigo = 'USER' AND m.NombreModulo IN ('TMR_TIME_REPORT', 'TMR_CARGA_ACTIVIDADES') THEN TRUE
            ELSE FALSE
        END AS PuedeEditar,
        CASE 
            -- ADMIN: Puede eliminar en TODO
            WHEN r.RolCodigo = 'ADMIN' THEN TRUE
            -- USER: NO puede eliminar nada (solo lectura o edición limitada)
            ELSE FALSE
        END AS PuedeEliminar
    FROM roles_map r
    CROSS JOIN modulos_map m
)
INSERT INTO autenticacion.tbl_autenticacion_rol_modulo
    (IdRol, IdModulo, PuedeVer, PuedeCrear, PuedeEditar, PuedeEliminar,
     Activo, UsuarioCreacion, IpCreacion)
SELECT 
    pd.IdRol,
    pd.IdModulo,
    pd.PuedeVer,
    pd.PuedeCrear,
    pd.PuedeEditar,
    pd.PuedeEliminar,
    TRUE,
    'SYSTEM',
    '127.0.0.1'
FROM permisos_data pd
WHERE pd.PuedeVer = TRUE OR pd.PuedeCrear = TRUE OR pd.PuedeEditar = TRUE OR pd.PuedeEliminar = TRUE
ON CONFLICT DO NOTHING;

RAISE NOTICE '✅ Permisos rol-módulo creados';

-- ============================================================================
-- SECCIÓN 2: RELACIÓN USUARIO → APLICACIÓN (tbl_autenticacion_usuario_aplicacion)
-- ============================================================================
-- Vincular cada usuario con la aplicación "Time Report"

WITH usuarios_activos AS (
    SELECT Id FROM autenticacion.tbl_autenticacion_usuario WHERE Activo = TRUE
),
app_tmr AS (
    SELECT Id FROM autenticacion.tbl_autenticacion_aplicacion WHERE NombreAplicacion = 'Time Report'
)
INSERT INTO autenticacion.tbl_autenticacion_usuario_aplicacion
    (IdUsuario, IdAplicacion, Activo, UsuarioCreacion, IpCreacion)
SELECT 
    u.Id,
    a.Id,
    TRUE,
    'SYSTEM',
    '127.0.0.1'
FROM usuarios_activos u
CROSS JOIN app_tmr a
WHERE NOT EXISTS (
    SELECT 1 FROM autenticacion.tbl_autenticacion_usuario_aplicacion
    WHERE IdUsuario = u.Id AND IdAplicacion = a.Id
);

RAISE NOTICE '✅ Relación usuario-aplicación creada (Time Report)';

-- ============================================================================
-- SECCIÓN 3: PERMISOS DIRECTOS DE USUARIO → MÓDULO (Casos especiales)
-- ============================================================================
-- OPCIONALES: Agregar permisos específicos por usuario que override los roles
-- Ejemplo: Permite que un usuario regular tenga acceso de edición extra en ciertos módulos
--
-- Este script NO crea nada aquí por defecto, pero las queries de login
-- deben hacer UNION de permisos de rol + permisos de usuario
--
-- INSERT INTO autenticacion.tbl_autenticacion_usuario_modulo
--     (IdUsuario, IdModulo, PuedeVer, PuedeCrear, PuedeEditar, PuedeEliminar, ...)
-- VALUES (...)

RAISE NOTICE '✅ Permisos usuario-módulo: RESERVADO para overrides (casos especiales)';

-- ============================================================================
-- SECCIÓN 4: VERIFICACIÓN DE INTEGRIDAD
-- ============================================================================

DO $$
DECLARE
    v_rol_modulo_count INTEGER;
    v_admin_permisos INTEGER;
    v_user_permisos INTEGER;
    v_usuario_app_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_rol_modulo_count
    FROM autenticacion.tbl_autenticacion_rol_modulo
    WHERE Activo = TRUE;

    SELECT COUNT(*) INTO v_admin_permisos
    FROM autenticacion.tbl_autenticacion_rol_modulo rm
    JOIN administracion.tbl_administracion_catalogo_detalle cd ON rm.IdRol = cd.Id
    WHERE cd.CodigoValor = 'ADMIN' AND rm.Activo = TRUE;

    SELECT COUNT(*) INTO v_user_permisos
    FROM autenticacion.tbl_autenticacion_rol_modulo rm
    JOIN administracion.tbl_administracion_catalogo_detalle cd ON rm.IdRol = cd.Id
    WHERE cd.CodigoValor = 'USER' AND rm.Activo = TRUE;

    SELECT COUNT(*) INTO v_usuario_app_count
    FROM autenticacion.tbl_autenticacion_usuario_aplicacion
    WHERE Activo = TRUE;

    RAISE NOTICE '
    ╔════════════════════════════════════════════════════════════╗
    ║     VERIFICACIÓN: Permisos y Aplicaciones de TMR           ║
    ╠════════════════════════════════════════════════════════════╣
    ║ ✅ Permisos rol-módulo total          : %             ║
    ║ ✅ Permisos ADMIN (9 módulos)         : % esperados   ║
    ║ ✅ Permisos USER (8 módulos)          : % esperados   ║
    ║ ✅ Usuario-Aplicación (TMR)           : %             ║
    ╠════════════════════════════════════════════════════════════╣
    ║ NOTAS:                                                     ║
    ║ • ADMIN: 9 módulos × (Ver, Crear, Editar, Eliminar)      ║
    ║ • USER: 8 módulos × permisos variados (sin Config)        ║
    ║ • Usuarios: 2 vinculados a aplicación Time Report         ║
    ╚════════════════════════════════════════════════════════════╝
    ', v_rol_modulo_count, v_admin_permisos, v_user_permisos, v_usuario_app_count;
END $$;

COMMIT;

-- =============================================================================
-- SECCIÓN 5: CONSULTAS DE VALIDACIÓN POST-COMMIT
-- =============================================================================
/*

1. Verificar permisos del rol ADMIN:
   SELECT 
       m.NombreModulo,
       rm.PuedeVer, rm.PuedeCrear, rm.PuedeEditar, rm.PuedeEliminar
   FROM autenticacion.tbl_autenticacion_rol_modulo rm
   JOIN administracion.tbl_administracion_catalogo_detalle cd ON rm.IdRol = cd.Id
   JOIN autenticacion.tbl_autenticacion_modulo m ON rm.IdModulo = m.Id
   WHERE cd.CodigoValor = 'ADMIN' AND rm.Activo = TRUE
   ORDER BY m.OrdenVisualizacion;
   
   Resultado esperado: 9 filas (todos los módulos) con CRUD = true


2. Verificar permisos del rol USER:
   SELECT 
       m.NombreModulo,
       rm.PuedeVer, rm.PuedeCrear, rm.PuedeEditar, rm.PuedeEliminar
   FROM autenticacion.tbl_autenticacion_rol_modulo rm
   JOIN administracion.tbl_administracion_catalogo_detalle cd ON rm.IdRol = cd.Id
   JOIN autenticacion.tbl_autenticacion_modulo m ON rm.IdModulo = m.Id
   WHERE cd.CodigoValor = 'USER' AND rm.Activo = TRUE
   ORDER BY m.OrdenVisualizacion;
   
   Resultado esperado: 8 filas (sin CONFIGURACION), con permisos variados


3. Verificar usuario-aplicación:
   SELECT 
       u.Email,
       a.NombreAplicacion
   FROM autenticacion.tbl_autenticacion_usuario_aplicacion ua
   JOIN autenticacion.tbl_autenticacion_usuario u ON ua.IdUsuario = u.Id
   JOIN autenticacion.tbl_autenticacion_aplicacion a ON ua.IdAplicacion = a.Id
   WHERE ua.Activo = TRUE;
   
   Resultado esperado: 2 filas (2 usuarios × 1 aplicación TMR)


4. Query completa de permisos para login (simulando consulta desde backend):
   SELECT 
       u.Id,
       u.Email,
       u.HashPassword,
       u.EstaActivo,
       e.CodigoEmpleado,
       p.Nombres,
       p.Apellidos,
       cd.CodigoValor as RolCodigo,
       cd.Valor as RolNombre,
       m.NombreModulo,
       m.RutaModulo,
       rm.PuedeVer,
       rm.PuedeCrear,
       rm.PuedeEditar,
       rm.PuedeEliminar
   FROM autenticacion.tbl_autenticacion_usuario u
   LEFT JOIN administracion.tbl_administracion_persona p ON u.IdPersona = p.Id
   LEFT JOIN administracion.tbl_administracion_empleado e ON p.Id = e.IdPersona
   LEFT JOIN autenticacion.tbl_autenticacion_usuario_rol ur ON u.Id = ur.IdUsuario
   LEFT JOIN administracion.tbl_administracion_catalogo_detalle cd ON ur.IdRol = cd.Id
   LEFT JOIN autenticacion.tbl_autenticacion_rol_modulo rm ON cd.Id = rm.IdRol
   LEFT JOIN autenticacion.tbl_autenticacion_modulo m ON rm.IdModulo = m.Id
   WHERE u.Email = 'admin@isc.local' AND u.Activo = TRUE
   ORDER BY m.OrdenVisualizacion;
   
   Resultado esperado: 9 filas (admin tiene 9 módulos accesibles)

*/

-- FIN DEL SCRIPT
-- Total de registros creados: ~18 (18 permisos rol-módulo + 2 usuario-aplicación)
-- Estructura de permisos completa para autenticación y autorización
