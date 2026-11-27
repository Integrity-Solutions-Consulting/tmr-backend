# Uso vigente de los seeders

La proteccion de fixtures y el procedimiento para ejecutar el comando global
en una imagen Docker de Dokploy estan en [DOKPLOY_SEGURIDAD.md](DOKPLOY_SEGURIDAD.md).

La instalación inicial se describe en
[00-ORDEN_DESPLIEGUE.md](../Scripts_DB/00-ORDEN_DESPLIEGUE.md).
No hay seeding automático al iniciar en Development ni en Production.

- `--prepare-database`: SQL versionado para una base nueva.
- `--initialize-data`: permisos de Proveedores y administrador inicial controlado.
- `--initialize-seguimiento-global`: permiso y rol global de Seguimiento, sin fixtures ni administrador bootstrap.
- `--verify-database`: comprobación de esquema y datos indispensables, sin cambios.

El generador `SeedTemplateSeeder` es una herramienta histórica de demostración,
no idempotente y con autenticación de prueba incompatible con el login BCrypt
actual. Consultar [README.md](README.md) antes de usarlo en una base desechable.
