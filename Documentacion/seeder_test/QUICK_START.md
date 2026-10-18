# Uso vigente de los seeders

La instalación inicial se describe en
[00-ORDEN_DESPLIEGUE.md](../Scripts_DB/00-ORDEN_DESPLIEGUE.md).
No hay seeding automático al iniciar en Development ni en Production.

- `--prepare-database`: SQL versionado para una base nueva.
- `--initialize-data`: permisos de Proveedores y administrador inicial controlado.
- `--verify-database`: comprobación de esquema y datos indispensables, sin cambios.

El generador `SeedTemplateSeeder` es una herramienta histórica de demostración,
no idempotente y con autenticación de prueba incompatible con el login BCrypt
actual. Consultar [README.md](README.md) antes de usarlo en una base desechable.
