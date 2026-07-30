-- =============================================================================
--  SCRIPT DE ADICIÓN — TABLA PASSWORD RESET
--  Base de datos : Inv_tmr_db
--  Motor         : PostgreSQL 16+
--  Descripción   : Crea tabla para gestionar tokens de restablecimiento de contraseña
--  Fecha         : 2026-07-29
-- =============================================================================
-- EJECUCIÓN:
--   psql -U mig-user -d isc-tmr-migration -f 10-Inv_tmr_db_add_password_reset.sql
-- =============================================================================

BEGIN;

-- ---------------------------------------------------------------------------
-- TABLA: tbl_autenticacion_password_reset
-- PROPÓSITO: Almacenar tokens temporales para restablecimiento de contraseña
-- CARACTERÍSTICAS:
--   - Tokens con expiración de 30 minutos
--   - One-time use (se marcan como utilizados)
--   - Auditoría completa de creación
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_password_reset (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    TokenHash           VARCHAR(255)    NOT NULL,
    FechaExpiracion     TIMESTAMPTZ     NOT NULL,
    Utilizado           BOOLEAN         NOT NULL DEFAULT FALSE,
    FechaUtilizacion    TIMESTAMPTZ,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    IpCreacion          VARCHAR(45)     NOT NULL,
    CONSTRAINT pk_autenticacion_password_reset PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_password_reset_usuario
        FOREIGN KEY (IdUsuario)
        REFERENCES autenticacion.tbl_autenticacion_usuario(Id)
        ON DELETE CASCADE
);

-- Índices para optimización
CREATE INDEX idx_autenticacion_password_reset_usuario 
    ON autenticacion.tbl_autenticacion_password_reset(IdUsuario);

CREATE INDEX idx_autenticacion_password_reset_token 
    ON autenticacion.tbl_autenticacion_password_reset(TokenHash);

CREATE INDEX idx_autenticacion_password_reset_expiracion 
    ON autenticacion.tbl_autenticacion_password_reset(FechaExpiracion);

-- Comentarios
COMMENT ON TABLE autenticacion.tbl_autenticacion_password_reset 
    IS 'Tokens temporales para restablecimiento de contraseña con expiración de 30 minutos';

COMMENT ON COLUMN autenticacion.tbl_autenticacion_password_reset.TokenHash 
    IS 'Hash del token aleatorio (no almacenar en plaintext)';

COMMENT ON COLUMN autenticacion.tbl_autenticacion_password_reset.FechaExpiracion 
    IS 'Marca de tiempo de expiración del token (30 minutos desde creación)';

COMMENT ON COLUMN autenticacion.tbl_autenticacion_password_reset.Utilizado 
    IS 'Flag para marcar si el token ya fue utilizado (one-time use)';

COMMIT;
