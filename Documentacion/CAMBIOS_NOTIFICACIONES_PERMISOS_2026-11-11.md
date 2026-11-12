# Notificaciones de permisos: tres MVP — 11 de noviembre de 2026

## Resultado
Se conserva el flujo actual de solicitudes y se refuerza su comunicación: persistencia transaccional, acceso directo a la solicitud vigente y seguimiento de envíos desde RRHH. No requiere migraciones ni nuevas tablas.

## 1. Persistencia y recuperación
- Crear una solicitud guarda sus avisos antes de confirmar la transacción. Las decisiones del líder, RRHH y enlace externo, y el reenvío del enlace, agrupan decisión y avisos en una transacción.
- Se bloquea la fila de la solicitud para serializar decisiones simultáneas: una decisión gana; la siguiente recibe la validación del estado actual. No se reproduce automáticamente la respuesta de la primera decisión.
- La campana se persiste con la operación; su actualización SSE se procesa mediante outbox. Reprocesar esa señal no inserta otra notificación.
- El dispatcher recupera eventos pendientes periódicamente (30 segundos por defecto), incluso si faltó la señal en memoria. Bloquea cada evento para coordinar varias instancias.
- Un fallo revierte los efectos parciales del handler y guarda el intento. Tras cinco fallos queda Fallido. Esta mejora pertenece al dispatcher compartido y también afecta otros eventos del sistema.
- Los correos nuevos conservan una instantánea, estado y versión de la solicitud. Avisos que dejaron de ser vigentes se omiten; los enlaces externos también se verifican por vigencia, uso y token.
- Se evita encolar dos veces al mismo correo dentro de una misma notificación de cambio de estado.

## 2. Acceso y mensajes
- Campana y correos internos incluyen el identificador de la solicitud. El frontend consulta su estado actual y abre el detalle correspondiente en colaborador, líder o RRHH.
- La consulta individual conserva la autorización y pertenencia existentes. Un enlace no concede acceso por sí mismo.
- Una solicitud que ya no está pendiente del líder deja de mostrar acciones de decisión en su revisión.
- Los avisos informativos finales al líder externo no incluyen una llamada a entrar al portal interno. Se conserva el Magic Link para las acciones externas vigentes.
- Los mensajes de éxito distinguen guardar la decisión y dejar el correo pendiente de envío. Se eliminan afirmaciones de envío inmediato y de firmas digitales no implementadas.
- Se incluyen los ajustes previos de duración: lenguaje humano en lugar de decimales para el resumen y las validaciones.

## 3. Seguimiento de envíos en RRHH
- Panel desplegable en revisión y detalle: consulta al abrirlo, actualización manual, errores legibles y botón de reintento protegido contra doble clic.
- Muestra hasta los últimos 100 correos y un resumen de hasta 100 notificaciones de campana por solicitud.
- Estados: Pendiente, Enviado al proveedor, Fallido y No vigente. Leer la campana no significa aprobar; enviar al proveedor no confirma recepción ni lectura.
- Solo RRHH/admin con los permisos requeridos consulta envíos o reintenta un correo fallido que siga vigente.
- Cada reintento crea un evento nuevo vinculado al anterior, con usuario y fecha. El original conserva su historial. Reintentos duplicados y avisos obsoletos se rechazan con conflicto.
- La API no devuelve tokens, payload completo ni errores técnicos del proveedor.

## Verificación realizada
- Backend: compilación correcta y 17 comprobaciones con PostgreSQL local aislado. Incluyen vigencia, precisión temporal de PostgreSQL, rollback, dos decisiones concurrentes, dos workers, cinco fallos, efectos parciales y recuperación sin señal en memoria.
- Frontend: compilación de producción correcta; mantiene advertencias de presupuestos CSS y dependencias CommonJS de archivos no modificados por este trabajo.
- Panel RRHH: cuatro pruebas aprobadas (carga bajo demanda, bloqueo de doble clic, recuperación del error y descarte de respuestas de otra solicitud).
- Revisión visual local del panel y su error de carga. El backend que estaba ejecutándose era la versión anterior; no se reinició la aplicación del usuario ni se ejecutaron aprobaciones, reintentos reales o correos contra datos compartidos.
- No se afirma QA integral de autenticación HTTP, entrega SMTP ni despliegue Dokploy. Las pruebas generales antiguas del frontend contienen referencias rotas; la suite nueva usa configuración e inclusión específicas.

### Reproducir
Backend, desde su repositorio:
```powershell
dotnet build tests/NotificacionesSmoke/NotificacionesSmoke.csproj -p:UseAppHost=false -o <salida-aislada>
$env:TMR_TEST_POSTGRES='Host=127.0.0.1;Port=<puerto>;Database=tmr_notifications_tests;Username=postgres'
dotnet <salida-aislada>/NotificacionesSmoke.dll
```
La base debe ser exclusiva de pruebas: la suite prepara y limpia sus tablas. Nunca usar la base de la aplicación.

Frontend:
```powershell
npx ng test --watch=false --ts-config tsconfig.notificaciones.spec.json --include 'src/app/features/time-report/permisos/envios-permiso/envios-permiso.spec.ts'
npm run build -- --configuration production --output-path <salida-aislada>
```

## Alcance y límites
- SMTP es entrega al menos una vez: si el proveedor acepta un correo y el proceso cae antes de guardar el resultado, un reintento podría duplicarlo. Los bloqueos evitan procesamiento concurrente normal, no garantizan exactamente un envío externo.
- Los eventos históricos sin instantánea conservan compatibilidad; no se puede reconstruir retroactivamente su contenido original. No se reparan automáticamente avisos históricos faltantes.
- No se incorpora edición/reenvío por el colaborador, cancelación, recordatorios por SLA ni nuevas reglas de selección entre múltiples líderes/proyectos. Se conserva la lógica actual de asignación y devolución.
- Se reutiliza el JSON del outbox; si crece mucho su volumen, medir las consultas de historial antes de añadir índices o paginación.
- La configuración local appsettings.Development.json queda fuera de los commits. No se modifican credenciales de despliegue.

## Publicación
Publicar backend y después frontend de develop para que las rutas de historial y consulta individual estén disponibles al cargar la nueva interfaz. No se requiere migración de BD. Subir los commits no equivale a confirmar que Dokploy terminó el despliegue.
