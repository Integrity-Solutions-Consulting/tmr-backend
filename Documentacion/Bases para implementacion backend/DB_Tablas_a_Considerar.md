
## 1. Objetivo

Definir cómo construir el módulo de autenticación para `time_report` usando la base de datos unificada `Inv_tmr_db` (PostgreSQL 16+) y una arquitectura de vertical slice. La idea es separar cada caso de uso en su propia carpeta funcional, evitando un backend monolítico por capas horizontales.

## 2. Tablas que se deben considerar

### 2.1 Tablas base de identidad y RRHH (esquema `administracion`)

Estas tablas son las que necesitas para resolver quién es el usuario real dentro del negocio:

| Tabla | Uso en autenticación |
|---|---|
| `administracion.tbl_administracion_persona` | Datos personales base del usuario (cédula, nombres, correo, teléfono) |
| `administracion.tbl_administracion_empleado` | Relación laboral y estado del empleado (código, cargo, fecha ingreso) |
| `administracion.tbl_administracion_cargo` | Cargo del empleado para contexto |
| `administracion.tbl_administracion_catalogo_detalle` | Catálogos maestros: roles (`AUT-ROL`), géneros, nacionalidades, tipos de contrato |
| `administracion.tbl_administracion_cliente` | Solo si deseas permitir login de clientes externos (no requerido para `time_report`) |

### 2.2 Tablas nucleares de autenticación (esquema `autenticacion`)

Estas son las tablas centrales del módulo de autenticación:

| Tabla | Uso | Estado |
|---|---|---|
| `autenticacion.tbl_autenticacion_usuario` | Credenciales principales del usuario (Email, HashPassword) | ✅ Activa |
| `autenticacion.tbl_autenticacion_usuario_rol` | Asignación de roles por usuario | ⚠️ Incompleta (solo Id) |
| `autenticacion.tbl_autenticacion_rol_modulo` | Permisos de rol sobre módulos (CRUD) | ✅ Activa |
| `autenticacion.tbl_autenticacion_usuario_modulo` | Permisos directos por usuario sobre módulos | ✅ Activa |
| `autenticacion.tbl_autenticacion_modulo` | Catálogo de módulos funcionales de `time_report` | ✅ Activa |
| `autenticacion.tbl_autenticacion_menu` | Menú dinámico por aplicación | ✅ Activa |
| `autenticacion.tbl_autenticacion_menu_rol` | Visibilidad de menú por rol | ✅ Activa |
| `autenticacion.tbl_autenticacion_menu_usuario` | Visibilidad de menú por usuario | ✅ Activa |
| `autenticacion.tbl_autenticacion_aplicacion` | Aplicaciones del ecosistema (TMR, INV) | ✅ Activa |
| `autenticacion.tbl_autenticacion_sesion_app` | Sesiones o tokens por aplicación | ✅ Activa |

**Nota:** Las tablas `tbl_autenticacion_privilegio_rol`, `tbl_autenticacion_privilegio_usuario`, `tbl_autenticacion_usuario_aplicacion` están incompletas en la BD actual. Los roles se administran a través de `administracion.tbl_administracion_catalogo_detalle` con `TipoCatalogo='AUT'` y `Codigo='ROL'`.

### 2.3 Tablas de sesión y seguridad (esquema `autenticacion`)

| Tabla | Uso |
|---|---|
| `autenticacion.tbl_autenticacion_sesion` | Registro de sesiones activas e históricas (JWT tracking) |
| `autenticacion.tbl_autenticacion_token_blacklist` | Blacklist de tokens invalidados (logout) |
| `autenticacion.tbl_autenticacion_password_historial` | Historial de hashes de password (evitar reutilización) |
| `autenticacion.tbl_autenticacion_pregunta_usuario` | Recuperación o verificación adicional (opcional) |

**Nota:** No existe `tbl_autenticacion_configuracion` en la BD actual. Los parámetros deben manejarse en código o crearse como extensión futura.

### 2.4 Tablas auxiliares (esquema `administracion`)

| Tabla | Uso |
|---|---|
| `administracion.tbl_administracion_cliente_usuario` | Solo si el login también cubre usuarios cliente (no aplica para `time_report`) |