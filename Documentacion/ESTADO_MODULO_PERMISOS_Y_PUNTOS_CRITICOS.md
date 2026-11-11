# Estado del Módulo de Vacaciones, Permisos y Notificaciones

> **Estado Técnico en Código:** **0 puntos críticos faltantes (100 % completado y verificado)**
> **Pruebas Automatizadas (`NotificacionesSmoke`):** **58 / 58 aserciones superadas**

---

## 1. ¿Falta algún punto crítico en código?

**No.** Como resultado de la auditoría, refactorización y pruebas integrales realizadas, **no queda ningún punto crítico pendiente a nivel de código (Backend, Frontend ni Base de Datos)**.

Todo el flujo de extremo a extremo está conectado, simplificado y blindado:

1. **Integración Real con Time Report y Seguimiento (`RESTA_META`, `SUMA_REPORTADAS`, `SIN_IMPACTO`):**
   - `SeguimientoService` y `TimeReportService` cruzan automáticamente los permisos en estado `APROBADA` con `CalculoHorasPeriodo.Calcular`.
   - `ConsultaHorasFaltantes` descuenta los días/horas de permisos aprobados (`RESTA_META` o `SUMA_REPORTADAS`) para no penalizar ni enviar recordatorios de horas faltantes a colaboradores en vacaciones o con permiso aprobado.
2. **Cálculo y Sincronización de Saldo de Vacaciones:**
   - Cálculo dinámico basado en la fecha de ingreso (15 días por año cumplido + días adicionales por antigüedad a partir del 6.º año, con tope legal de 30 días) menos los días consumidos/comprometidos.
   - Al aprobarse, rechazarse o cancelarse una solicitud de vacaciones, el saldo disponible del colaborador se actualiza de inmediato.
3. **Módulo de Notificaciones (Campana SSE + Outbox de Correo + Auditoría):**
   - `PermisosNotificacionService.cs` fue refactorizado y simplificado (de **796 a 469 líneas**), eliminando código muerto, consultas triplicadas a base de datos y parámetros obsoletos.
   - `PermisoAvisoVigencia.cs` valida estrictamente `solicitud.Estado.Codigo == evento.EstadoSolicitud`, evitando correos rezagados cuando una solicitud ya cambió de estado, pero permitiendo solicitudes que nacen directamente en `APROBADA` (informativas) o `PENDIENTE_TH` (sin líder/proyecto).
   - Plantilla HTML corporativa minimalista (`notificacion-permiso.html`) con formato limpio de período (`11/11/2026 · 08:30 - 17:30`) y duración (`1 día · 8 horas` o `2 horas`), sin paréntesis anidados.
4. **Cuentas de Prueba Sincronizadas y Escenarios Fuera de lo Común Cubiertos:**
   - Sincronización automática en login (`SincronizadorCuentasPermisos.cs`) y mediante script SQL (`SINCRONIZAR_CUENTAS_PRUEBA.sql`) para las 5 cuentas: `admin@tmr.com`, `colaborador@tmr.com`, `lider@tmr.com`, `ger1@tmr.com` y `rec1@tmr.com`.
   - **Autoprotección de Líder:** Si `lider@tmr.com` solicita un permiso para sí mismo en un proyecto donde él mismo es el líder, el sistema salta la auto-aprobación (`idLider = null`) y lo envía directo a Talento Humano (`PENDIENTE_TH`).
   - **Permisos sin proyecto / multi-proyecto / informativos:** Flujos directos a `PENDIENTE_TH` o `APROBADA` operativa sin bloqueos.

---

## 2. Únicos Puntos Externos / Operativos (Fuera del Código)

Aunque en programación no falta nada, ten en cuenta estos **2 puntos externos/de entorno** antes de la presentación con tu jefa (**Tanya**):

| # | Categoría | Descripción | Acción recomendada |
|---|---|---|---|
| **1** | **Definición de Negocio (Tanya)** | Confirmar con Tanya si desea ajustar algún parámetro del catálogo `permisos.tipo_solicitud` (por ejemplo, si algún permiso hoy configurado con aprobación debe pasar a ser solo informativo, o viceversa, o si desea agregar algún tipo adicional). | El catálogo ya soporta el cambio en caliente mediante la columna `requiere_aprobacion` e `impacto_time_report` en base de datos, sin tocar código. |
| **2** | **Variables de Entorno Locales (Correo y Adjuntos)** | 1) **Correos (`Notificaciones`):** Si en tu máquina local quieres que todos los correos de las 5 cuentas lleguen a tu propia bandeja durante la demo, mantén configurado `Notificaciones:OverrideEmailRecipient` en tu `appsettings.Development.json`.<br>2) **Adjuntos (`MinIO`):** Si no tienes el contenedor de MinIO levantado durante la demo, asegúrate de tener `"MinIO": { "AllowLocalFallback": true }` para que los certificados médicos se guarden y descarguen desde disco local sin error. | Verificar `appsettings.Development.json` antes de iniciar la presentación. |

---

## 3. Guía Rápida de Cuentas Sincronizadas para la Demo

Al iniciar sesión con cualquiera de las 5 cuentas `@tmr.com` (o al ejecutar `Documentacion/Scripts_DB/SINCRONIZAR_CUENTAS_PRUEBA.sql`), quedan listas con la siguiente estructura:

| Cuenta | Rol asignado | Antigüedad configurada | Rol en el flujo de Vacaciones / Permisos |
|---|---|---|---|
| `colaborador@tmr.com` | Colaborador (`COLAB`) | ~3 años (45 días acumulados) | Solicita vacaciones/permisos asignado al proyecto **Proyecto Flujo Permisos TMR**. |
| `lider@tmr.com` | Líder (`LIDER`) | ~4 años (60 días acumulados) | Líder asignado a **Proyecto Flujo Permisos TMR**. Aprueba/rechaza en **Paso 1** (`PENDIENTE_LIDER`) o solicita permisos propios (que van directo a RRHH). |
| `rec1@tmr.com` | Recursos Humanos (`RRHH`) | ~5 años | Bandeja de Talento Humano. Aprueba/rechaza en **Paso 2** (`PENDIENTE_TH`) y recibe alertas de permisos informativos. |
| `ger1@tmr.com` | Gerente (`GERENTE`) | ~6 años | Visualización gerencial / reportes y flujos de supervisión. |
| `admin@tmr.com` | Administrador (`ADMIN`) | ~6 años | Acceso total a todas las bandejas, catálogos y aprobaciones. |

### Scripts SQL de Apoyo para tus Pruebas
- **Sincronizar las 5 cuentas manualmente (opcional, también corre solo al hacer login):**
  `tmr-backend/Documentacion/Scripts_DB/SINCRONIZAR_CUENTAS_PRUEBA.sql`
- **Limpiar todas las solicitudes y notificaciones (`TRUNCATE`) para reiniciar la demo desde cero:**
  `tmr-backend/Documentacion/Scripts_DB/LIMPIAR_SOLICITUDES_PRUEBA.sql`
