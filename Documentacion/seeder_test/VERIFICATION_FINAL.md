# Alcance de verificación de seeders

La verificación histórica de junio de 2026 no acredita una instalación del
backend actual. En particular, `dotnet run` no ejecuta automáticamente el
seeder de demostración y sus cuentas SHA256 no validan el login BCrypt.

La comprobación reproducible vigente está en el workflow `Validate Build`:
crea PostgreSQL 16 vacío, aplica el paquete SQL y la inicialización dos veces,
verifica esquema/permisos/administrador y comprueba liveness y readiness del
contenedor Linux. Falla ante errores de esquema, configuración o conexión.

Consultar la [guía vigente](../Scripts_DB/00-ORDEN_DESPLIEGUE.md) para ejecutar
las etapas en otra instalación. Esta comprobación no sustituye pruebas
funcionales del frontend, correo, reportes PDF o migración de datos históricos.

## Prueba local aislada del 17 de octubre de 2026

- Compilación .NET 10 e imagen Linux Alpine: correctas.
- Cuatro workflows: actionlint sin errores (sin validadores opcionales shellcheck/pyflakes).
- Compose: configuración sin interpolación válida.
- PostgreSQL 16 vacío: preparación e inicialización repetidas sin duplicados.
- Resultado: 13 scripts, un administrador BCrypt con cambio obligatorio de
  contraseña y cuatro permisos de proveedores. Administrador tiene cuatro
  asignaciones; Gerente y Lider, una de lectura cada uno.
- Verificación de tablas/columnas mapeadas y cultura `es-ES`: correcta.
- Backend conectado: liveness 200 y readiness 200; limpieza de sesiones ejecutada.
- Backend con PostgreSQL inaccesible: liveness 200 y readiness 503.
- Cadena ausente: arranque rechazado con mensaje explícito sin credenciales.

No se ejecutó Actions remotamente ni se probó un despliegue completo con DNS/TLS,
SMTP, frontend, móvil o generación PDF. Las migraciones EF históricas se
auditaron, no se usaron como ruta de instalación. Persisten advertencias previas
de compilación y de paquetes NuGet, incluidos OpenApi y Pkcs; actualizar esas
dependencias requiere una revisión separada.

## Comprobación final del filtrado de argumentos

- `Program.cs` filtra únicamente los nombres de comandos administrativos con
  `Where`, preservando el orden y los argumentos repetidos.
- Compilación e imagen Linux reconstruidas correctamente.
- Preparación e inicialización ejecutadas dos veces sobre PostgreSQL 16 vacío;
  verificación administrativa correcta. Permanecen 13 scripts, un usuario y
  cuatro permisos de proveedores.
- Los tres comandos se probaron con `--DetailedErrors true
  --Logging:IncludeScopes true`; ambos valores `true` se conservan.
- Arranque sin argumentos y arranque con esos argumentos: liveness y readiness
  HTTP 200 en ambos casos.
- Las cuatro guías originales se conservan en
  [HISTORICO_ANTES_INSTALADOR.md](HISTORICO_ANTES_INSTALADOR.md).
