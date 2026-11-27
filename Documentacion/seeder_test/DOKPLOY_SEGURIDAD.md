# Fixtures y comando global en Dokploy

## Proteccion de fixtures

`SincronizadorCuentasPermisos` queda deshabilitado por defecto, incluido
Development. Solo se habilita en un entorno aislado cuando coinciden:

```text
ASPNETCORE_ENVIRONMENT=Testing
Fixtures__EnableAccountSync=true
```

La sincronizacion puede crear o reactivar cuentas, personas, empleados,
contratos, asignaciones, roles y permisos de las cuentas estandar; tambien
puede restablecer la contrasena de `admin@tmr.com`. No se debe habilitar en
Dokploy, Production, Staging ni Development.

## Ejecucion sin asumir Compose

Dokploy puede ejecutar la imagen directamente y no aplicar `docker-compose.yml`.
Antes de una tarea administrativa:

1. Identificar el contenedor y la imagen reales de la aplicacion.
2. Confirmar `ASPNETCORE_ENVIRONMENT` y `DOTNET_ENVIRONMENT`, sin confiar solo
   en el texto de los logs.
3. Confirmar el digest de la imagen, el entrypoint `dotnet tmr-backend.dll` y
   la version que se quiere operar.
4. Verificar host, puerto, base y usuario del destino mediante metadatos
   redactados. No imprimir valores de `ConnectionStrings__DefaultConnection`,
   `DB_CONNECTION_STRING` ni otros secretos.
5. Obtener aprobacion para modificar esa base y comprobar que no es una base
   real equivocada, staging o una copia no autorizada.

No asumir que existe un servicio Compose llamado `backend`. Usar una tarea
puntual de Dokploy o un contenedor efimero basado en la imagen exacta, con la
misma inyeccion de variables y secretos de la aplicacion. Un ejemplo generico
(el mecanismo de secretos de Dokploy reemplaza el `--env-file`) es:

```bash
docker run --rm \
  --env-file <archivo-no-secreto-aprobado> \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e Fixtures__EnableAccountSync=false \
  <imagen-real>:<tag-o-digest> --initialize-seguimiento-global
```

No copiar contrasenas en la linea de comandos ni en logs. El comando escribe
solo `SEGUIMIENTO_ACTIVIDADES_GLOBAL_WRITE`, el rol `Lider General` y sus
relaciones de permiso; no ejecuta fixtures ni migraciones. Escribir esas filas
requiere confirmar el destino previamente. Luego puede ejecutarse
`--verify-database` como comprobacion de solo lectura.

## Verificacion segura de configuracion

En PowerShell, mostrar unicamente variables no sensibles y presencia de
secretos, nunca sus valores:

```powershell
docker inspect <contenedor> --format '{{range .Config.Env}}{{println .}}{{end}}' |
  ForEach-Object {
    if ($_ -match '^(ASPNETCORE_ENVIRONMENT|DOTNET_ENVIRONMENT|Fixtures__EnableAccountSync)=') { $_ }
    elseif ($_ -match '^(ConnectionStrings__DefaultConnection|DB_CONNECTION_STRING)=') { ($_ -split '=',2)[0] + '=***configured***' }
  }
docker inspect <contenedor> --format 'image={{.Config.Image}} entrypoint={{json .Config.Entrypoint}} cmd={{json .Config.Cmd}}'
docker image inspect <imagen>:<tag> --format '{{.Id}} {{.RepoDigests}}'
```

Estas comprobaciones son informativas. No ejecutar el inicializador, ni
migraciones, ni comandos que abran una conexion a la base hasta aprobar el
destino y la ventana de cambio.
