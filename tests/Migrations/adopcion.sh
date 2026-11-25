#!/usr/bin/env bash
# Uso de CI: PG_CONTAINER es un servicio PostgreSQL efímero y APP_IMAGE la imagen de este checkout.
set -euo pipefail
: "${PG_CONTAINER:?}" "${APP_IMAGE:?}" "${PGUSER:?}" "${PGPASSWORD:?}"
pg() { docker exec -i -e PGPASSWORD "$PG_CONTAINER" "$@"; }
query() { pg psql -XAtq -v ON_ERROR_STOP=1 -U "$PGUSER" -d "$1" -c "$2"; }
run_app() {
  local db=$1 stage=$2
  local ConnectionStrings__DefaultConnection="Host=127.0.0.1;Database=$db;Username=$PGUSER;Password=$PGPASSWORD"
  export ConnectionStrings__DefaultConnection
  docker run --rm --network "container:$PG_CONTAINER" -e ConnectionStrings__DefaultConnection "$APP_IMAGE" "$stage"
}
signature() {
  pg psql -XAtq -v ON_ERROR_STOP=1 -U "$PGUSER" -d "$1" -c 'SET search_path=pg_catalog' -f - < Migrations/Baseline/VerificarEstructura.sql
}

fresh=tmr_ci_baseline_ref
copy=tmr_ci_baseline_copy
pg createdb -U "$PGUSER" "$fresh"
run_app "$fresh" --prepare-database
pg createdb -U "$PGUSER" -T "$fresh" "$copy"
# Solo sobre la copia recién creada: simular una base preexistente sin historial.
query "$copy" 'DROP TABLE public."__EFMigrationsHistory"'
query "$copy" "INSERT INTO administracion.tbl_administracion_persona
 (numeroidentificacion,tipopersona,nombres,apellidos,usuariocreacion,ipcreacion)
 VALUES ('CI-PR2','NATURAL','Prueba','Migraciones','CI','127.0.0.1')"
row_sql="SELECT md5(row_to_json(p)::text) FROM administracion.tbl_administracion_persona p WHERE numeroidentificacion='CI-PR2'"
row_before=$(query "$copy" "$row_sql")
test -n "$row_before"

# Un permiso desactivado localmente debe conservar su decisión tras adoptar.
query "$copy" "UPDATE autenticacion.tbl_autenticacion_rol_permiso rp SET activo=false
 FROM autenticacion.tbl_autenticacion_rol r, autenticacion.tbl_autenticacion_permiso p
 WHERE rp.idrol=r.id AND rp.idpermiso=p.id AND upper(r.nombre)='GERENTE' AND p.codigo='PERMISOS_VIEW'"
grant_sql="SELECT rp.activo FROM autenticacion.tbl_autenticacion_rol_permiso rp
 JOIN autenticacion.tbl_autenticacion_rol r ON r.id=rp.idrol
 JOIN autenticacion.tbl_autenticacion_permiso p ON p.id=rp.idpermiso
 WHERE upper(r.nombre)='GERENTE' AND p.codigo='PERMISOS_VIEW'"
test "$(query "$copy" "$grant_sql")" = f

query "$copy" 'ALTER TABLE administracion.tbl_administracion_persona ADD COLUMN prueba_ci integer'
if run_app "$copy" --adopt-baseline; then echo 'Se aceptó una estructura distinta' >&2; exit 1; fi
test "$(query "$copy" "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NULL")" = t
query "$copy" 'ALTER TABLE administracion.tbl_administracion_persona DROP COLUMN prueba_ci'
run_app "$copy" --adopt-baseline
run_app "$copy" --adopt-baseline
run_app "$fresh" --apply-migrations
run_app "$copy" --apply-migrations
run_app "$copy" --prepare-database
run_app "$copy" --apply-migrations
test "$(query "$copy" "$row_sql")" = "$row_before"
test "$(query "$copy" "$grant_sql")" = f
expected=$(signature "$fresh")
actual=$(signature "$copy")
test -n "$expected"
test "$expected" = "$actual"

# Simular una importación que ya tenía la tabla antes de registrarse en EF.
staging=time_report.tbl_time_report_permiso_falta_importacion
query "$copy" "INSERT INTO $staging
 (fila_excel,nombre_excel,requerimiento_excel,fecha_excel,huella,nota_revision)
 VALUES (2,'Persona CI','PERMISO','2026-01-10',repeat('a',64),'Pendiente de respaldo')"
staging_row="SELECT md5(row_to_json(p)::text) FROM $staging p WHERE fila_excel=2"
staging_before=$(query "$copy" "$staging_row")
query "$copy" "DELETE FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261125000000_ImportacionPermisosFaltas'"
run_app "$copy" --apply-migrations
run_app "$copy" --apply-migrations
test "$(query "$copy" "$staging_row")" = "$staging_before"
test "$(query "$copy" "SELECT count(*) FROM public.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261125000000_ImportacionPermisosFaltas'")" = 1
test "$(query "$copy" "SELECT count(*) FROM $staging WHERE id_solicitud IS NOT NULL")" = 0
history='SELECT "MigrationId" FROM public."__EFMigrationsHistory" ORDER BY 1'
expected=$(query "$fresh" "$history")
actual=$(query "$copy" "$history")
test -n "$expected"
test "$expected" = "$actual"
