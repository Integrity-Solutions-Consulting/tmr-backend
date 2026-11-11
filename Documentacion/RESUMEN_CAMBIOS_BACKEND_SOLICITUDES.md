# Resumen de Trabajo — Backend (`tmr-backend`)

**Fecha:** 06 de noviembre de 2026  
**Módulo principal:** Solicitudes de Permisos, Licencias y Vacaciones + Notificaciones SSE + Outbox Correo + Almacenamiento MinIO S3  
**Documento rector:** `docs/requerimientos-solicitudes-v2.docx`

---

## 1. Lo que quedó implementado y consolidado (Estado actual)

### 1.1. Base de Datos PostgreSQL normalizada en 3FN (Script 19)
Se implementó un modelo relacional en **Tercera Forma Normal (3FN)** dentro de `Documentacion/Scripts_DB/19-Inv_tmr_db_modulo_solicitudes.sql`, compuesto por **6 tablas**:

1. **`time_report.tbl_time_report_estado_solicitud`**: Catálogo normalizado de estados del flujo (`PENDIENTE_JEFE`, `PENDIENTE_TH`, `OBSERVADA`, `APROBADA`, `RECHAZADA`) con código de semáforo (`AMARILLO`, `VERDE`, `ROJO`).
2. **`time_report.tbl_time_report_tipo_permiso`**: Catálogo oficial de los 8 tipos de solicitud del formulario de Talento Humano (`VACACIONES`, `LICENCIA_PATERNIDAD`, `LICENCIA_MATERNIDAD`, `PERMISO_PERSONAL`, `LICENCIA_CALAMIDAD`, `LICENCIA_ENFERMEDAD`, `PERMISO_ESTUDIOS`, `OTRO`).
3. **`time_report.tbl_time_report_solicitud`**: Cabecera transaccional de solicitudes vinculada por llaves foráneas reales hacia `administracion.tbl_administracion_empleado`, `time_report.tbl_time_report_asignacion_proyecto`, `time_report.tbl_time_report_proyecto`, `administracion.tbl_administracion_cliente`, `administracion.tbl_administracion_lider`, `time_report.tbl_time_report_tipo_permiso` y `time_report.tbl_time_report_estado_solicitud`.
4. **`time_report.tbl_time_report_solicitud_revision`**: Historial normalizado de aprobaciones, observaciones y rechazos (Nivel 1: `JEFE_INMEDIATO` por `CUENTA_TMR` o `MAGIC_LINK_EXTERNO`; Nivel 2: `TALENTO_HUMANO`).
5. **`time_report.tbl_time_report_solicitud_adjunto`**: Metadatos de archivos de respaldo subidos a MinIO (`bucket_name`, `object_key` con UUID, `content_type`, `tamano_bytes`, `sha256`, `subido_por_usuario_id`).
6. **`notificacion.notificacion_usuario`**: Bandeja persistida para la campana de notificaciones internas en tiempo real por usuario destino.

Adicionalmente:
- Se dejó `Documentacion/Scripts_DB/ROLLBACK_19-Inv_tmr_db_modulo_solicitudes.sql` para reversión limpia e idempotente.
- Se mantuvo intacta `Infrastructure/Database/Entities/TblTimeReportPermiso.cs` (tabla original `time_report.tbl_time_report_permisos` usada por Time Report clásico).
- El esquema se gestiona exclusivamente mediante scripts de migración SQL (sin ejecución de DDL en tiempo de petición HTTP).

### 1.2. Notificaciones en Tiempo Real (SSE) y Envío de Correos Resiliente (`EventoOutbox`)
- **Cola persistida `EventoOutbox` para Correos**: El envío de correos electrónicos de Solicitudes (`PermisosNotificacionService.cs`) encola eventos `CorreoPermisoEncolado` con payload ligero (`IdSolicitud`, `DestinatarioEmail`, `Evento`, `IncluirTokenExterno`) en `notificacion.evento_outbox`, procesados asíncronamente por `EventDispatcher` y `EnviarCorreoPermisoHandler` con hasta 5 reintentos automáticos y sin bloquear las transacciones HTTP.
- **SSE para Solicitudes (`RF-010`)**: Las notificaciones de campana del módulo de Solicitudes persisten en `notificacion.notificacion_usuario` (`NotificacionUsuario.cs`) con truncamiento seguro a 500 caracteres y empujan eventos instantáneos por **Server-Sent Events** mediante `NotificacionInternaNotifier.cs` y los endpoints en `NotificacionesEndpoints.cs`:
  - `GET /api/notificaciones/internas`
  - `GET /api/notificaciones/internas/stream` (SSE en vivo)
  - `PATCH /api/notificaciones/internas/{id}/leer`
  - `PATCH /api/notificaciones/internas/leer-todas`

### 1.3. Endpoints, Políticas RBAC y Reglas de Negocio 100% conectados a PostgreSQL
En `Features/Permisos/Endpoints/PermisosEndpoints.cs` y `Features/Permisos/DTOs/PermisosDtos.cs`:
- **`GET /api/permisos/contexto` (`ficha-colaborador`)** (`MIS_PERMISOS_READ`): Lee desde PostgreSQL los datos del colaborador autenticado (ficha activa de empleado, régimen contractual Nómina vs Servicios Profesionales, proyectos asignados, líderes disponibles, catálogo de tipos de permiso y catálogo de usuarios de Talento Humano RRHH).
- **`GET /api/permisos/mis-permisos` (`mis-solicitudes`)** (`MIS_PERMISOS_READ`), **`POST /api/permisos` (`solicitudes`)** y **`POST /api/permisos/con-evidencia`** (`MIS_PERMISOS_CREATE`):
  - Exige que el usuario autenticado posea una ficha activa en `administracion.tbl_administracion_empleado` (no crea empleados ficticios).
  - Validación de régimen contractual (`ResolverRegimenContratoAsync`) y regla **`RF-004`** (certificado médico obligatorio cuando el colaborador es de **Nómina**, solicita **Licencia por enfermedad** y supera **1 día hábil**).
  - Cálculo de días laborables descontando fines de semana y feriados registrados en `time_report.tbl_time_report_feriados` (`CargarFeriadosRangoAsync`).
  - Validación contra traslape de fechas con otras solicitudes activas del mismo colaborador (permitiendo medias jornadas complementarias `MANANA` / `TARDE` en el mismo día).
  - Cálculo de saldo de vacaciones según antigüedad real (`Fechaingreso`, Art. 69 Código del Trabajo: 15 días base + 1 día adicional por año a partir del 6.º año, hasta 15 adicionales) menos días consumidos o en trámite en el año.
  - Generación de código secuencial `PER-{ANIO}-{CONSECUTIVO}` resistente a concurrencia (reintentos ante `23505 UniqueViolation`) y limpieza automática en MinIO si la transacción de base de datos falla.
- **Flujo completo de revisión y reportes con máquina de estados estricta**:
  - Bandeja de Jefe Inmediato interno: `GET /api/permisos/equipo` (`PERMISOS_LIDER_READ`) y `POST /api/permisos/{id}/decision-lider` (`PERMISOS_LIDER_APPROVE`, requiere estado `PENDIENTE_JEFE`).
  - Revisión pública por token para Líder Externo (`AllowAnonymous` + `RequireRateLimiting("magic-link")`): `GET /api/permisos/externo/{token}`, `GET /api/permisos/externo/{token}/pdf`, `GET /api/permisos/externo/{token}/evidencia` y `POST /api/permisos/externo/{token}/decision` (con soporte de `aprobar`, `observar` y `rechazar`).
  - Bandeja de confirmación final de Talento Humano: `GET /api/permisos/rrhh` (`PERMISOS_RRHH_READ`), `POST /api/permisos/{id}/decision-rrhh` (`PERMISOS_RRHH_CONFIRM`, valida estado `PENDIENTE_TH` y acciones `confirmar` / `devolver`) y `POST /api/permisos/{id}/reenviar-enlace` (`PERMISOS_RRHH_CONFIRM`).
  - Vista consolidada de Gerencia: `GET /api/permisos/gerencia` (`PERMISOS_GERENCIA_READ`).
  - Generación de PDF oficial con QuestPDF (`GET /api/permisos/{id}/pdf`) y gestión de evidencias (`GET /api/permisos/{id}/evidencia`, `POST /api/permisos/{id}/evidencia`).

### 1.4. Blindaje de Seguridad (Permisos, Magic Link, Storage y Notificaciones)
- **Protección de Magic Link y Prevención de Enumeración/SQLi**:
  - `TokenExterno` se genera únicamente cuando el Jefe Inmediato asignado es **Externo (`EXT`)** (`null` para líderes internos `INT`).
  - Validación pre-BD `EsTokenExternoValido(token)` (exactamente 32 caracteres hexadecimales GUID formato `"N"`), verificación `EsLiderExterno` en los 4 endpoints públicos `/externo/{token}*`, control de un solo uso (`TokenExternoUsadoEn`), expiración de 72 horas y política de Rate Limiting `"magic-link"` (30 peticiones/minuto por IP en `Program.cs`).
- **Validación de Destinatario RRHH (`EsUsuarioRrhhValidoAsync`)**:
  - Impide que un `idUsuarioRrhh` manipulado en el request envíe notificaciones de RRHH a usuarios que no pertenecen a Talento Humano.
- **Límites de Entrada y Consistencia Numérica**:
  - Validación de longitud máxima en `Motivo` (`<= 2000`), `Comentario`/`Observacion` (`<= 2000`), `DetalleHorario` (`<= 120`), rango máximo de `90` días hábiles por solicitud, restricción de media jornada (`MANANA`/`TARDE`) a un único día y acotamiento de `TotalHoras` en `OTROS` a `[0.5, diasLaborables * 8]`.
  - En `NotificacionesEndpoints.cs`, validación de `empleadoIds` (`> 0` y `<= 200`) y rango de `desdeMs` para prevenir excepciones `ArgumentOutOfRangeException` (`500`).
- **Almacenamiento Seguro MinIO S3 / Fallback Local (`PermisosStorageService.cs`)**:
  - Verificación de cabecera binaria real (*magic bytes*), sanitización de caracteres peligrosos (`< > " ' ; \r \n \0`) en nombres de archivo y protección canónica contra *Path Traversal* (`../../`) en lectura y eliminación de adjuntos.

---

## 2. Inventario de archivos del Backend

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
- `tmr-backend/Infrastructure/Database/ApplicationDbContext.cs`
- `tmr-backend/Infrastructure/Database/Entities/TblTimeReportPermiso.cs`
- `tmr-backend/Features/Notificaciones/NotificacionesModule.cs`
- `tmr-backend/Features/Notificaciones/Presentation/Endpoint/NotificacionesEndpoints.cs`
- `tmr-backend/Features/Permisos/DTOs/PermisosDtos.cs`
- `tmr-backend/Features/Permisos/Endpoints/PermisosEndpoints.cs`
- `tmr-backend/Features/Permisos/Services/PermisosNotificacionService.cs`
- `tmr-backend/Features/Permisos/Services/PermisosPdfService.cs`
- `tmr-backend/Features/Permisos/Services/PermisosStorageService.cs`
- `tmr-backend/Documentacion/Scripts_DB/00-ORDEN_DESPLIEGUE.md`
