@echo off
set PGPASSWORD=root
set PSQL="C:\Program Files\PostgreSQL\18\bin\psql.exe"

echo Running scripts...
%PSQL% -U postgres -d inv_tmr_db -f 01-Inv_tmr_db_deploy_29052026.sql
%PSQL% -U postgres -d inv_tmr_db -f "02-Inv_tmr_catalogos actualizado.v3.sql"
%PSQL% -U postgres -d inv_tmr_db -f 03-Inv_tmr_db_ajustes_tblSesion_tblProyecto.sql
%PSQL% -U postgres -d inv_tmr_db -f 04-Inv_tmr_db_inserts_tipoActividades.sql
%PSQL% -U postgres -d inv_tmr_db -f 05-Inv_tmr_db_inserts_permisos_modulos.sql
%PSQL% -U postgres -d inv_tmr_db -f 06-Inv_tmr_db_insert_usuario_luis_fernando_sanchez.sql
%PSQL% -U postgres -d inv_tmr_db -f 07-Inv_tmr_db_alter_tblProyecto.sql
%PSQL% -U postgres -d inv_tmr_db -f 08-Inv_tmr_db_alter_tblEmpleadoProyecto.sql
%PSQL% -U postgres -d inv_tmr_db -f 09-Inv_tmr_db_alter_tblEmpleados_salida.sql
%PSQL% -U postgres -d inv_tmr_db -f 10-Inv_tmr_db_add_password_reset.sql
echo Done.
