/*
================================================================================
SCRIPT: Crear Permisos Faltantes para Módulos sin Permisos Activos
================================================================================
Propósito: Insertar permisos VIEW mínimos para los 4 módulos que aparecen 
grises en Configuración → Roles: Requerimientos, Configuración, Reportes, Time Report
 
Módulos a procesar:
  - ID 3: Requerimientos
  - ID 7: Configuración
  - ID 8: Reportes
  - ID 9: Time Report
 
NOTA: Este script es IDEMPOTENTE (seguro repetir)
================================================================================
*/
 
-- ============================================================================
-- PASO 1: DIAGNÓSTICO (OPCIONAL - solo para verificar antes de ejecutar)
-- ============================================================================
-- Descomenta para ver el estado actual de permisos
/*
SELECT 
    m.id,
    m.nombremodulo,
    COUNT(p.id) AS cantidad_permisos,
    STRING_AGG(p.codigo, ', ' ORDER BY p.codigo) AS codigos
FROM autenticacion.tbl_autenticacion_modulo m
LEFT JOIN autenticacion.tbl_autenticacion_permiso p ON m.id = p.idmodulo AND p.activo = true
WHERE m.id IN (3, 7, 8, 9)
GROUP BY m.id, m.nombremodulo
ORDER BY m.id;
*/
 
-- ============================================================================
-- PASO 2: INSERTAR PERMISOS FALTANTES
-- ============================================================================
INSERT INTO autenticacion.tbl_autenticacion_permiso
    (idmodulo, codigo, accion, descripcion, activo, usuariocreacion, ipcreacion)
VALUES
    (3, 'REQUERIMIENTOS_VIEW', 'VIEW', 'Permiso VIEW para Requerimientos', true, 'setup', '127.0.0.1'),
    (7, 'CONFIGURACION_VIEW', 'VIEW', 'Permiso VIEW para Configuración', true, 'setup', '127.0.0.1'),
    (8, 'REPORTES_VIEW', 'VIEW', 'Permiso VIEW para Reportes', true, 'setup', '127.0.0.1'),
    (9, 'TIME_REPORT_VIEW', 'VIEW', 'Permiso VIEW para Time Report', true, 'setup', '127.0.0.1')
ON CONFLICT (codigo) DO UPDATE SET activo = true;
 
-- ============================================================================
-- PASO 3: VERIFICACIÓN POST-EJECUCIÓN
-- ============================================================================
-- Descomenta para confirmar que todo se creó correctamente
/*
SELECT 
    m.id,
    m.nombremodulo,
    COUNT(p.id) AS cantidad_permisos,
    STRING_AGG(p.codigo, ', ' ORDER BY p.codigo) AS codigos
FROM autenticacion.tbl_autenticacion_modulo m
LEFT JOIN autenticacion.tbl_autenticacion_permiso p ON m.id = p.idmodulo AND p.activo = true
WHERE m.id IN (3, 7, 8, 9)
GROUP BY m.id, m.nombremodulo
ORDER BY m.id;
*/
 
/*
Resultado esperado:
┌────┬─────────────────┬──────────────────┬─────────────────────┐
│ id │ nombremodulo    │ cantidad_permisos │ codigos             │
├────┼─────────────────┼──────────────────┼─────────────────────┤
│ 3  │ Requerimientos  │ 1                 │ REQUERIMIENTOS_VIEW │
│ 7  │ Configuracion   │ 1                 │ CONFIGURACION_VIEW  │
│ 8  │ Reportes        │ 1                 │ REPORTES_VIEW       │
│ 9  │ Time Report     │ 1                 │ TIME_REPORT_VIEW    │
└────┴─────────────────┴──────────────────┴─────────────────────┘
*/