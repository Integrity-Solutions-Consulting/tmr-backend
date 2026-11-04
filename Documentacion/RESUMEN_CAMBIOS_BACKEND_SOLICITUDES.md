# Resumen de Trabajo — Backend (`tmr-backend`)

**Fecha:** 04 de noviembre de 2026  
**Módulo principal:** Solicitudes de Permisos, Licencias y Vacaciones + Notificaciones SSE + Almacenamiento MinIO  
**Documento rector:** `docs/requerimientos-solicitudes-v2.docx`

---

## 1. Lo que quedó implementado y consolidado (Estado actual)

### 1.1. Base de Datos PostgreSQL normalizada en 3FN (Script 19)
Se reemplazó el esquema desnormalizado previo por un modelo relacional en **Tercera Forma Normal (3FN)** dentro de `Documentacion/Scripts_DB/19-Inv_tmr_db_modulo_solicitudes.sql`, compuesto por **6 tablas**:

1. **`public.tbl_time_report_estado_solicitud`**: Catálogo normalizado de estados del flujo (`PENDIENTE_JEFE`, `APROBADA_JEFE`, `APROBADA`, `OBSERVADA`, `RECHAZADA`) con código de semáforo (`AMARILLO`, `VERDE`, `ROJO`).
2. **`public.tbl_time_report_tipo_permiso`**: Catálogo oficial de los 7 tipos de solicitud del formulario de Talento Humano (`VACACIONES`, `LICENCIA_PATERNIDAD`, `LICENCIA_MATERNIDAD`, `PERMISO_PERSONAL`, `LICENCIA_CALAMIDAD`, `LICENCIA_ENFERMEDAD`, `PERMISO_ESTUDIOS`).
3. **`public.tbl_time_report_solicitud`**: Cabecera transaccional de solicitudes vinculada por llaves foráneas reales hacia `tbl_administracion_empleado`, `tbl_time_report_asignacion_proyecto`, `tbl_time_report_proyecto`, `tbl_administracion_cliente`, `tbl_administracion_lider`, `tbl_time_report_tipo_permiso` y `tbl_time_report_estado_solicitud`.
4. **`public.tbl_time_report_solicitud_revision`**: Historial normalizado de aprobaciones, observaciones y rechazos (Nivel 1: `JEFE_INTERNO` / `JEFE_EXTERNO`; Nivel 2: `TALENTO_HUMANO`).
5. **`public.tbl_time_report_solicitud_adjunto`**: Metadatos de archivos de respaldo subidos a MinIO (`bucket_name`, `object_key` con UUID, `content_type`, `tamano_bytes`, `sha256`, `subido_por_usuario_id`).
6. **`notificacion.notificacion_usuario`**: Bandeja persistida para la campana de notificaciones internas en tiempo real por usuario destino.

Adicionalmente:
- Se dejó `Documentacion/Scripts_DB/ROLLBACK_19-Inv_tmr_db_modulo_solicitudes.sql` para reversión limpia e idempotente.
- Se mantuvo intacta `Infrastructure/Database/Entities/TblTimeReportPermiso.cs` (tabla original `tbl_time_report_permisos` usada por Time Report clásico) sin mezclar columnas ajenas.
- En `PermisosEndpoints.cs` (`AsegurarEsquemaSolicitudesAsync`) el backend verifica e inicializa de forma idempotente las tablas y semillas del Script 19 si aún no existen en la base de datos.

### 1.2. Separación estricta de `EventoOutbox` y Notificaciones en Tiempo Real (SSE)
- **`EventoOutbox` intacto**: Se conservó `notificacion.evento_outbox` y su `EventDispatcher` exclusivamente para el módulo existente de cortes y recordatorios de horas (`NotificacionesModule.cs`), sin tocar su funcionamiento ni usarlo en Permisos.
- **SSE para Solicitudes (`RF-010`)**: Las notificaciones de campana del módulo de Solicitudes persisten en `notificacion.notificacion_usuario` (`NotificacionUsuario.cs`) y empujan eventos instantáneos por **Server-Sent Events** mediante `NotificacionInternaNotifier.cs` y los endpoints en `NotificacionesEndpoints.cs`:
  - `GET /api/notificaciones/internas`
  - `GET /api/notificaciones/internas/stream` (SSE en vivo)
  - `PATCH /api/notificaciones/internas/{id}/leer`
  - `PATCH /api/notificaciones/internas/leer-todas`

### 1.3. Endpoints y Reglas de Negocio 100% conectados a PostgreSQL (Cero datos quemados)
En `Features/Permisos/Endpoints/PermisosEndpoints.cs` y `Features/Permisos/DTOs/PermisosDtos.cs`:
- **`GET /api/permisos/contexto`**: Lee en tiempo real desde PostgreSQL los datos del colaborador autenticado (persona, cédula, cargo, departamento, régimen contractual Nómina vs Servicios Profesionales, proyectos asignados, proyectos activos del sistema con su líder principal, líderes disponibles y catálogo de tipos de permiso).
- **`POST /api/permisos` y `POST /api/permisos/con-evidencia`**:
  - Validación de régimen contractual (`ResolverRegimenContratoAsync`) y regla **`RF-004`** (certificado médico obligatorio cuando el colaborador es de **Nómina**, solicita **Licencia por enfermedad** y supera **1 día hábil**).
  - Generación de código secuencial `PER-{ANIO}-{CONSECUTIVO}`, token externo con vigencia de 72 horas para líderes de cliente externo, notificación por correo y aviso SSE a Jefe Inmediato y Talento Humano.
- **Flujo completo de revisión y reportes**:
  - Bandeja de Jefe Inmediato interno (`GET /api/permisos/bandeja-lider`, `POST /api/permisos/{id}/aprobar-lider`, `POST /api/permisos/{id}/rechazar-lider`).
  - Revisión pública por token para Líder Externo sin requerir sesión en TMR (`GET /api/permisos/externo/{token}`, `POST /api/permisos/externo/{token}/decidir`).
  - Bandeja de confirmación final de Talento Humano (`GET /api/permisos/bandeja-rrhh`, `POST /api/permisos/{id}/confirmar-rrhh`, `POST /api/permisos/{id}/observar-rrhh`, `POST /api/permisos/{id}/rechazar-rrhh`).
  - Vista consolidada de Gerencia (`GET /api/permisos/gerencia`) y generación de PDF oficial (`GET /api/permisos/{id}/pdf`).

### 1.4. Servicio de Almacenamiento MinIO con Validación Binaria y Doble Protocolo
En `Features/Permisos/Services/PermisosStorageService.cs`:
- Validación previa a transacción (`RNF-003`): límite estricto de **5 MB**, extensiones `.pdf`, `.png`, `.jpg`/`.jpeg`, verificación de **cabecera binaria real (magic bytes)** y cálculo de huella `SHA-256`.
- Estructura de llaves no predecibles: `solicitudes/{anio}/{idSolicitud}/{uuid}.{ext}` dentro del bucket `tmr-solicitudes`.
- **Soporte dual de conexión a MinIO**:
  1. **API S3 Nativa (AWS Signature V4 — Puerto `9000`)**: Para comunicación directa con el puerto S3 de MinIO.
  2. **Puente automático REST MinIO Console (`/api/v1` — Puerto `9001`)**: Cuando `Minio:Endpoint` apunta al dominio web de consola expuesto por Dokploy (`MINIO_BROWSER_REDIRECT_URL`), el servicio autentica en `/api/v1/login`, verifica o crea automáticamente el bucket `tmr-solicitudes` en `/api/v1/buckets`, sube el adjunto en `/api/v1/buckets/{bucket}/objects/upload` y permite descargarlo en `/api/v1/buckets/{bucket}/objects/download`.

---

## 2. Lo que se corrigió hoy en el Backend

| # | Problema / Error detectado | Causa raíz | Corrección aplicada |
|---|---|---|---|
| **1** | `System.ArgumentException: Format of the initialization string does not conform to specification starting at index 0` al arrancar `EventDispatcher`. | `NpgsqlConnectionStringBuilder` recibía una cadena en formato URI (`postgresql://usuario:clave@host:puerto/bd`) en lugar de pares `Host=...;Port=...`. | Se agregó en `Program.cs` un convertidor automático que detecta cadenas `postgres://` o `postgresql://` y las transforma con `NpgsqlConnectionStringBuilder` antes de registrar `ApplicationDbContext`. |
| **2** | `28P01: password authentication failed for user "mig-user"` y `Host desconocido (isc-tmr-postgre-migration-ysjwvc:5432)`. | En ejecución local fuera de la red interna Docker de Dokploy, el hostname interno de contenedor no resuelve por DNS y `appsettings.Development.json` pisaba la cadena de `appsettings.json`. | Se configuró en `appsettings.Development.json` la conexión pública TCP expuesta por Dokploy (`Host=147.93.181.146;Port=7878;Database=isc-tmr-migration;Username=mig-user;...`) dejando `appsettings.json` limpio para variables de entorno en despliegue. |
| **3** | Al abrir **Nueva Solicitud** (especialmente desde una cuenta `Admin` sin fila previa en `tbl_administracion_empleado`), no cargaban proyectos ni se autocompletaba el Jefe Inmediato, y existían etiquetas quemadas. | 1) En `ProyectosEndpoints`, el líder principal de un proyecto se guarda en una fila cabecera de `tbl_time_report_asignacion_proyecto` con `idempleado = NULL` e `idlider != NULL`, por lo que buscar líder solo en la fila del empleado devolvía `null`. 2) El usuario `Admin` no tenía asignaciones propias y recibía lista vacía. | En `PermisosEndpoints.cs` se unificó la consulta de proyectos activos resolviendo `liderPrincipalPorProyecto` (priorizando la fila cabecera `Idempleado == null && Idlider != null`), exponiendo tanto asignaciones propias (`EsAsignacionPropia = true`) como el catálogo activo de proyectos/clientes con su líder real, eliminando textos quemados y creando ficha base `Persona`/`Empleado` si un `Admin` registra una solicitud. |
| **4** | `POST http://localhost:5091/api/permisos/con-evidencia 400 (Bad Request)` al intentar subir el PDF al bucket de MinIO. | El dominio configurado (`http://isc-tmr-minio-d85269-147-93-181-146.sslip.io`) apunta en Dokploy al puerto `9001` (Consola Web de MinIO) y no al puerto `9000` (API S3), devolviendo HTML / `405` ante peticiones S3. | En `PermisosStorageService.cs` se implementó detección automática del modo Consola Web para operar vía `/api/v1` (login con cookie de sesión cacheada, creación automática del bucket `tmr-solicitudes`, subida multipart y descarga), además de reforzar `Host` y `Content-Length` en `CrearRequestFirmadaS3` para el puerto `9000`. |

---

## 3. Inventario de archivos del Backend

### Archivos creados
- `tmr-backend/Documentacion/Scripts_DB/19-Inv_tmr_db_modulo_solicitudes.sql`
- `tmr-backend/Documentacion/Scripts_DB/ROLLBACK_19-Inv_tmr_db_modulo_solicitudes.sql`
- `tmr-backend/Infrastructure/Database/Entities/TblTimeReportEstadoSolicitud.cs`
- `tmr-backend/Infrastructure/Database/Entities/TblTimeReportTipoPermiso.cs`
- `tmr-backend/Infrastructure/Database/Entities/TblTimeReportSolicitud.cs`
- `tmr-backend/Infrastructure/Database/Entities/TblTimeReportSolicitudRevision.cs`
- `tmr-backend/Infrastructure/Database/Entities/TblTimeReportSolicitudAdjunto.cs`
- `tmr-backend/Features/Notificaciones/Domain/NotificacionUsuario.cs`
- `tmr-backend/Features/Notificaciones/Application/Estado/NotificacionInternaNotifier.cs`

### Archivos modificados
- `tmr-backend/Program.cs`
- `tmr-backend/appsettings.Development.json`
- `tmr-backend/appsettings.json`
- `tmr-backend/Infrastructure/Database/ApplicationDbContext.cs`
- `tmr-backend/Infrastructure/Database/Entities/TblTimeReportPermiso.cs`
- `tmr-backend/Features/Notificaciones/NotificacionesModule.cs`
- `tmr-backend/Features/Notificaciones/Presentation/Endpoint/NotificacionesEndpoints.cs`
- `tmr-backend/Features/Permisos/DTOs/PermisosDtos.cs`
- `tmr-backend/Features/Permisos/Endpoints/PermisosEndpoints.cs`
- `tmr-backend/Features/Permisos/Services/PermisosStorageService.cs`
- `tmr-backend/Documentacion/Scripts_DB/00-ORDEN_DESPLIEGUE.md`

### Archivos eliminados / reemplazados
- `tmr-backend/Documentacion/Scripts_DB/19-Inv_tmr_db_modulo_permisos_ausencias.sql` *(reemplazado por `19-Inv_tmr_db_modulo_solicitudes.sql` en 3FN)*.

