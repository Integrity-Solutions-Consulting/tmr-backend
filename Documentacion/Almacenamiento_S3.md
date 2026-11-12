# Almacenamiento S3 de documentos de colaboradores

El proveedor se selecciona con `FileStorage__Provider`: `Local` conserva el comportamiento actual y `S3` usa el SDK oficial de AWS. Las lecturas y eliminaciones consultan el proveedor guardado en `tbl_actualizacion_archivo`, por lo que cambiar a S3 no migra ni inutiliza archivos locales existentes.

## Variables para Dokploy

No guardar estos valores en el repositorio. Las credenciales deben ser inyectadas por el entorno seguro de Dokploy o por el rol IAM de la instancia.

```text
FileStorage__Provider=S3
FileStorage__S3__Bucket=amazon-tmr-inv-integrity
FileStorage__S3__Region=us-east-2
FileStorage__S3__Endpoint=https://s3.us-east-2.amazonaws.com
FileStorage__S3__Prefix=tmr/produccion/colaboradores/
AWS_ACCESS_KEY_ID=<ACCESS_KEY_ID_DEL_ENTORNO>
AWS_SECRET_ACCESS_KEY=<SECRET_ACCESS_KEY_DEL_ENTORNO>
```

Para desarrollo, usar el mismo bloque cambiando únicamente el prefijo a `tmr/desarrollo/colaboradores/` y las credenciales de desarrollo. No se imprimen credenciales ni se generan URLs públicas o temporales.

## Comprobación real

1. Configurar temporalmente `FileStorage__S3__Prefix=tmr/prueba/colaboradores/` y credenciales de una identidad de prueba con permisos limitados al bucket/prefijo.
2. Arrancar el backend sin aplicar migraciones ni modificar políticas del bucket.
3. Autenticarse como colaborador, crear/enviar una solicitud y cargar un PDF ficticio menor de 10 MB.
4. Confirmar en PostgreSQL que el registro contiene `provider = s3` y que `storagekey` comienza por `tmr/prueba/colaboradores/`.
5. Descargarlo desde el endpoint autenticado del colaborador; comprobar que otro colaborador recibe `404`.
6. Eliminar el objeto de prueba y el registro únicamente después de concluir la verificación, según el procedimiento operativo del ambiente.

La conexión real no se considera validada hasta ejecutar estos pasos con credenciales configuradas; el build no prueba acceso a AWS.
