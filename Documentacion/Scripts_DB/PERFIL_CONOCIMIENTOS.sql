-- Perfil: areas de conocimiento y su porcentaje.
-- Equivalente a la migracion EF 20261119120000_AddConocimientosPerfil.
-- Requiere 20261112120000_ActualizacionDirectaYSolicitudes.
-- Referencia para ejecucion manual; el instalador historico solo carga scripts numerados.
BEGIN;
ALTER TABLE administracion.tbl_colaborador_datos_control ADD COLUMN IF NOT EXISTS conocimientos jsonb;
COMMIT;
