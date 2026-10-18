# Instalación y actualización de TMR

Guía vigente: ejecutar desde la raíz de `tmr-backend`. Preparación de esquema,
inicialización de datos y arranque son etapas independientes. Ninguna se ejecuta
automáticamente al iniciar el servidor web. La instalación nueva requiere una
base vacía y no debe ejecutarse sobre Desarrollo ni Producción.

## Requisitos

- PostgreSQL 16, UTF-8 y almacenamiento persistente. El operador crea la base y
  el usuario del nuevo servidor. La cuenta instaladora necesita permisos para
  crear esquemas, funciones, triggers y extensiones `pgcrypto` y `uuid-ossp`.
  Si el proveedor restringe extensiones, pedir que las habilite previamente.
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

## 2. Construir y preparar estructura y catálogos históricos

```bash
docker compose build backend
docker compose run --rm backend --prepare-database
```

Aplica los SQL embebidos **01, 02, 03, 04, 05, 07, 08, 09, 10, 11, 12, 13, 14**,
en ese orden. Incluyen esquema, funciones, triggers y catálogos/RBAC históricos.
Mezclan DDL y datos; algunos no son idempotentes ejecutados directamente en psql.
El comando registra versión y SHA-256 en `public.tmr_installation_history`
en la misma transacción de cada script. Al repetir omite versiones aplicadas;
un script aplicado que cambió provoca error. Un fallo revierte ese script y
permite reintentar tras corregir la causa. El instalador serializa las ejecuciones.

**No ejecutar el script 06**: crea un usuario particular y no es requisito del
sistema. Está excluido del paquete embebido. El script 14 corrige el nombre
`liderhora` a `liderhoras` y las asociaciones de módulos del script 05,
resolviéndolas por nombre en lugar de depender de IDs numéricos históricos.

No combinar esta ruta con `EnsureCreated` ni `dotnet ef database update`.
La única migración EF (`20260527194023_InitialCreate`) es histórica: no contiene
password reset, historial de contratos, estado de asignación ni tabla oficial
de tipos de proveedor, ni instala funciones/triggers/catálogos SQL. **EF por sí
solo no prepara el sistema actual**. Se conserva su historial; la ruta soportada
para una instalación nueva es el paquete SQL.

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

`--verify-database` comprueba las tablas y columnas mapeadas de los cinco esquemas
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

El preparador rechaza bases TMR históricas sin su historial de instalación;
no las adopta ni marca versiones automáticamente. Auditar sus scripts aplicados
y probar los pendientes en una copia restaurada, con backup y autorización.
No repetir el paquete inicial ni insertar historial para forzarlo. Para cambios
futuros, agregar versiones SQL y actualizar el manifiesto del instalador;
no editar versiones ya distribuidas.

Para trasladar datos usar backup/restauración PostgreSQL (`pg_dump`/`pg_restore`)
compatible con el destino, conservando esquemas, extensiones y secuencias.
Validar una copia con `--verify-database` y flujos funcionales antes de cambiar
tráfico. Instalar una base vacía no sustituye un traslado ni demuestra que las
bases existentes tengan exactamente el mismo esquema.

## GitHub Actions

`Validate Build` (`a2-validate_build_to_prod.yml`) construye sin publicar, crea
PostgreSQL 16 efímero, prepara e inicializa dos veces, verifica esquema y arranca
el contenedor para comprobar liveness y readiness. Usa una conexión propia del
job inyectada como `ConnectionStrings__DefaultConnection`.
No utiliza `CI_DB_CONNECTION_STRING` ni conexiones Dokploy. Conserva la validación
de `JWT_SECRET_KEY`, que debe estar disponible para el PR. Los workflows Dev y
Producción no cambian. Este job valida una instalación nueva, sin migrar entornos
desplegados ni sustituir las pruebas funcionales de toda la UI.
