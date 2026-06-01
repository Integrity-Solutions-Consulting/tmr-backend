-- ════════════════════════════════════════════════════════════════════════════════
-- SCRIPT: ALTER TABLE para agregar UltimoJti a TblAutenticacionSesion
-- FECHA: 2026-05-28
-- PROPÓSITO: Rastrear el JTI del Access Token para mayor seguridad en Logout
-- ════════════════════════════════════════════════════════════════════════════════

-- 1. Agregar columna UltimoJti a TblAutenticacionSesion
ALTER TABLE tbl_autenticacion_sesion
ADD COLUMN ultim_jti VARCHAR(500) NULL;

-- 2. Crear índice para mejorar búsquedas por JTI
CREATE INDEX idx_autenticacion_sesion_ultim_jti 
ON tbl_autenticacion_sesion (ultim_jti);

-- 3. Crear índice compuesto para búsquedas de sesión activa por usuario y JTI
CREATE INDEX idx_autenticacion_sesion_user_jti
ON tbl_autenticacion_sesion (id_usuario, ultim_jti)
WHERE esta_activa = true;

-- ════════════════════════════════════════════════════════════════════════════════
-- VERIFICACIÓN: Mostrar la estructura actualizada
-- ════════════════════════════════════════════════════════════════════════════════
-- Comentar esta línea después de verificar
-- \d tbl_autenticacion_sesion;

-- ════════════════════════════════════════════════════════════════════════════════
-- ROLLBACK (en caso de necesidad):
-- ════════════════════════════════════════════════════════════════════════════════
-- DROP INDEX IF EXISTS idx_autenticacion_sesion_ultim_jti;
-- DROP INDEX IF EXISTS idx_autenticacion_sesion_user_jti;
-- ALTER TABLE tbl_autenticacion_sesion DROP COLUMN ultim_jti;
