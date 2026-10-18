# Seeder de datos de demostración (histórico)

El contenido anterior de las guías se conserva en
[HISTORICO_ANTES_INSTALADOR.md](HISTORICO_ANTES_INSTALADOR.md), sin vigencia operativa.

`SeedTemplateSeeder` y `SeedRunner` se conservan para pruebas manuales en bases
desechables. No se ejecutan automáticamente con `dotnet run`, tampoco en
Development, y no son parte de una instalación nueva.

El generador crea clientes, líderes, empleados, proyectos y años de actividades.
No es idempotente: varias de sus inserciones se repiten y usan IDs de catálogo
fijos. Sus usuarios usan SHA256, mientras el login actual utiliza BCrypt; no
sirven como cuentas iniciales funcionales. No ejecutarlo sobre bases reales.

La preparación reproducible de estructura, catálogos, roles, permisos y primer
administrador está en la [guía de instalación vigente](../Scripts_DB/00-ORDEN_DESPLIEGUE.md).
Los comandos actuales son `--prepare-database`, `--initialize-data` y
`--verify-database`. El arranque web no genera datos.

Para estudiar el generador heredado, consultar
`Infrastructure/Database/Seeders/SeedTemplateSeeder.cs` y
`Infrastructure/Database/Scripts/SeedRunner.cs`. Su invocación desde código debe
reservarse a una copia local desechable y no agregarse a `Program.cs`.
