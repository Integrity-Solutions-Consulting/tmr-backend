# Instalación y actualización de TMR

Guía vigente: ejecutar desde la raíz de `tmr-backend`. Preparación de esquema,
inicialización de datos y arranque son etapas independientes. Ninguna se ejecuta
automáticamente al iniciar el servidor web. La instalación nueva requiere una
base vacía y no debe ejecutarse sobre Desarrollo ni Producción.

## Requisitos

- PostgreSQL 16, UTF-8 y almacenamiento persistente. El operador crea la base y
  el usuario del nuevo servidor. La cuenta instaladora necesita permisos para
  crear esquemas, tipos, tablas, funciones, índices, secuencias y triggers.
- Docker Engine y Compose v2 o posterior. La imagen usa .NET 10 Alpine.
  Incluye ICU para `es-ES` y bibliotecas Kerberos para Npgsql. Globalización
  invariante debe estar desactivada; el verificador comprueba la cultura.
  Alternativa nativa: SDK/runtime .NET 10, `dotnet restore` y `dotnet publish`.
- Backend HTTP interno en 8080, proxy inverso con HTTPS para acceso público y
  CORS con el origen exacto del frontend. No publicar PostgreSQL en Internet.
- Frontend independiente Angular 21 (`tmr-frontend`), servido en puerto 3000 por
  su contenedor Nginx. Su Dockerfile exige `BUILD_CONFIG=production` y `API_URL`
  pública terminada en `/api` durante la compilación. Variables al arrancar no
  cambian el JS. El lockfile exige Node `^20.19.0 || ^22.12.0 || >=24.0.0`;
  su Dockerfile usa Node 20 Alpine. Frontend y móvil no están en el Compose backend.

## 1. Configurar el nuevo entorno

Copiar `.env.example` a `.env` y completar fuera de Git:

- `DB_CONNECTION_STRING`: conexión Npgsql a la base nueva con Host, Port,
  Database, Username, Password y SSL según el servidor.
- `JWT_SECRET_KEY`: secreto nuevo de al menos 32 caracteres.

Compose los traduce a `ConnectionStrings__DefaultConnection` y `Jwt__SecretKey`
dentro del contenedor. Una imagen ejecutada sin Compose necesita directamente
estos nombres con doble guion bajo. `TMR_DB_CONNECTION` no es leído por el backend.
`dotnet` no carga `.env` automáticamente: usar variables exportadas o user-secrets.

Configurar en el entorno real del servicio `Cors__AllowedOrigins__0`,
`EmailSettings__FrontendUrl`, `EmailSettings__SmtpServer`, `EmailSettings__Port`,
`EmailSettings__SenderEmail`, `EmailSettings__Username` y
`EmailSettings__Password` según corresponda. Compose solo pasa las variables
declaradas en su sección `environment`: agregar allí las necesarias para el
nuevo entorno. SMTP permite recuperar contraseñas; las notificaciones semanales
están desactivadas por defecto. No reutilizar credenciales de los entornos actuales.
No versionar secretos ni backups; mantenerlos fuera del contexto Docker.

## 2. Instalar el esquema con EF Core

Desde la raíz del backend, con `ConnectionStrings__DefaultConnection` apuntando a la base vacía:

```bash
docker compose build backend
docker compose run --rm backend --apply-migrations
```

EF aplica `20261110000000_BaselineProduccion` y las cinco migraciones de colaboradores posteriores. El script 24 de permisos y vacaciones incorporado desde develop queda pendiente de integrar en las migraciones EF antes de desplegar esta versión.
La línea base incluye tablas, tipos, secuencias, funciones, triggers, restricciones e índices del esquema de producción. Para instalaciones nuevas incorpora los catálogos, módulos, roles y permisos del paquete histórico (incluidos GERENTE y sus permisos) y no crea cuentas personales. Las tres tablas de respaldo operativo `administracion.bkp_*` existentes en el respaldo original quedan fuera de la instalación. Las tablas de proveedores de `inventory` sí se incluyen. La FK de proveedores de la línea base corresponde a producción; la alineación con la variante de desarrollo requiere una entrega separada y validación previa de datos.

`--prepare-database` sigue disponible para despliegues por etapas. En una base vacía instala solamente la línea base; en una que ya la tiene registrada termina sin retroceder de versión. `--apply-migrations` instala todas las pendientes. Ambos usan el historial configurado, `public."__EFMigrationsHistory"` por defecto. En ambientes que usan otro esquema configure `Database__MigrationsHistorySchema` tanto en la API como en el instalador.

Los SQL numerados permanecen como archivo histórico. El backend usa los recursos versionados en `Migrations/Baseline`. El contenido de una migración ya distribuida es inmutable. Para el siguiente cambio actualice el modelo, genere y revise una migración EF y pruebe tanto instalación nueva como actualización. Los ocho mapeos públicos legados sin tabla en el esquema oficial siguen disponibles al código y están excluidos del DDL de EF.

## 3. Inicializar datos indispensables

En Bash, introducir los datos del primer administrador sin poner la contraseña
en la línea de comandos:

```bash
read -r -p 'Email: ' Bootstrap__AdminEmail
read -r -p 'Identificación (máximo 20 caracteres): ' Bootstrap__AdminIdentification
read -r -p 'Nombre: ' Bootstrap__AdminName
read -r -s -p 'Contraseña inicial (mínimo 12 caracteres): ' Bootstrap__AdminPassword
export Bootstrap__AdminEmail Bootstrap__AdminIdentification Bootstrap__AdminName Bootstrap__AdminPassword
docker compose run --rm \
  -e Bootstrap__AdminEmail -e Bootstrap__AdminIdentification \
  -e Bootstrap__AdminName -e Bootstrap__AdminPassword \
  backend --initialize-data
unset Bootstrap__AdminEmail Bootstrap__AdminIdentification Bootstrap__AdminName Bootstrap__AdminPassword
docker compose run --rm backend --verify-database
```

Se guarda BCrypt con el mismo hasher de la aplicación y se asigna Administrador
con cambio obligatorio de contraseña. No se crean clientes, empleados ni
actividades ficticias. Repetir no cambia credenciales ni roles de cuentas
existentes; no se permite crear otro usuario inicial si ya existen usuarios.

`--initialize-data` asegura módulo y cuatro permisos `PROVEEDORES_*`, acceso
completo de Administrador y lectura para Gerente/Lider, en una transacción.
Si ya hay usuarios y no se pasan variables Bootstrap, solo asegura esos permisos.
Puede insertar/reactivar registros: autorizar por separado en bases existentes.
No se ejecuta durante el arranque normal.

`--verify-database` comprueba las tablas y columnas mapeadas de los esquemas
TMR, catálogos, tipos de actividad, administrador y permisos de proveedores.
Sale con código no cero si faltan; no modifica datos.

Sin Docker: `dotnet run --no-launch-profile -- --prepare-database` (o
`--initialize-data`, `--verify-database`) con variables
`ConnectionStrings__DefaultConnection` y `Bootstrap__*` exportadas. Las etapas
administrativas no requieren JWT ni arrancan servicios en segundo plano.

## 4. Arrancar y comprobar

```bash
docker compose up -d backend
curl --fail http://localhost:8080/health/live
curl --fail http://localhost:8080/health/ready
```

Para construir el frontend desde `tmr-frontend`, proporcionar la URL pública
del backend ya publicado (no `localhost` si lo consumirán otros equipos):

```bash
read -r -p 'URL pública del API terminada en /api: ' API_URL
docker build -f .docker/containerfile --build-arg BUILD_CONFIG=production \
  --build-arg API_URL="$API_URL" -t tmr-frontend:local .
docker run -d --name tmr-frontend -p 3000:3000 tmr-frontend:local
```

Configurar DNS/TLS y el proxy del nuevo servidor para ambos servicios. Este
ejemplo no cambia los workflows ni los dominios de instalaciones existentes.

Liveness comprueba el proceso; readiness devuelve 503 si PostgreSQL no responde.
Comprobar además login, cambio de clave, roles, catálogos y recuperación de
contraseña en el frontend publicado. La cadena ausente impide arrancar con un
error sin credenciales. Una cadena presente no abre conexión durante el arranque.
La limpieza de sesiones consulta la base tras 30 segundos y las notificaciones,
si están habilitadas, según calendario tras una espera inicial de un minuto.
Son procesos operativos y no se desactivaron para ocultar fallos de CI.

## Bases existentes y traslados

Antes del primer despliegue de la nueva cadena, restaure un respaldo con datos en un ambiente aislado y acuerde una ventana sin cambios de DDL. Sobre esa copia ejecute:

```bash
docker compose run --rm backend --adopt-baseline
docker compose run --rm backend --apply-migrations
docker compose run --rm backend --verify-database
```

La adopción contrasta el historial y las firmas de los catálogos PostgreSQL con las estructuras verificadas de producción, desarrollo o instalación histórica 01–23. Comprueba tablas, columnas, restricciones, índices, funciones, triggers, tipos enum y secuencias de los esquemas TMR. Los propietarios, ACL, datos, valores actuales de secuencias y copias operativas quedan fuera de la firma. Si encuentra diferencias o un historial parcial, termina antes de registrar la línea base. Revise el grupo indicado en la copia. Mantenga los permisos de usuarios y los catálogos del ambiente: la adopción solo registra la nueva fila de historial, conserva los IDs anteriores y omite las semillas de instalación. La aplicación de migraciones es un paso posterior.

`--initialize-data` puede crear o reactivar permisos y asignaciones. En bases existentes ejecútelo solo si esos cambios se aprobaron. Los respaldos de solo esquema sirven para validar estructura; la prueba de conservación de datos requiere un respaldo con datos.

Para trasladar datos usar backup/restauración PostgreSQL (`pg_dump`/`pg_restore`)
compatible con el destino, conservando esquemas, extensiones y secuencias.
Validar una copia con `--verify-database` y flujos funcionales antes de cambiar
tráfico. Instalar una base vacía no sustituye un traslado ni demuestra que las
bases existentes tengan exactamente el mismo esquema.

## GitHub Actions

`Validate Build` (`a2-validate_build_to_prod.yml`) construye sin publicar, crea
PostgreSQL 16 efímero, prepara e inicializa dos veces, compara la adopción de una copia con datos contra una instalación nueva, verifica esquema y arranca
el contenedor para comprobar liveness y readiness. Usa una conexión propia del
job inyectada como `ConnectionStrings__DefaultConnection`.
No utiliza `CI_DB_CONNECTION_STRING` ni conexiones Dokploy. Conserva la validación
de `JWT_SECRET_KEY`, que debe estar disponible para el PR. Los workflows Dev y
Producción no cambian. Este job valida una instalación nueva, sin migrar entornos
desplegados ni sustituir las pruebas funcionales de toda la UI.
