ALTER TYPE autenticacion.revocacion_razon_enum ADD VALUE 'SESSION_LIMIT';
ALTER TYPE autenticacion.revocacion_razon_enum ADD VALUE 'SESSION_IDLE_TIMEOUT';
ALTER TYPE autenticacion.revocacion_razon_enum ADD VALUE 'SESSION_EXPIRED';
ALTER TYPE autenticacion.revocacion_razon_enum ADD VALUE 'REVOKE';
ALTER TYPE autenticacion.revocacion_razon_enum ADD VALUE 'ADMIN_REVOKE_ALL';
ALTER TYPE autenticacion.revocacion_razon_enum ADD VALUE 'USER_AGENT_MISMATCH';
ALTER TYPE autenticacion.revocacion_razon_enum ADD VALUE 'IP_MISMATCH';
ALTER TYPE autenticacion.revocacion_razon_enum ADD VALUE 'TOKEN_REUSED';
ALTER TYPE autenticacion.revocacion_razon_enum ADD VALUE 'TOKEN_REVOKED';
 
 
ALTER TABLE 
autenticacion.tbl_autenticacion_sesion
ALTER COLUMN revocadorazon TYPE
VARCHAR(50)
USING revocadorazon::text;