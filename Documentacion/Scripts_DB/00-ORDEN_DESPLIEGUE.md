# Despliegue de base de datos desde cero

Ejecutar los scripts contra la base del entorno correspondiente. El nombre de
la base y la cadena de conexión cambian por ambiente; los esquemas y tablas no.
No ejecutar scripts de otro entorno ni cambiar credenciales desde la aplicación.

Orden:

1. `01-Inv_tmr_db_deploy_29052026.sql` — estructura base.
2. `02-Inv_tmr_catalogos actualizado.v3.sql` — catálogos, módulos, roles y permisos.
3. `03-Inv_tmr_db_ajustes_tblSesion_tblProyecto.sql` hasta
   `12-Inv_tmr_db_alter_asignacion_estado.sql`, en orden numérico.
4. `13-Inv_tmr_db_migracion_proveedores.sql` — tabla oficial de tipos de
   proveedor y cambio de su FK.

Después de los scripts, iniciar el backend. `ProveedorPermissionsSeeder` es
idempotente y solo asegura el módulo y permisos de Proveedores; no reemplaza
las migraciones SQL ni crea el esquema inicial.

Para una base existente, ejecutar primero la migración 13 en una copia o en el
entorno de desarrollo y validar `GET /api/proveedores` con un usuario autorizado.
La migración se detiene y revierte si encuentra un proveedor cuyo tipo no pueda
conservarse; no elimina proveedores ni modifica sus datos.
