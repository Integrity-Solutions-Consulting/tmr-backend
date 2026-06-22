# Flujo de Eliminación Física de Líderes

## Overview
El endpoint `DELETE /api/lideres/{id}/fisico` permite realizar una eliminación física (hard delete) de un líder y sus registros asociados, respetando las relaciones de integridad referencial.

## Flujo de Eliminación

### PASO 1: Validación y Obtención de Datos
1. Obtener el lider con:
   - Relación con `IdpersonaNavigation` (la persona vinculada)
   - Relación con `IdtipoNavigation` (el tipo de lider: interno/externo)
   - Relación con `TblTimeReportAsignacionProyectos` (asignaciones de proyecto)

2. Si el lider no existe → Retornar `false` (404 Not Found)

### PASO 2: Eliminar Asignaciones de Proyecto
**Tabla:** `TblTimeReportAsignacionProyecto`
- Eliminar TODOS los registros donde `Idlider = {id}`
- Esta tabla tiene una FK a `TblAdministracionLider`, por lo que debe limpiarse primero
- Sin este paso, la eliminación del lider fallará por violación de FK

**Razón del Orden:**
- `TblTimeReportAsignacionProyecto` → referencia a `TblAdministracionLider`
- Si intentamos eliminar el lider primero, la FK no permitirá la operación

### PASO 3: Eliminar el Registro del Lider
**Tabla:** `TblAdministracionLider`
- Eliminar el registro del lider
- Ahora ya no hay referencias externas a este registro

### PASO 4: Manejo de la Persona según Tipo
El comportamiento depende del tipo de lider:

#### Si es Lider EXTERNO (`Codigovalor = "EXT"`):
1. Verificar si la persona está vinculada a:
   - `TblAdministracionEmpleados` (como empleado)
   - `TblAutenticacionUsuarios` (como usuario)

2. **Si la persona NO está vinculada a nada:**
   - Eliminar el registro de `TblAdministracionPersona`
   - Eliminación completa del lider y su persona

3. **Si la persona SÍ está vinculada:**
   - NO eliminar la persona (evita violación de FK)
   - Marcar la persona como `Activo = false` (soft delete)
   - Esto preserva la integridad referencial

**Razón:**
- Un lider externo tiene una persona creada específicamente para él
- Solo se crea una persona para el lider externo en `CrearAsync`
- Sin embargo, esa persona podría estar vinculada a otros registros

#### Si es Lider INTERNO (`Codigovalor = "INT"`):
- **NO hacer nada con la persona**
- La persona NO se elimina ni se desactiva
- La persona sigue siendo utilizable para:
  - Colaborador (`TblAdministracionEmpleados`)
  - Usuario (`TblAutenticacionUsuarios`)
  - Otro lider interno

**Razón:**
- Los líderes internos son personas existentes del sistema
- La persona estará vinculada a un colaborador y posiblemente a un usuario
- Eliminar la persona causaría violación de FK

## Diagrama de Dependencias

```
TblAdministracionPersona (Persona)
├── TblAdministracionEmpleados (Colaborador) [FK: Idpersona]
├── TblAdministracionLider (Lider) [FK: Idpersona]
│   └── TblTimeReportAsignacionProyecto [FK: Idlider] ← ELIMINAR PRIMERO
└── TblAutenticacionUsuario (Usuario) [FK: Idpersona]
```

## Orden Correcto de Eliminación

1. **PRIMERO:** `TblTimeReportAsignacionProyecto` (Idlider)
2. **SEGUNDO:** `TblAdministracionLider` (Id)
3. **TERCERO:** 
   - Si Externo: `TblAdministracionPersona` (Id) o marcar como inactivo
   - Si Interno: No hacer nada

## Transacción

Toda la operación se ejecuta dentro de una transacción:
- Si todo va bien → Commit
- Si ocurre un error → Rollback (se revierten todos los cambios)

## Casos de Uso

### Caso 1: Eliminar Lider Externo sin Vínculos
```
Entrada: Lider ID 5 (Externo, Juan Pérez)
↓
Eliminar asignaciones de proyecto de Juan
↓
Eliminar lider de Juan
↓
Eliminar persona Juan Pérez
↓
Salida: 204 No Content (éxito total)
```

### Caso 2: Eliminar Lider Externo con Vínculos
```
Entrada: Lider ID 7 (Externo, María García - pero es también Usuario)
↓
Eliminar asignaciones de proyecto de María
↓
Eliminar lider de María
↓
Persona está en TblAutenticacionUsuarios → Solo marcar inactiva
↓
Salida: 204 No Content (lider eliminado, persona inactiva)
```

### Caso 3: Eliminar Lider Interno
```
Entrada: Lider ID 3 (Interno, Carlos López - es Empleado y Usuario)
↓
Eliminar asignaciones de proyecto de Carlos
↓
Eliminar lider de Carlos
↓
Persona NO se toca (sigue activa)
↓
Salida: 204 No Content (solo lider eliminado)
```

### Caso 4: Lider No Existe
```
Entrada: Lider ID 999 (no existe)
↓
FirstOrDefaultAsync retorna null
↓
Salida: 404 Not Found
```

## Diferencia entre Endpoints

### `DELETE /api/lideres/{id}` (Soft Delete - Desactivar)
- **Cambio:** `Lider.Activo = false`
- **Datos:** Permanecen en la BD
- **Reversible:** Sí, cambiando `Activo = true`
- **Uso:** Auditoría, historial

### `DELETE /api/lideres/{id}/fisico` (Hard Delete - Eliminación Física)
- **Cambio:** Registros se eliminan de la BD
- **Datos:** Se pierden permanentemente
- **Reversible:** No (solo con backup)
- **Uso:** Limpieza de datos, correciones

## Manejo de Errores

| Error | Causa | Solución |
|-------|-------|----------|
| `404 Not Found` | Lider no existe | Verificar ID |
| `Violation of PRIMARY KEY` | Registros duplicados | Revisar data |
| `Violation of FOREIGN KEY` | Dependencias no eliminadas | Verificar PASO 2 |
| `Timeout` | Operación muy lenta | Indexar campos Idlider |

## Índices Recomendados

Para optimizar la eliminación:
- `idx_tr_asignacion_proyecto_lider` en `TblTimeReportAsignacionProyecto.Idlider` ✓ (ya existe)
- `idx_adm_empleado_persona` en `TblAdministracionEmpleados.Idpersona` (verificar)
- `idx_aut_usuario_persona` en `TblAutenticacionUsuarios.Idpersona` (verificar)
