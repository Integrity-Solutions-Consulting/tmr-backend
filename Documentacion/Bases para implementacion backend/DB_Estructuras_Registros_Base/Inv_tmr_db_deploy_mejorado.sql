-- =============================================================================
--  SCRIPT DE DESPLIEGUE UNIFICADO — VERSIÓN MEJORADA
--  Base de datos : Inv_tmr_db
--  Estándar      : Normativo bancario / grandes empresas Ecuador
--  Motor         : PostgreSQL 16+
--  Generado      : 2026-05-05
--  Mejorado      : 2026-05-18  (pipeline 00-orchestrator-agent, TASK-001 a TASK-022)
--  Origen        : isc-inv (PostgreSQL) + isc-tmr (SQL Server)
-- =============================================================================
-- =============================================================================
--\set ON_ERROR_STOP on
-- =============================================================================
-- EJECUCIÓN:
--   1. Conectarse como superusuario:  psql -U postgres
--   2. Ejecutar este script completo: \i Inv_tmr_db_deploy.sql
--   3. Verificar:                     \c Inv_tmr_db
-- =============================================================================
-- CONVENCIONES APLICADAS:
--   Tablas    → tbl_<modulo>_<entidad>          (singular, snake_case)
--   SP        → sp_<accion>_<modulo>_<entidad>
--   Funciones → fn_<modulo>_<accion>_<desc>
--   Triggers  → trg_<tabla>_<evento>
--   FK        → fk_<tabla_origen>_<tabla_destino>
--   PK        → pk_<tabla>
--   Tipos     →  / VARCHAR(n) / TEXT / NUMERIC(p,s) / TIMESTAMPTZ / BOOLEAN / JSONB
-- =============================================================================

-- ---------------------------------------------------------------------------
-- PASO 1 — CREAR LA BASE DE DATOS
-- Ejecutar conectado a 'postgres' (base por defecto)
-- ---------------------------------------------------------------------------
--\c postgres

--DROP DATABASE IF EXISTS "Inv_tmr_db";
--CREATE DATABASE "Inv_tmr_db"
   -- ENCODING    = 'UTF8'
   -- LC_COLLATE  = 'en_US.utf8'
   -- LC_CTYPE    = 'en_US.utf8'
   -- TEMPLATE    = template0;
-- ---------------------------------------------------------------------------
-- PASO 2 — Conectarse a la nueva base y configurar entorno
-- ---------------------------------------------------------------------------
--\c "Inv_tmr_db"

SET client_encoding                 = 'UTF8';
SET standard_conforming_strings     = ON;
SET search_path                     = public;
SET check_function_bodies           = TRUE;
SET client_min_messages             = WARNING;

BEGIN;

-- ---------------------------------------------------------------------------
-- PASO 3 — EXTENSIONES
-- ---------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- ---------------------------------------------------------------------------
-- PASO 4 — ESQUEMAS
-- ---------------------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS administracion;
COMMENT ON SCHEMA administracion IS 'Personal, cargos, clientes, modos de trabajo y datos maestros de RRHH';

CREATE SCHEMA IF NOT EXISTS inventario;
COMMENT ON SCHEMA inventario IS 'Equipos, activos tecnológicos, proveedores y facturas de adquisición';

CREATE SCHEMA IF NOT EXISTS autenticacion;
COMMENT ON SCHEMA autenticacion IS 'Usuarios, roles, permisos, sesiones y aplicaciones del sistema';

CREATE SCHEMA IF NOT EXISTS auditoria;
COMMENT ON SCHEMA auditoria IS 'Registro histórico de cambios en entidades críticas del sistema';

CREATE SCHEMA IF NOT EXISTS time_report;
COMMENT ON SCHEMA time_report IS 'Gestión de proyectos, actividades diarias, permisos y reporte de horas';

-- ---------------------------------------------------------------------------
-- 1. tbl_administracion_catalogo
-- Origen: Tabla nueva. 
-- Catálogo general para clasificaciones varias (tipo, estado, prioridad, etc.)
-- ---------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_catalogo (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    TipoCatalogo        VARCHAR(3)     NOT NULL CHECK (TipoCatalogo IN ('INV','TMR','AUT','AUD','ADM')),
    Codigo              VARCHAR(3)     NOT NULL,
    Descripcion         VARCHAR(255),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_administracion_catalogo PRIMARY KEY (Id),
    CONSTRAINT uq_administracion_catalogo_tipo_codigo UNIQUE (TipoCatalogo, Codigo)
);

-- ----------------------------------------------------------------------------
-- 2. tbl_administracion_catalogo_detalle
-- Origen: Tabla nueva.
-- Detalles de cada catálogo general, con clave-valor para flexibilidad.
-- ----------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_catalogo_detalle (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdCatalogo          INTEGER         NOT NULL,
    CodigoValor         VARCHAR(5)     NOT NULL,
    Valor               VARCHAR(100)    NOT NULL,
    Descripcion         VARCHAR(255),
    Orden               SMALLINT,
    ValorExtra          VARCHAR(100),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_administracion_catalogo_detalle PRIMARY KEY (Id),
    CONSTRAINT fk_administracion_catalogo_detalle_catalogo
        FOREIGN KEY (IdCatalogo)
        REFERENCES administracion.tbl_administracion_catalogo(Id),
    CONSTRAINT uq_administracion_catalogo_detalle_id_codigo UNIQUE (IdCatalogo, CodigoValor)
);

-- ---------------------------------------------------------------------------
-- 3. tbl_administracion_cargo
-- Origen: administration.position (INV) + dbo.Positions (TMR) UNIFICADAS
-- ---------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_cargo (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdDepartamento      INTEGER,
    NombreCargo         VARCHAR(100)    NOT NULL,
    Descripcion         TEXT,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_administracion_cargo PRIMARY KEY (Id),
    CONSTRAINT fk_administracion_cargo_departamento
        FOREIGN KEY (IdDepartamento)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 4. tbl_administracion_persona
-- Origen: dbo.Persons (TMR) — entidad base unificada para empleados y clientes
-- ---------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_persona (
    Id                      INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NumeroIdentificacion    VARCHAR(20)     NOT NULL,
    IdTipoIdentificacion    INTEGER,
    IdGenero                INTEGER,
    IdNacionalidad          INTEGER,
    TipoPersona             VARCHAR(10)     NOT NULL CHECK (TipoPersona IN ('NATURAL','JURIDICA')),
    Nombres                 VARCHAR(100)    NOT NULL,
    Apellidos               VARCHAR(100)    NOT NULL,
    FechaNacimiento         DATE,
    Email                   VARCHAR(100),
    Telefono                VARCHAR(20),
    Direccion               VARCHAR(255),
    Activo                  BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion         VARCHAR(50)     NOT NULL,
    FechaCreacion           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion     VARCHAR(50),
    FechaModificacion       TIMESTAMPTZ,
    IpCreacion              VARCHAR(45)     NOT NULL,
    IpModificacion          VARCHAR(45),
    CONSTRAINT pk_administracion_persona PRIMARY KEY (Id),
    CONSTRAINT fk_administracion_persona_genero
        FOREIGN KEY (IdGenero)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_administracion_persona_nacionalidad
        FOREIGN KEY (IdNacionalidad)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_administracion_persona_tipo_identificacion
        FOREIGN KEY (IdTipoIdentificacion)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 5. tbl_administracion_empleado
-- Origen: administration.employee (INV) + dbo.Employees (TMR) UNIFICADAS
-- ---------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_empleado (
    Id                      INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdPersona               INTEGER         NOT NULL,
    IdCargo                 INTEGER,
    IdModoTrabajo           INTEGER,
    IdCategoriaEmpleado     INTEGER,
    IdEmpresaCatalogo       INTEGER,
    CodigoEmpleado          VARCHAR(20)     NOT NULL,
    FechaIngreso            DATE,
    FechaTerminacion        DATE,
    IdTipoContrato          Integer,
    EmailCorporativo        VARCHAR(100),
    Salario                 NUMERIC(12,2),
    Activo                  BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion         VARCHAR(50)     NOT NULL,
    FechaCreacion           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion     VARCHAR(50),
    FechaModificacion       TIMESTAMPTZ,
    IpCreacion              VARCHAR(45)     NOT NULL,
    IpModificacion          VARCHAR(45),
    CONSTRAINT pk_administracion_empleado PRIMARY KEY (Id),
    CONSTRAINT fk_administracion_empleado_persona
        FOREIGN KEY (IdPersona)
        REFERENCES administracion.tbl_administracion_persona(Id),
    CONSTRAINT fk_administracion_empleado_cargo
        FOREIGN KEY (IdCargo)
        REFERENCES administracion.tbl_administracion_cargo(Id),
    CONSTRAINT fk_administracion_empleado_modo_trabajo
        FOREIGN KEY (IdModoTrabajo)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_administracion_empleado_categoria
        FOREIGN KEY (IdCategoriaEmpleado)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_administracion_empleado_empresa
        FOREIGN KEY (IdEmpresaCatalogo)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_administracion_empleado_contrato
        FOREIGN KEY (IdTipoContrato)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT uq_administracion_empleado_codigo UNIQUE (CodigoEmpleado)
);

-- ---------------------------------------------------------------------------
-- 6. tbl_administracion_cliente
-- Origen: administration.customer (INV) + dbo.Clients (TMR) UNIFICADAS
-- ---------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_cliente (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NumeroIdentificacion    VARCHAR(20)     NOT NULL,
    IdTipoIdentificacion    INTEGER,
    NombreComercial     VARCHAR(100),
    RazonSocial         VARCHAR(150),    
    Email               VARCHAR(100),
    Telefono            VARCHAR(20),
    Direccion           VARCHAR(255),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_administracion_cliente PRIMARY KEY (Id),
    CONSTRAINT fk_administracion_cliente_tipo_identificacion
        FOREIGN KEY (IdTipoIdentificacion)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 7. tbl_administracion_lider
-- Origen: dbo.Leaders (TMR)
-- ---------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_lider (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdPersona           INTEGER         NOT NULL,
    IdTipo              INTEGER,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_administracion_lider PRIMARY KEY (Id),
    CONSTRAINT fk_administracion_lider_persona
        FOREIGN KEY (IdPersona)
        REFERENCES administracion.tbl_administracion_persona(Id),
     CONSTRAINT fk_administracion_lider_tipo
        FOREIGN KEY (IdTipo)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 8. tbl_administracion_apariencia
-- Origen: administration.appearance (INV)
-- ---------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_apariencia (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    FondoLogin          VARCHAR(25)     NOT NULL,
    Tipografia          VARCHAR(25)     NOT NULL,
    EncabezadoFijo      BOOLEAN         NOT NULL,
    PosicionMenu        VARCHAR(25)     NOT NULL,
    MenuColapsado       BOOLEAN         NOT NULL,
    ColorFondo          VARCHAR(25)     NOT NULL,
    BorderCaja          INTEGER         NOT NULL,
    FondoCaja           VARCHAR(25)     NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_administracion_apariencia PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 9. tbl_administracion_registro_asignacion
-- Origen: administration.assignment_record (INV)
-- ---------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_registro_asignacion (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEmpleado          INTEGER,
    FechaRegistro       TIMESTAMPTZ,
    Descripcion         TEXT,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_administracion_registro_asignacion PRIMARY KEY (Id),
    CONSTRAINT fk_administracion_registro_asignacion_empleado
        FOREIGN KEY (IdEmpleado)
        REFERENCES administracion.tbl_administracion_empleado(Id)
);

-- =============================================================================
--      2 — AUTENTICACION
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 10. tbl_autenticacion_aplicacion
-- Origen: authentication.applications (INV)
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_aplicacion (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NombreAplicacion    VARCHAR(100)    NOT NULL,
    Descripcion         VARCHAR(255),
    UrlBase             VARCHAR(255),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_autenticacion_aplicacion PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 11. tbl_autenticacion_modulo
-- Origen: dbo.Modules (TMR)
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_modulo (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NombreModulo        VARCHAR(100)    NOT NULL,
    RutaModulo          VARCHAR(255),
    Icono               VARCHAR(50),
    OrdenVisualizacion  INTEGER,
    IdSubmodulo         INTEGER         NOT NULL DEFAULT 0,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_autenticacion_modulo PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 12. tbl_autenticacion_menu
-- Origen: authentication.menus (INV)
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_menu (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdAplicacion        INTEGER,
    NombreMenu          VARCHAR(100)    NOT NULL,
    RutaMenu            VARCHAR(255),
    Icono               VARCHAR(50),
    OrdenVisualizacion  INTEGER,
    IdMenuPadre         INTEGER,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_autenticacion_menu PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_menu_aplicacion
        FOREIGN KEY (IdAplicacion)
        REFERENCES autenticacion.tbl_autenticacion_aplicacion(Id),
    CONSTRAINT fk_autenticacion_menu_padre
        FOREIGN KEY (IdMenuPadre)
        REFERENCES autenticacion.tbl_autenticacion_menu(Id)
        ON DELETE CASCADE,
    CONSTRAINT chk_menu_no_auto_padre CHECK (Id <> IdMenuPadre)
);

-- ---------------------------------------------------------------------------
-- 13. tbl_autenticacion_usuario
-- Origen: authentication.users (INV) + dbo.Users (TMR) UNIFICADAS
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_usuario (
    Id                      INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdPersona               INTEGER,
    Email                   VARCHAR(100)     NOT NULL,
    HashPassword            VARCHAR(255)    NOT NULL,
    UltimoLogin             TIMESTAMPTZ,
    EstaActivo              BOOLEAN,
    DebeCambiarPassword     BOOLEAN         NOT NULL DEFAULT FALSE,
    Activo                  BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion         VARCHAR(50)     NOT NULL,
    FechaCreacion           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion     VARCHAR(50),
    FechaModificacion       TIMESTAMPTZ,
    IpCreacion              VARCHAR(45)     NOT NULL,
    IpModificacion          VARCHAR(45),
    CONSTRAINT pk_autenticacion_usuario PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_usuario_persona
        FOREIGN KEY (IdPersona)
        REFERENCES administracion.tbl_administracion_persona(Id),
    CONSTRAINT uq_autenticacion_usuario_nombre UNIQUE (Email)
);

-- ---------------------------------------------------------------------------
-- 14. tbl_administracion_cliente_usuario  (se crea aquí porque depende del usuario)
-- Origen: administration.user_customer (INV)
-- ---------------------------------------------------------------------------
CREATE TABLE administracion.tbl_administracion_cliente_usuario (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdCliente           INTEGER         NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_administracion_cliente_usuario PRIMARY KEY (Id),
    CONSTRAINT fk_administracion_cliente_usuario_cliente
        FOREIGN KEY (IdCliente)
        REFERENCES administracion.tbl_administracion_cliente(Id),
    CONSTRAINT fk_administracion_cliente_usuario_usuario
        FOREIGN KEY (IdUsuario)
        REFERENCES autenticacion.tbl_autenticacion_usuario(Id)
);

-- ---------------------------------------------------------------------------
-- 15. tbl_autenticacion_sesion
-- Origen: authentication.sessions (INV) + dbo.UserSessions (TMR) UNIFICADAS
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_sesion (
    Id                  BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    TokenSesion         VARCHAR(1000)   NOT NULL,
    HoraIngreso         TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    HoraSalida          TIMESTAMPTZ,
    DireccionIp         VARCHAR(45),
    AgenteUsuario       VARCHAR(512),
    DispositivoInfo     VARCHAR(255),
    UbicacionInfo       VARCHAR(255),
    EstaActiva          BOOLEAN,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_autenticacion_sesion PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_sesion_usuario
        FOREIGN KEY (IdUsuario)
        REFERENCES autenticacion.tbl_autenticacion_usuario(Id)
);

-- ---------------------------------------------------------------------------
-- 16. tbl_autenticacion_token_blacklist
-- Origen: authentication.token_blacklist (INV)
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_token_blacklist (
    Id                  BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL,
    Token               TEXT            NOT NULL,
    FechaExpiracion     TIMESTAMPTZ,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_autenticacion_token_blacklist PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 17. tbl_autenticacion_password_historial
-- Origen: authentication.historical_passwords (INV)
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_password_historial (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    HashPassword        VARCHAR(255)    NOT NULL,
    FechaCambio         TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    IpCreacion          VARCHAR(45)     NOT NULL,
    CONSTRAINT pk_autenticacion_password_historial PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_password_historial_usuario
        FOREIGN KEY (IdUsuario)
        REFERENCES autenticacion.tbl_autenticacion_usuario(Id)
);

-- ---------------------------------------------------------------------------
-- 18. tbl_autenticacion_pregunta_usuario
-- Origen: authentication.user_questions (INV)
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_pregunta_usuario (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    Pregunta            VARCHAR(255)    NOT NULL,
    Respuesta           VARCHAR(255)    NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_autenticacion_pregunta_usuario PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_pregunta_usuario_usuario
        FOREIGN KEY (IdUsuario)
        REFERENCES autenticacion.tbl_autenticacion_usuario(Id)
);

-- ---------------------------------------------------------------------------
-- 19. tbl_autenticacion_sesion_app
-- Origen: authentication.app_session (INV)
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_sesion_app (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    IdAplicacion        INTEGER         NOT NULL,
    TokenApp            TEXT            NOT NULL,
    FechaExpiracion     TIMESTAMPTZ,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    IpCreacion          VARCHAR(45)     NOT NULL,
    CONSTRAINT pk_autenticacion_sesion_app PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_sesion_app_usuario
        FOREIGN KEY (IdUsuario)
        REFERENCES autenticacion.tbl_autenticacion_usuario(Id),
    CONSTRAINT fk_autenticacion_sesion_app_aplicacion
        FOREIGN KEY (IdAplicacion)
        REFERENCES autenticacion.tbl_autenticacion_aplicacion(Id)
);

-- ---------------------------------------------------------------------------
-- 20. tbl_autenticacion_inventario_token
-- Origen: dbo.InventoryToken (TMR) — token JWT de cross-auth entre apps
-- ---------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_inventario_token (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    Token               VARCHAR(1500),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_autenticacion_inventario_token PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 21. tbl_autenticacion_rol_modulo
-- Tablas relacionales de Rol / Módulo / Menú / Privilegio
-- --------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_rol_modulo (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdRol               INTEGER         NOT NULL,
    IdModulo            INTEGER         NOT NULL,
    PuedeVer            BOOLEAN,
    PuedeCrear          BOOLEAN,
    PuedeEditar         BOOLEAN,
    PuedeEliminar       BOOLEAN,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_autenticacion_rol_modulo PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_rol_modulo_rol
        FOREIGN KEY (IdRol)    REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_autenticacion_rol_modulo_modulo
        FOREIGN KEY (IdModulo) REFERENCES autenticacion.tbl_autenticacion_modulo(Id)
);

-- ---------------------------------------------------------------------------
-- 22. tbl_autenticacion_usuario_modulo
-- Tablas relacionales de Rol / Módulo / Menú / Privilegio
-- --------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_usuario_modulo (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    IdModulo            INTEGER         NOT NULL,
    PuedeVer            BOOLEAN,
    PuedeCrear          BOOLEAN,
    PuedeEditar         BOOLEAN,
    PuedeEliminar       BOOLEAN,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_autenticacion_usuario_modulo PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_usuario_modulo_usuario
        FOREIGN KEY (IdUsuario) REFERENCES autenticacion.tbl_autenticacion_usuario(Id),
    CONSTRAINT fk_autenticacion_usuario_modulo_modulo
        FOREIGN KEY (IdModulo)  REFERENCES autenticacion.tbl_autenticacion_modulo(Id)
);

-- ---------------------------------------------------------------------------
-- 23. tbl_autenticacion_menu_rol
-- Origen: authentication.menu_role (INV) + dbo.MenuRoles (TMR) UNIFICADAS
-- --------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_menu_rol (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdMenu              INTEGER         NOT NULL,
    IdRol               INTEGER         NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    IpCreacion          VARCHAR(45)     NOT NULL,
    CONSTRAINT pk_autenticacion_menu_rol PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_menu_rol_menu
        FOREIGN KEY (IdMenu) REFERENCES autenticacion.tbl_autenticacion_menu(Id),
    CONSTRAINT fk_autenticacion_menu_rol_rol
        FOREIGN KEY (IdRol)  REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 24. tbl_autenticacion_menu_usuario
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_menu_usuario (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdMenu              INTEGER         NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    IpCreacion          VARCHAR(45)     NOT NULL,
    CONSTRAINT pk_autenticacion_menu_usuario PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_menu_usuario_menu
        FOREIGN KEY (IdMenu)    REFERENCES autenticacion.tbl_autenticacion_menu(Id),
    CONSTRAINT fk_autenticacion_menu_usuario_usuario
        FOREIGN KEY (IdUsuario) REFERENCES autenticacion.tbl_autenticacion_usuario(Id)
);

-- ---------------------------------------------------------------------------
-- 25. tbl_autenticacion_privilegio_rol
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_privilegio_rol (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdPrivilegio        INTEGER         NOT NULL,
    IdRol               INTEGER         NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    IpCreacion          VARCHAR(45)     NOT NULL,
    CONSTRAINT pk_autenticacion_privilegio_rol PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_privilegio_rol_privilegio
        FOREIGN KEY (IdPrivilegio) REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_autenticacion_privilegio_rol_rol
        FOREIGN KEY (IdRol)        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 26. tbl_autenticacion_privilegio_usuario
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_privilegio_usuario (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdPrivilegio        INTEGER         NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    IpCreacion          VARCHAR(45)     NOT NULL,
    CONSTRAINT pk_autenticacion_privilegio_usuario PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_privilegio_usuario_privilegio
        FOREIGN KEY (IdPrivilegio) REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_autenticacion_privilegio_usuario_usuario
        FOREIGN KEY (IdUsuario)    REFERENCES autenticacion.tbl_autenticacion_usuario(Id)
);

-- ---------------------------------------------------------------------------
-- 27. tbl_autenticacion_usuario_rol
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_usuario_rol (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    IdRol               INTEGER         NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    IpCreacion          VARCHAR(45)     NOT NULL,
    CONSTRAINT pk_autenticacion_usuario_rol PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_usuario_rol_usuario
        FOREIGN KEY (IdUsuario) REFERENCES autenticacion.tbl_autenticacion_usuario(Id),
    CONSTRAINT fk_autenticacion_usuario_rol_rol
        FOREIGN KEY (IdRol)     REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 28. tbl_autenticacion_usuario_aplicacion
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE autenticacion.tbl_autenticacion_usuario_aplicacion (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    IdAplicacion        INTEGER         NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    IpCreacion          VARCHAR(45)     NOT NULL,
    CONSTRAINT pk_autenticacion_usuario_aplicacion PRIMARY KEY (Id),
    CONSTRAINT fk_autenticacion_usuario_aplicacion_usuario
        FOREIGN KEY (IdUsuario)    REFERENCES autenticacion.tbl_autenticacion_usuario(Id),
    CONSTRAINT fk_autenticacion_usuario_aplicacion_aplicacion
        FOREIGN KEY (IdAplicacion) REFERENCES autenticacion.tbl_autenticacion_aplicacion(Id)
);

-- =============================================================================
--     3 — INVENTARIO
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 29. tbl_inventario_empresa
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_empresa (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NombreEmpresa       VARCHAR(100)    NOT NULL,
    Ruc                 VARCHAR(13),
    Direccion           VARCHAR(255),
    Telefono            VARCHAR(20),
    Email               VARCHAR(100),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_inventario_empresa PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 30. tbl_inventario_proveedor
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_proveedor (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdTipoProveedor     INTEGER,
    NombreProveedor     VARCHAR(150)    NOT NULL,
    Ruc                 VARCHAR(13),
    Email               VARCHAR(100),
    Telefono            VARCHAR(20),
    Direccion           VARCHAR(255),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_inventario_proveedor PRIMARY KEY (Id),
    CONSTRAINT fk_inventario_proveedor_tipo_proveedor
        FOREIGN KEY (IdTipoProveedor)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 31. tbl_inventario_factura
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_factura (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NumeroFactura       VARCHAR(50)     NOT NULL,
    IdProveedor         INTEGER,
    FechaFactura        DATE            NOT NULL,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_inventario_factura PRIMARY KEY (Id),
    CONSTRAINT uq_inventario_factura_numero UNIQUE (NumeroFactura),
    CONSTRAINT fk_inventario_factura_proveedor
        FOREIGN KEY (IdProveedor)
        REFERENCES inventario.tbl_inventario_proveedor(Id)
);

-- ---------------------------------------------------------------------------
-- 32. tbl_inventario_detalle_factura
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_detalle_factura (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdFactura           INTEGER         NOT NULL,
    Descripcion         VARCHAR(255),
    Cantidad            INTEGER         NOT NULL DEFAULT 1,
    PrecioUnitario      NUMERIC(18,2)   NOT NULL DEFAULT 0,
    Subtotal            NUMERIC(18,2)   NOT NULL DEFAULT 0,
    Iva                 NUMERIC(18,2)   NOT NULL DEFAULT 0,
    Total               NUMERIC(18,2)   NOT NULL DEFAULT 0,
    Observacion         VARCHAR(500),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_inventario_detalle_factura PRIMARY KEY (Id),
    CONSTRAINT fk_inventario_detalle_factura_factura
        FOREIGN KEY (IdFactura)
        REFERENCES inventario.tbl_inventario_factura(Id)
);

-- ---------------------------------------------------------------------------
-- 34. tbl_inventario_equipo
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_equipo (
    Id                          INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdCategoria                 INTEGER         NOT NULL,
    IdEstado                    INTEGER         NOT NULL,
    IdCondicion                 INTEGER,
    IdTipoGarantia              INTEGER,
    IdFactura                   INTEGER,
    Marca                       VARCHAR(100)    NOT NULL,
    Modelo                      VARCHAR(100)    NOT NULL,
    NumeroSerie                 VARCHAR(100)    NOT NULL,
    FechaAdquisicion            DATE,
    FechaVencimientoGarantia    DATE,
    ValorAdquisicion            NUMERIC(18,2),
    Descripcion                 VARCHAR(255),
    DatosAdicionales            JSONB,
    Activo                      BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion             VARCHAR(50)     NOT NULL,
    FechaCreacion               TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion         VARCHAR(50),
    FechaModificacion           TIMESTAMPTZ,
    IpCreacion                  VARCHAR(45)     NOT NULL,
    IpModificacion              VARCHAR(45),
    CONSTRAINT pk_inventario_equipo PRIMARY KEY (Id),
    CONSTRAINT uq_inventario_equipo_serie UNIQUE (NumeroSerie),
    CONSTRAINT fk_inventario_equipo_categoria
        FOREIGN KEY (IdCategoria)    REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_inventario_equipo_estado
        FOREIGN KEY (IdEstado)       REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_inventario_equipo_condicion
        FOREIGN KEY (IdCondicion)    REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_inventario_equipo_tipo_garantia
        FOREIGN KEY (IdTipoGarantia) REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_inventario_equipo_factura
        FOREIGN KEY (IdFactura)      REFERENCES inventario.tbl_inventario_factura(Id)
);

-- ---------------------------------------------------------------------------
-- 35. tbl_inventario_equipo
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_caracteristica_equipo (
    Id                      INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEquipo                INTEGER         NOT NULL,
    IdTipoComponente        INTEGER,
    NombreCaracteristica    VARCHAR(100)    NOT NULL,
    ValorCaracteristica     VARCHAR(255)    NOT NULL,
    Activo                  BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion         VARCHAR(50)     NOT NULL,
    FechaCreacion           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion     VARCHAR(50),
    FechaModificacion       TIMESTAMPTZ,
    IpCreacion              VARCHAR(45)     NOT NULL,
    IpModificacion          VARCHAR(45),
    CONSTRAINT pk_inventario_caracteristica_equipo PRIMARY KEY (Id),
    CONSTRAINT fk_inventario_caracteristica_equipo_equipo
        FOREIGN KEY (IdEquipo)         REFERENCES inventario.tbl_inventario_equipo(Id),
    CONSTRAINT fk_inventario_caracteristica_equipo_tipo_componente
        FOREIGN KEY (IdTipoComponente) REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 36. tbl_inventario_asignacion_equipo
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_asignacion_equipo (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEquipo            INTEGER         NOT NULL,
    IdEmpleado          INTEGER         NOT NULL,
    FechaAsignacion     TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    FechaDevolucion     TIMESTAMPTZ,
    Observacion         VARCHAR(500),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_inventario_asignacion_equipo PRIMARY KEY (Id),
    CONSTRAINT fk_inventario_asignacion_equipo_equipo
        FOREIGN KEY (IdEquipo)   REFERENCES inventario.tbl_inventario_equipo(Id),
    CONSTRAINT fk_inventario_asignacion_equipo_empleado
        FOREIGN KEY (IdEmpleado) REFERENCES administracion.tbl_administracion_empleado(Id)
);

-- ---------------------------------------------------------------------------
-- 37. tbl_inventario_reparacion_equipo
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_reparacion_equipo (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEquipo            INTEGER         NOT NULL,
    FechaInicio         DATE            NOT NULL,
    FechaFin            DATE,
    Descripcion         VARCHAR(255),
    CostoReparacion     NUMERIC(18,2),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_inventario_reparacion_equipo PRIMARY KEY (Id),
    CONSTRAINT fk_inventario_reparacion_equipo_equipo
        FOREIGN KEY (IdEquipo) REFERENCES inventario.tbl_inventario_equipo(Id)
);

-- ---------------------------------------------------------------------------
-- 38. tbl_inventario_baja_equipo
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_baja_equipo (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEquipo            INTEGER         NOT NULL,
    IdTipoBaja          INTEGER,
    FechaBaja           DATE            NOT NULL,
    MotivoBaja          VARCHAR(500),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_inventario_baja_equipo PRIMARY KEY (Id),
    CONSTRAINT fk_inventario_baja_equipo_equipo
        FOREIGN KEY (IdEquipo)   REFERENCES inventario.tbl_inventario_equipo(Id),
    CONSTRAINT fk_inventario_baja_equipo_tipo_baja
        FOREIGN KEY (IdTipoBaja) REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- ---------------------------------------------------------------------------
-- 39. tbl_inventario_stock_categoria
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE inventario.tbl_inventario_stock_categoria (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdCategoria         INTEGER         NOT NULL,
    StockMinimo         INTEGER         NOT NULL DEFAULT 0,
    StockMaximo         INTEGER,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_inventario_stock_categoria PRIMARY KEY (Id),
    CONSTRAINT fk_inventario_stock_categoria_categoria
        FOREIGN KEY (IdCategoria) REFERENCES administracion.tbl_administracion_catalogo_detalle(Id)
);

-- =============================================================================
--  4 — TIME_REPORT
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 40. tbl_time_report_tipo_proyecto
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_tipo_proyecto (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NombreTipo          VARCHAR(50)     NOT NULL,
    EsSubtipo           BOOLEAN,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_time_report_tipo_proyecto PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 41. tbl_time_report_proyecto
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_proyecto (
    Id                          INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdCliente                   INTEGER,
    IdEstadoProyecto            INTEGER         NOT NULL,
    IdTipoProyecto              INTEGER,
    IdLider                     INTEGER,
    Codigo                      VARCHAR(50),
    Nombre                      VARCHAR(150)    NOT NULL,
    Descripcion                 VARCHAR(255),
    FechaInicioPlaneada         DATE,
    FechaFinPlaneada            DATE,
    FechaInicioReal             DATE,
    FechaFinReal                DATE,
    FechaInicioEspera           DATE,
    FechaFinEspera              DATE,
    Observacion                 VARCHAR(255),
    Presupuesto                 NUMERIC(15,2),
    HorasAsignadas              NUMERIC(10,2),
    Activo                      BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion             VARCHAR(50)     NOT NULL,
    FechaCreacion               TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion         VARCHAR(50),
    FechaModificacion           TIMESTAMPTZ,
    IpCreacion                  VARCHAR(45)     NOT NULL,
    IpModificacion              VARCHAR(45),
    CONSTRAINT pk_time_report_proyecto PRIMARY KEY (Id),
    CONSTRAINT fk_time_report_proyecto_cliente
        FOREIGN KEY (IdCliente)
        REFERENCES administracion.tbl_administracion_cliente(Id),
    CONSTRAINT fk_time_report_proyecto_estado
        FOREIGN KEY (IdEstadoProyecto)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_time_report_proyecto_tipo
        FOREIGN KEY (IdTipoProyecto)
        REFERENCES time_report.tbl_time_report_tipo_proyecto(Id),
    CONSTRAINT fk_time_report_proyecto_lider
        FOREIGN KEY (IdLider)
        REFERENCES administracion.tbl_administracion_lider(Id)
);

-- ---------------------------------------------------------------------------
-- 42. tbl_time_report_empleado_proyecto
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_empleado_proyecto (
    Id                      INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEmpleado              INTEGER,
    IdProyecto              INTEGER         NOT NULL,
    IdProveedor             INTEGER,
    FechaAsignacion         DATE,
    FechaFinAsignacion      DATE,
    RolAsignado             VARCHAR(100),
    CostoPorHora            NUMERIC(10,2),
    HorasAsignadas          NUMERIC(10,2),
    Activo                  BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion         VARCHAR(50)     NOT NULL,
    FechaCreacion           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion     VARCHAR(50),
    FechaModificacion       TIMESTAMPTZ,
    IpCreacion              VARCHAR(45)     NOT NULL,
    IpModificacion          VARCHAR(45),
    CONSTRAINT pk_time_report_empleado_proyecto PRIMARY KEY (Id),
    CONSTRAINT fk_time_report_empleado_proyecto_empleado
        FOREIGN KEY (IdEmpleado) REFERENCES administracion.tbl_administracion_empleado(Id),
    CONSTRAINT fk_time_report_empleado_proyecto_proyecto
        FOREIGN KEY (IdProyecto) REFERENCES time_report.tbl_time_report_proyecto(Id)
);

-- ---------------------------------------------------------------------------
-- 43. tbl_time_report_tipo_actividad
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_tipo_actividad (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NombreTipo          VARCHAR(100)    NOT NULL,
    Descripcion         VARCHAR(255),
    CodigoColor         VARCHAR(7),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_time_report_tipo_actividad PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 44. tbl_time_report_actividad_diaria
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_actividad_diaria (
    Id                      INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEmpleado              INTEGER         NOT NULL,
    IdProyecto              INTEGER,
    IdTipoActividad         INTEGER         NOT NULL,
    CodigoRequerimiento     VARCHAR(50),
    CantidadHoras           NUMERIC(5,2)    NOT NULL,
    FechaActividad          DATE            NOT NULL,
    DescripcionActividad    VARCHAR(255)            NOT NULL,
    Notas                   TEXT,
    EsBillable              BOOLEAN,
    AprobadoPor             INTEGER,
    FechaAprobacion         TIMESTAMPTZ,
    Activo                  BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion         VARCHAR(50)     NOT NULL,
    FechaCreacion           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion     VARCHAR(50),
    FechaModificacion       TIMESTAMPTZ,
    IpCreacion              VARCHAR(45)     NOT NULL,
    IpModificacion          VARCHAR(45),
    CONSTRAINT pk_time_report_actividad_diaria PRIMARY KEY (Id),
    CONSTRAINT fk_time_report_actividad_diaria_empleado
        FOREIGN KEY (IdEmpleado)     REFERENCES administracion.tbl_administracion_empleado(Id),
    CONSTRAINT fk_time_report_actividad_diaria_proyecto
        FOREIGN KEY (IdProyecto)     REFERENCES time_report.tbl_time_report_proyecto(Id),
    CONSTRAINT fk_time_report_actividad_diaria_tipo
        FOREIGN KEY (IdTipoActividad) REFERENCES time_report.tbl_time_report_tipo_actividad(Id),
    CONSTRAINT fk_time_report_actividad_diaria_aprobador
        FOREIGN KEY (AprobadoPor)    REFERENCES administracion.tbl_administracion_empleado(Id)
);

-- ---------------------------------------------------------------------------
-- 45. tbl_time_report_permiso
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_permiso (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEmpleado          INTEGER         NOT NULL,
    IdTipoPermiso       INTEGER         NOT NULL,
    IdEstadoAprobacion  INTEGER         NOT NULL,
    FechaInicio         DATE            NOT NULL,
    FechaFin            DATE            NOT NULL,
    TotalDias           NUMERIC(6,2)    NOT NULL,
    TotalHoras          NUMERIC(6,2),
    EsPagado            BOOLEAN,
    Descripcion         TEXT,
    AprobadoPor         INTEGER,
    FechaAprobacion     TIMESTAMPTZ,
    Observacion         TEXT,
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_time_report_permiso PRIMARY KEY (Id),
    CONSTRAINT fk_time_report_permiso_empleado
        FOREIGN KEY (IdEmpleado)
        REFERENCES administracion.tbl_administracion_empleado(Id),
    CONSTRAINT fk_time_report_permiso_tipo
        FOREIGN KEY (IdTipoPermiso)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_time_report_permiso_estado_aprobacion
        FOREIGN KEY (IdEstadoAprobacion)
        REFERENCES administracion.tbl_administracion_catalogo_detalle(Id),
    CONSTRAINT fk_time_report_permiso_aprobador
        FOREIGN KEY (AprobadoPor)
        REFERENCES administracion.tbl_administracion_empleado(Id)
);

-- ---------------------------------------------------------------------------
-- 46. tbl_time_report_feriado
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_feriado (
    Id                  INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NombreFeriado       VARCHAR(150)    NOT NULL,
    FechaFeriado        DATE            NOT NULL,
    EsRecurrente        BOOLEAN,
    TipoFeriado         VARCHAR(50),
    Descripcion         VARCHAR(255),
    Activo              BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion     VARCHAR(50)     NOT NULL,
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion VARCHAR(50),
    FechaModificacion   TIMESTAMPTZ,
    IpCreacion          VARCHAR(45)     NOT NULL,
    IpModificacion      VARCHAR(45),
    CONSTRAINT pk_time_report_feriado PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 47. tbl_time_report_proyeccion_horas
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_proyeccion_horas (
    Id                          INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    GrupoProyeccion             UUID,
    IdTipoRecurso               INTEGER         NOT NULL,
    NombreRecurso               VARCHAR(255)    NOT NULL,
    NombreProyeccion            VARCHAR(255)    NOT NULL,
    CostoPorHora                NUMERIC(18,2)   NOT NULL,
    CantidadRecurso             INTEGER         NOT NULL,
    DistribucionTiempo          TEXT            NOT NULL,
    TiempoTotal                 NUMERIC(18,2)   NOT NULL,
    CostoRecurso                NUMERIC(18,2)   NOT NULL,
    PorcentajeParticipacion     NUMERIC(5,2)    NOT NULL,
    TipoPeriodo                 BOOLEAN         NOT NULL,
    CantidadPeriodo             INTEGER         NOT NULL,
    Activo                      BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion             VARCHAR(50)     NOT NULL,
    FechaCreacion               TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion         VARCHAR(50),
    FechaModificacion           TIMESTAMPTZ,
    IpCreacion                  VARCHAR(45)     NOT NULL,
    IpModificacion              VARCHAR(45),
    CONSTRAINT pk_time_report_proyeccion_horas PRIMARY KEY (Id),
    CONSTRAINT fk_time_report_proyeccion_horas_tipo_recurso
        FOREIGN KEY (IdTipoRecurso)
        REFERENCES administracion.tbl_administracion_cargo(Id)
);

-- ---------------------------------------------------------------------------
-- 48. tbl_time_report_proyeccion_horas_proyecto
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_proyeccion_horas_proyecto (
    Id                          INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdProyecto                  INTEGER         NOT NULL,
    IdTipoRecurso               INTEGER         NOT NULL,
    NombreRecurso               VARCHAR(255)    NOT NULL,
    CostoPorHora                NUMERIC(18,2)   NOT NULL,
    CantidadRecurso             INTEGER         NOT NULL,
    DistribucionTiempo          TEXT            NOT NULL,
    TiempoTotal                 NUMERIC(18,2)   NOT NULL,
    CostoRecurso                NUMERIC(18,2)   NOT NULL,
    PorcentajeParticipacion     NUMERIC(5,2)    NOT NULL,
    TipoPeriodo                 BOOLEAN         NOT NULL,
    CantidadPeriodo             INTEGER         NOT NULL,
    Activo                      BOOLEAN         NOT NULL DEFAULT TRUE,
    UsuarioCreacion             VARCHAR(50)     NOT NULL,
    FechaCreacion               TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    UsuarioModificacion         VARCHAR(50),
    FechaModificacion           TIMESTAMPTZ,
    IpCreacion                  VARCHAR(45)     NOT NULL,
    IpModificacion              VARCHAR(45),
    CONSTRAINT pk_time_report_proyeccion_horas_proyecto PRIMARY KEY (Id),
    CONSTRAINT fk_time_report_proyeccion_horas_proyecto_proyecto
        FOREIGN KEY (IdProyecto)
        REFERENCES time_report.tbl_time_report_proyecto(Id),
    CONSTRAINT fk_time_report_proyeccion_horas_proyecto_tipo_recurso
        FOREIGN KEY (IdTipoRecurso)
        REFERENCES administracion.tbl_administracion_cargo(Id)
);

-- ---------------------------------------------------------------------------
-- 49. tbl_time_report_homologacion_banco
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_homologacion_banco (
    Id                      INT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEmpleado              INTEGER         NOT NULL,
    NombreCompletoTR        VARCHAR(200),
    CedulaTR                VARCHAR(20),
    ProyectoTR              VARCHAR(200),
    ClienteTR               VARCHAR(200),
    NombreCompletoBanco     VARCHAR(200),
    Observacion             VARCHAR(100),
    FechaRegistro           TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_time_report_homologacion_banco PRIMARY KEY (Id),
    CONSTRAINT fk_time_report_homologacion_banco_empleado
        FOREIGN KEY (IdEmpleado)
        REFERENCES administracion.tbl_administracion_empleado(Id),
    CONSTRAINT uq_time_report_homologacion_banco
        UNIQUE (IdEmpleado, ProyectoTR)
);

-- ---------------------------------------------------------------------------
-- 50. tbl_time_report_outbox_cargo
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE time_report.tbl_time_report_outbox_cargo (
    IdOutbox        UUID            NOT NULL DEFAULT gen_random_uuid(),
    IdAgregado      INTEGER         NOT NULL,
    Operacion       CHAR(1)         NOT NULL CHECK (Operacion IN ('I','U','D')),
    PayloadJson     JSONB,
    Intentos        SMALLINT        NOT NULL DEFAULT 0,
    ProximoIntento  TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    ProcesadoEn     TIMESTAMPTZ,
    MensajeError    VARCHAR(2000),
    CreadoEn        TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_time_report_outbox_cargo PRIMARY KEY (IdOutbox)
);

-- =============================================================================
--    5 — AUDITORIA
-- =============================================================================

-- ---------------------------------------------------------------------------
-- 51. tbl_auditoria_empleado
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE auditoria.tbl_auditoria_empleado (
    Id                  BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEmpleado          INTEGER         NOT NULL,
    TipoCambio          VARCHAR(10)     NOT NULL CHECK (TipoCambio IN ('INSERT','UPDATE','DELETE')),
    CamposModificados   JSONB,
    ValoresAnteriores   JSONB,
    ValoresNuevos       JSONB,
    CambiadoPor         VARCHAR(50),
    DireccionIp         VARCHAR(45),
    AgenteUsuario       VARCHAR(255),
    UsuarioCreacion     VARCHAR(50),
    IpCreacion          VARCHAR(45),
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_auditoria_empleado PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 52. tbl_auditoria_equipo
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE auditoria.tbl_auditoria_equipo (
    Id                  BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdEquipo            INTEGER         NOT NULL,
    TipoCambio          VARCHAR(10)     NOT NULL CHECK (TipoCambio IN ('INSERT','UPDATE','DELETE')),
    CamposModificados   JSONB,
    ValoresAnteriores   JSONB,
    ValoresNuevos       JSONB,
    CambiadoPor         VARCHAR(50),
    DireccionIp         VARCHAR(45),
    AgenteUsuario       VARCHAR(255),
    UsuarioCreacion     VARCHAR(50),
    IpCreacion          VARCHAR(45),
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_auditoria_equipo PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 53. tbl_auditoria_asignacion_equipo
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE auditoria.tbl_auditoria_asignacion_equipo (
    Id                  BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdAsignacion        INTEGER         NOT NULL,
    TipoCambio          VARCHAR(10)     NOT NULL CHECK (TipoCambio IN ('INSERT','UPDATE','DELETE')),
    CamposModificados   JSONB,
    ValoresAnteriores   JSONB,
    ValoresNuevos       JSONB,
    CambiadoPor         VARCHAR(50),
    DireccionIp         VARCHAR(45),
    AgenteUsuario       VARCHAR(255),
    UsuarioCreacion     VARCHAR(50),
    IpCreacion          VARCHAR(45),
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_auditoria_asignacion_equipo PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 54. tbl_auditoria_sesion_usuario
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE auditoria.tbl_auditoria_sesion_usuario (
    Id                  BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL,
    IdUsuario           INTEGER         NOT NULL,
    TipoCambio          VARCHAR(10)     NOT NULL CHECK (TipoCambio IN ('LOGIN','LOGOUT','FAILED')),
    DireccionIp         VARCHAR(45),
    AgenteUsuario       VARCHAR(255),
    FechaCreacion       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_auditoria_sesion_usuario PRIMARY KEY (Id)
);

-- ---------------------------------------------------------------------------
-- 55. tbl_auditoria_historico_general
-- Origen: 
-- --------------------------------------------------------------------------
CREATE TABLE auditoria.tbl_auditoria_historico_general (
    Id                  BIGINT GENERATED ALWAYS AS IDENTITY NOT NULL,
    NombreTabla         VARCHAR(100)    NOT NULL,
    IdRegistro          VARCHAR(50)     NOT NULL,
    TipoOperacion       VARCHAR(10)     NOT NULL CHECK (TipoOperacion IN ('INSERT','UPDATE','DELETE')),
    DatosAnteriores     JSONB,
    DatosNuevos         JSONB,
    CambiadoPor         VARCHAR(50),
    FechaCambio         TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    DireccionIp         VARCHAR(45),
    CONSTRAINT pk_auditoria_historico_general PRIMARY KEY (Id)
);

-- =============================================================================
--     6 — ÍNDICES
-- =============================================================================

CREATE INDEX idx_inv_equipo_categoria        ON inventario.tbl_inventario_equipo(IdCategoria);
CREATE INDEX idx_inv_equipo_estado           ON inventario.tbl_inventario_equipo(IdEstado);
CREATE INDEX idx_inv_equipo_activo           ON inventario.tbl_inventario_equipo(Activo);
CREATE INDEX idx_inv_asignacion_equipo       ON inventario.tbl_inventario_asignacion_equipo(IdEquipo);
CREATE INDEX idx_inv_asignacion_empleado     ON inventario.tbl_inventario_asignacion_equipo(IdEmpleado);
CREATE INDEX idx_inv_asignacion_devolucion   ON inventario.tbl_inventario_asignacion_equipo(FechaDevolucion);

CREATE INDEX idx_tr_actividad_empleado       ON time_report.tbl_time_report_actividad_diaria(IdEmpleado);
CREATE INDEX idx_tr_actividad_proyecto       ON time_report.tbl_time_report_actividad_diaria(IdProyecto);
CREATE INDEX idx_tr_actividad_fecha          ON time_report.tbl_time_report_actividad_diaria(FechaActividad);

CREATE INDEX idx_adm_empleado_persona        ON administracion.tbl_administracion_empleado(IdPersona);
CREATE INDEX idx_adm_persona_identificacion  ON administracion.tbl_administracion_persona(NumeroIdentificacion);

CREATE INDEX idx_aut_sesion_usuario          ON autenticacion.tbl_autenticacion_sesion(IdUsuario);
CREATE INDEX idx_aut_sesion_activa           ON autenticacion.tbl_autenticacion_sesion(EstaActiva);

CREATE INDEX idx_aud_empleado_id             ON auditoria.tbl_auditoria_empleado(IdEmpleado);
CREATE INDEX idx_aud_empleado_fecha          ON auditoria.tbl_auditoria_empleado(FechaCreacion);
CREATE INDEX idx_aud_sesion_usuario          ON auditoria.tbl_auditoria_sesion_usuario(IdUsuario);
CREATE INDEX idx_aud_historico_tabla         ON auditoria.tbl_auditoria_historico_general(NombreTabla);
CREATE INDEX idx_aud_historico_fecha         ON auditoria.tbl_auditoria_historico_general(FechaCambio);

-- TASK-008: Índices en FK de time_report (prevención de seqscan y deadlocks)
CREATE INDEX idx_tr_proyecto_cliente         ON time_report.tbl_time_report_proyecto(IdCliente);
CREATE INDEX idx_tr_proyecto_lider           ON time_report.tbl_time_report_proyecto(IdLider);
CREATE INDEX idx_tr_proyecto_tipo            ON time_report.tbl_time_report_proyecto(IdTipoProyecto);
CREATE INDEX idx_tr_permiso_empleado         ON time_report.tbl_time_report_permiso(IdEmpleado);
CREATE INDEX idx_tr_permiso_estado           ON time_report.tbl_time_report_permiso(IdEstadoAprobacion);
CREATE INDEX idx_tr_empleado_proyecto_emp    ON time_report.tbl_time_report_empleado_proyecto(IdEmpleado);

-- TASK-009: Índices en FK de inventario
CREATE INDEX idx_inv_reparacion_equipo       ON inventario.tbl_inventario_reparacion_equipo(IdEquipo);
CREATE INDEX idx_inv_baja_equipo             ON inventario.tbl_inventario_baja_equipo(IdEquipo);
CREATE INDEX idx_inv_baja_tipo               ON inventario.tbl_inventario_baja_equipo(IdTipoBaja);
CREATE INDEX idx_inv_factura_proveedor       ON inventario.tbl_inventario_factura(IdProveedor);
CREATE INDEX idx_inv_detalle_factura         ON inventario.tbl_inventario_detalle_factura(IdFactura);

-- TASK-010: Índice en UUID GrupoProyeccion (función fn_time_report_obtener_proyeccion_por_grupo)
CREATE INDEX idx_tr_proyeccion_grupo         ON time_report.tbl_time_report_proyeccion_horas(GrupoProyeccion);

-- TASK-011: Índice en token blacklist — CORREGIDO iter-1 (NOW() no es IMMUTABLE en predicados)
-- Índice simple cubre todas las búsquedas de validación; la lógica de expiración queda en la app
CREATE INDEX idx_aut_token_blacklist_token   ON autenticacion.tbl_autenticacion_token_blacklist(Token);

-- TASK-015: Índice compuesto para worker del patrón Outbox
CREATE INDEX idx_tr_outbox_procesamiento     ON time_report.tbl_time_report_outbox_cargo(ProximoIntento, ProcesadoEn)
    WHERE ProcesadoEn IS NULL;

-- =============================================================================
--     7 — FUNCIONES
-- =============================================================================

-- ---------------------------------------------------------------------------
-- fn_inventario_obtener_tendencia_adquisicion
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION inventario.fn_inventario_obtener_tendencia_adquisicion(p_anio INTEGER)
RETURNS TABLE(numero_mes INTEGER, nombre_mes VARCHAR(10),
              conteo_adquisiciones BIGINT, valor_total_adquisiciones NUMERIC)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    WITH meses AS (
        SELECT 1 AS num,'Ene'::VARCHAR(10) AS nom UNION ALL SELECT 2,'Feb' UNION ALL
        SELECT 3,'Mar' UNION ALL SELECT 4,'Abr' UNION ALL SELECT 5,'May' UNION ALL
        SELECT 6,'Jun' UNION ALL SELECT 7,'Jul' UNION ALL SELECT 8,'Ago' UNION ALL
        SELECT 9,'Sep' UNION ALL SELECT 10,'Oct' UNION ALL SELECT 11,'Nov' UNION ALL SELECT 12,'Dic'
    ),
    adq AS (
        SELECT EXTRACT(MONTH FROM f.FechaFactura)::INTEGER AS mes,
               COUNT(DISTINCT f.Id)                        AS conteo,
               COALESCE(SUM(df.Total), 0)                  AS total
        FROM inventario.tbl_inventario_factura f
        JOIN inventario.tbl_inventario_detalle_factura df ON df.IdFactura = f.Id
        WHERE EXTRACT(YEAR FROM f.FechaFactura) = p_anio
        GROUP BY mes
    )
    SELECT m.num, m.nom,
           COALESCE(a.conteo,0)::BIGINT,
           COALESCE(a.total,0)::NUMERIC
    FROM meses m LEFT JOIN adq a ON m.num = a.mes
    ORDER BY m.num;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_inventario_obtener_totales_dashboard
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION inventario.fn_inventario_obtener_totales_dashboard()
RETURNS TABLE(total_equipos BIGINT, equipos_asignados BIGINT, total_clientes BIGINT,
              equipos_reparacion BIGINT, equipos_disponibles BIGINT, equipos_baja BIGINT)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    WITH ce AS (
        SELECT COUNT(*) FILTER (WHERE Activo = TRUE)  AS total_activo,
               COUNT(*) FILTER (WHERE Activo = FALSE) AS total_inactivo,
               COUNT(*) FILTER (
                   WHERE IdEstado = (SELECT Id FROM administracion.tbl_administracion_catalogo_detalle WHERE Valor='Disponible')
                   AND Activo = TRUE
               ) AS total_disponible
        FROM inventario.tbl_inventario_equipo
    ),
    asig AS (SELECT COUNT(*) AS total FROM inventario.tbl_inventario_asignacion_equipo ea
             JOIN inventario.tbl_inventario_equipo e ON ea.IdEquipo=e.Id
             WHERE ea.FechaDevolucion IS NULL AND e.Activo=TRUE AND ea.Activo=TRUE),
    rep  AS (SELECT COUNT(*) AS total FROM inventario.tbl_inventario_equipo
             WHERE IdEstado IN (SELECT Id FROM administracion.tbl_administracion_catalogo_detalle
                                WHERE Valor IN ('En reparacion','En revision'))
             AND Activo=TRUE),
    cli  AS (SELECT COUNT(*) AS total FROM administracion.tbl_administracion_cliente WHERE Activo=TRUE)
    SELECT ce.total_activo, asig.total, cli.total, rep.total, ce.total_disponible, ce.total_inactivo
    FROM ce, asig, rep, cli;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_inventario_obtener_equipos_por_categoria
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION inventario.fn_inventario_obtener_equipos_por_categoria()
RETURNS TABLE(categoria VARCHAR, equipos_asignados BIGINT)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT ce.Valor::VARCHAR,
           COUNT(DISTINCT e.Id) FILTER (WHERE ea.FechaDevolucion IS NULL AND ea.Activo=TRUE AND e.Activo=TRUE)
    FROM administracion.tbl_administracion_catalogo_detalle ce --tbl_inventario_categoria_equipo
    LEFT JOIN inventario.tbl_inventario_equipo e ON ce.Id=e.IdCategoria
    LEFT JOIN inventario.tbl_inventario_asignacion_equipo ea ON e.Id=ea.IdEquipo
    GROUP BY ce.Valor ORDER BY 2 DESC;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_inventario_obtener_resumen_estado_por_categoria
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION inventario.fn_inventario_obtener_resumen_estado_por_categoria(p_id_categoria INTEGER)
RETURNS TABLE(nombre_estado VARCHAR, conteo_equipos BIGINT)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT ee.Valor::VARCHAR, COUNT(e.Id)
    FROM administracion.tbl_administracion_catalogo_detalle ee
    LEFT JOIN inventario.tbl_inventario_equipo e
           ON ee.Id=e.IdEstado AND e.Activo=TRUE AND e.IdCategoria=p_id_categoria
    GROUP BY ee.Valor ORDER BY 2 DESC;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_inventario_obtener_equipos_inactivos
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION inventario.fn_inventario_obtener_equipos_inactivos()
RETURNS TABLE(id INTEGER, marca VARCHAR, modelo VARCHAR, numero_serie VARCHAR,
              nombre_categoria VARCHAR, nombre_estado VARCHAR,
              fecha_ultima_asignacion TIMESTAMPTZ, ultimo_asignado TEXT)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT e.Id, e.Marca, e.Modelo, e.NumeroSerie,
           ce.Valor, ee.Valor AS NombreEstado,
           ua.FechaAsignacion,
           (p.Nombres||' '||p.Apellidos)::TEXT
    FROM inventario.tbl_inventario_equipo e
    JOIN administracion.tbl_administracion_catalogo_detalle ce ON e.IdCategoria=ce.Id
    JOIN administracion.tbl_administracion_catalogo_detalle ee ON e.IdEstado=ee.Id
    LEFT JOIN LATERAL (
        SELECT ea.IdEmpleado, ea.FechaAsignacion
        FROM inventario.tbl_inventario_asignacion_equipo ea
        WHERE ea.IdEquipo=e.Id ORDER BY ea.FechaAsignacion DESC LIMIT 1
    ) ua ON TRUE
    LEFT JOIN administracion.tbl_administracion_empleado emp ON ua.IdEmpleado=emp.Id
    LEFT JOIN administracion.tbl_administracion_persona  p   ON emp.IdPersona=p.Id
    WHERE e.Activo=FALSE ORDER BY e.FechaModificacion DESC;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_obtener_dashboard_general
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_obtener_dashboard_general()
RETURNS TABLE(total_proyectos_activos BIGINT, total_empleados_activos BIGINT,
              total_clientes_activos BIGINT, horas_registradas_mes NUMERIC,
              proyectos_en_curso BIGINT)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT
        (SELECT COUNT(*) FROM time_report.tbl_time_report_proyecto WHERE Activo=TRUE),
        (SELECT COUNT(*) FROM administracion.tbl_administracion_empleado WHERE Activo=TRUE),
        (SELECT COUNT(*) FROM administracion.tbl_administracion_cliente WHERE Activo=TRUE),
        (SELECT COALESCE(SUM(CantidadHoras),0)
         FROM time_report.tbl_time_report_actividad_diaria
         WHERE EXTRACT(MONTH FROM FechaActividad)=EXTRACT(MONTH FROM NOW())
           AND EXTRACT(YEAR  FROM FechaActividad)=EXTRACT(YEAR  FROM NOW())
           AND Activo=TRUE),
        (SELECT COUNT(*) FROM time_report.tbl_time_report_proyecto p
         JOIN administracion.tbl_administracion_catalogo_detalle ep ON p.IdEstadoProyecto=ep.Id --estado proyecto
         WHERE ep.CodigoValor='EN_CURSO' AND p.Activo=TRUE);
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_obtener_recursos_por_cliente
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_obtener_recursos_por_cliente()
RETURNS TABLE(cliente VARCHAR, mes_numero INTEGER, anio INTEGER,
              cantidad_recursos BIGINT, total_horas NUMERIC)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT COALESCE(c.NombreComercial,c.RazonSocial)::VARCHAR,
           EXTRACT(MONTH FROM da.FechaActividad)::INTEGER,
           EXTRACT(YEAR  FROM da.FechaActividad)::INTEGER,
           COUNT(DISTINCT e.Id), SUM(da.CantidadHoras)
    FROM time_report.tbl_time_report_actividad_diaria da
    JOIN administracion.tbl_administracion_empleado  e  ON da.IdEmpleado=e.Id
    JOIN time_report.tbl_time_report_proyecto        p  ON da.IdProyecto=p.Id
    JOIN administracion.tbl_administracion_cliente   c  ON p.IdCliente=c.Id
    WHERE p.Activo=TRUE AND c.Activo=TRUE
    GROUP BY COALESCE(c.NombreComercial,c.RazonSocial),
             EXTRACT(MONTH FROM da.FechaActividad), EXTRACT(YEAR FROM da.FechaActividad)
    ORDER BY 3 DESC, 2 DESC;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_obtener_resumen_proyectos
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_obtener_resumen_proyectos()
RETURNS TABLE(nombre_cliente VARCHAR, nombre_proyecto VARCHAR, estado_proyecto VARCHAR,
              lider_proyecto TEXT, total_recursos BIGINT,
              horas_registradas NUMERIC, presupuesto NUMERIC)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT COALESCE(c.NombreComercial,c.RazonSocial)::VARCHAR, p.Nombre::VARCHAR,
           ep.Valor::VARCHAR,
           (pers.Nombres||' '||pers.Apellidos)::TEXT,
           COUNT(DISTINCT ep2.IdEmpleado),
           COALESCE(SUM(da.CantidadHoras),0), p.Presupuesto
    FROM time_report.tbl_time_report_proyecto p
    LEFT JOIN administracion.tbl_administracion_cliente     c   ON p.IdCliente=c.Id
    LEFT JOIN administracion.tbl_administracion_catalogo_detalle   ep  ON p.IdEstadoProyecto=ep.Id -- Estado proyecto catalogo detalle
    LEFT JOIN administracion.tbl_administracion_lider       l   ON p.IdLider=l.Id
    LEFT JOIN time_report.tbl_time_report_empleado_proyecto ep2 ON p.Id=ep2.IdProyecto AND ep2.Activo=TRUE
    LEFT JOIN time_report.tbl_time_report_actividad_diaria  da  ON p.Id=da.IdProyecto  AND da.Activo=TRUE
    LEFT JOIN administracion.tbl_administracion_empleado        e    ON ep2.IdEmpleado=e.Id  AND e.Activo=TRUE
    LEFT JOIN administracion.tbl_administracion_persona         pers ON e.IdPersona=pers.Id
    
    WHERE p.Activo=TRUE
    GROUP BY COALESCE(c.NombreComercial,c.RazonSocial), p.Nombre,
             ep.Valor, pers.Nombres, pers.Apellidos, p.Presupuesto
    ORDER BY 1,2;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_quitar_tildes  (auxiliar para homologación)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_quitar_tildes(p_texto TEXT)
RETURNS TEXT LANGUAGE plpgsql IMMUTABLE AS $$
BEGIN
    RETURN translate(lower(p_texto),
        'áéíóúàèìòùäëïöüâêîôûãõñç',
        'aeiouaeiouaeiouaeiouaonc');
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_obtener_proyeccion_por_grupo
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_obtener_proyeccion_por_grupo(p_id_grupo UUID)
RETURNS TABLE(id_proyeccion INTEGER, grupo_proyeccion UUID, id_tipo_recurso INTEGER,
              nombre_tipo_recurso VARCHAR, nombre_recurso VARCHAR, nombre_proyeccion VARCHAR,
              costo_por_hora NUMERIC, cantidad_recurso INTEGER, distribucion_tiempo TEXT,
              tiempo_total NUMERIC, costo_recurso NUMERIC,
              porcentaje_participacion NUMERIC, tipo_periodo BOOLEAN, cantidad_periodo INTEGER)
LANGUAGE plpgsql AS $$
BEGIN
    IF p_id_grupo IS NULL THEN
        RAISE EXCEPTION 'El parámetro p_id_grupo no puede ser NULL.';
    END IF;
    RETURN QUERY
    SELECT ph.Id, ph.GrupoProyeccion, ph.IdTipoRecurso,
           c.NombreCargo::VARCHAR, ph.NombreRecurso, ph.NombreProyeccion,
           ph.CostoPorHora, ph.CantidadRecurso, ph.DistribucionTiempo,
           ph.TiempoTotal, ph.CostoRecurso, ph.PorcentajeParticipacion,
           ph.TipoPeriodo, ph.CantidadPeriodo
    FROM time_report.tbl_time_report_proyeccion_horas ph
    JOIN administracion.tbl_administracion_cargo c ON ph.IdTipoRecurso=c.Id
    WHERE ph.GrupoProyeccion=p_id_grupo ORDER BY c.NombreCargo;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_horas_por_tipo_actividad
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_horas_por_tipo_actividad(p_fecha DATE DEFAULT CURRENT_DATE)
RETURNS TABLE(tipo_actividad VARCHAR, total_horas NUMERIC)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT ta.NombreTipo::VARCHAR, SUM(da.CantidadHoras)
    FROM time_report.tbl_time_report_actividad_diaria da
    JOIN time_report.tbl_time_report_tipo_actividad ta ON da.IdTipoActividad=ta.Id
    WHERE da.Activo=TRUE AND da.FechaActividad=p_fecha
    GROUP BY ta.NombreTipo ORDER BY ta.NombreTipo;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_obtener_proyeccion_por_proyecto
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_obtener_proyeccion_por_proyecto(p_id_proyecto INTEGER)
RETURNS TABLE(id_proyeccion INTEGER, id_tipo_recurso INTEGER, nombre_tipo_recurso VARCHAR,
              nombre_recurso VARCHAR, costo_por_hora NUMERIC, cantidad_recurso INTEGER,
              cantidad_periodo INTEGER, distribucion_tiempo TEXT, tiempo_total NUMERIC,
              costo_recurso NUMERIC, porcentaje_participacion NUMERIC,
              tipo_periodo BOOLEAN, id_proyecto INTEGER)
LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM time_report.tbl_time_report_proyecto WHERE Id=p_id_proyecto AND Activo=TRUE) THEN
        RAISE EXCEPTION 'El proyecto % no existe o no está activo.', p_id_proyecto;
    END IF;
    RETURN QUERY
    SELECT php.Id, php.IdTipoRecurso, c.NombreCargo::VARCHAR, php.NombreRecurso,
           php.CostoPorHora, php.CantidadRecurso, php.CantidadPeriodo,
           php.DistribucionTiempo, php.TiempoTotal,
           CAST(php.TiempoTotal*php.CostoPorHora AS NUMERIC(10,2)),
           php.PorcentajeParticipacion, php.TipoPeriodo, php.IdProyecto
    FROM time_report.tbl_time_report_proyeccion_horas_proyecto php
    LEFT JOIN administracion.tbl_administracion_cargo c ON php.IdTipoRecurso=c.Id
    WHERE php.IdProyecto=p_id_proyecto AND php.Activo=TRUE
    ORDER BY php.CostoRecurso DESC, php.NombreRecurso;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_recursos_pendientes_reporte
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_recursos_pendientes_reporte(
    p_mes INTEGER DEFAULT NULL, p_anio INTEGER DEFAULT NULL, p_mes_completo BOOLEAN DEFAULT FALSE)
RETURNS TABLE(nombre_empleado TEXT, cedula VARCHAR, dias_habiles BIGINT,
              dias_registrados BIGINT, horas_registradas NUMERIC, estado_reporte TEXT)
LANGUAGE plpgsql AS $$
DECLARE
    v_mes    INTEGER := COALESCE(p_mes,  EXTRACT(MONTH FROM NOW())::INTEGER);
    v_anio   INTEGER := COALESCE(p_anio, EXTRACT(YEAR  FROM NOW())::INTEGER);
    v_inicio DATE;
    v_fin    DATE;
BEGIN
    IF v_mes<1 OR v_mes>12 OR v_anio<2000 OR v_anio>2100 THEN
        RAISE EXCEPTION 'Parámetros inválidos. Mes 1-12, Año 2000-2100.';
    END IF;
    v_inicio := MAKE_DATE(v_anio, v_mes, 1);
    v_fin    := CASE WHEN p_mes_completo
                     THEN (DATE_TRUNC('MONTH',v_inicio)+INTERVAL '1 MONTH - 1 day')::DATE
                     ELSE MAKE_DATE(v_anio,v_mes,15) END;
    RETURN QUERY
    WITH habiles AS (
        SELECT COUNT(*) AS total
        FROM generate_series(v_inicio, v_fin, INTERVAL '1 day') d(fecha)
        WHERE EXTRACT(DOW FROM d.fecha) NOT IN (0,6)
          AND NOT EXISTS (SELECT 1 FROM time_report.tbl_time_report_feriado f
                          WHERE f.FechaFeriado=d.fecha::DATE AND f.Activo=TRUE)
    ),
    resumen AS (
        SELECT e.Id,
               (p.Nombres||' '||p.Apellidos)::TEXT AS nombre,
               p.NumeroIdentificacion,
               COUNT(DISTINCT da.FechaActividad)              AS dias_reg,
               COALESCE(SUM(da.CantidadHoras),0)              AS horas_reg
        FROM administracion.tbl_administracion_empleado e
        JOIN administracion.tbl_administracion_persona  p ON e.IdPersona=p.Id
        LEFT JOIN time_report.tbl_time_report_actividad_diaria da
               ON da.IdEmpleado=e.Id AND da.FechaActividad BETWEEN v_inicio AND v_fin AND da.Activo=TRUE
        WHERE e.Activo=TRUE
        GROUP BY e.Id, p.Nombres, p.Apellidos, p.NumeroIdentificacion
    )
    SELECT r.nombre, r.NumeroIdentificacion, h.total, r.dias_reg, r.horas_reg,
           CASE WHEN r.dias_reg>=h.total THEN 'COMPLETO'
                WHEN r.dias_reg>0        THEN 'PARCIAL'
                ELSE 'PENDIENTE' END::TEXT
    FROM resumen r, habiles h ORDER BY 6 DESC, r.nombre;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_reporte_recursos_por_proyecto
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_reporte_recursos_por_proyecto()
RETURNS TABLE(nombre_cliente VARCHAR, fecha_inicio DATE,
              lider_proyecto TEXT, nombre_recurso TEXT, cargo VARCHAR)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    SELECT COALESCE(c.NombreComercial,c.RazonSocial)::VARCHAR,
           p.FechaInicioPlaneada,
           (pers.Nombres||' '||pers.Apellidos)::TEXT,
           (pers.Nombres||' '||pers.Apellidos)::TEXT,
           car.NombreCargo::VARCHAR
    FROM time_report.tbl_time_report_proyecto p
    JOIN administracion.tbl_administracion_cliente         c    ON p.IdCliente=c.Id
    JOIN administracion.tbl_administracion_lider           l    ON p.IdLider=l.Id
    JOIN time_report.tbl_time_report_empleado_proyecto     ep   ON ep.IdProyecto=p.Id  AND ep.Activo=TRUE
    JOIN administracion.tbl_administracion_empleado        e    ON ep.IdEmpleado=e.Id  AND e.Activo=TRUE
    JOIN administracion.tbl_administracion_cargo           car  ON e.IdCargo=car.Id
    JOIN administracion.tbl_administracion_persona         pers ON e.IdPersona=pers.Id
    WHERE p.Activo=TRUE ORDER BY pers.Nombres, pers.Apellidos, p.Nombre;
END; $$;

-- ---------------------------------------------------------------------------
-- fn_time_report_resumen_clientes_horas
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION time_report.fn_time_report_resumen_clientes_horas()
RETURNS TABLE(cliente VARCHAR, mes_numero INTEGER, anio INTEGER,
              total_recursos BIGINT, total_horas NUMERIC)
LANGUAGE plpgsql AS $$
BEGIN
    RETURN QUERY
    WITH act AS (
        SELECT da.IdProyecto, da.IdEmpleado, da.CantidadHoras,
               EXTRACT(YEAR  FROM da.FechaActividad)::INTEGER AS anio,
               EXTRACT(MONTH FROM da.FechaActividad)::INTEGER AS mes
        FROM time_report.tbl_time_report_actividad_diaria da WHERE da.Activo=TRUE
    ),
    cli_base AS (
        SELECT c.Id, COALESCE(c.NombreComercial,c.RazonSocial)::VARCHAR AS nombre,
               COUNT(DISTINCT ep.IdEmpleado) AS recursos
        FROM administracion.tbl_administracion_cliente c
        JOIN time_report.tbl_time_report_proyecto p ON c.Id=p.IdCliente AND p.Activo=TRUE
        JOIN time_report.tbl_time_report_empleado_proyecto ep ON p.Id=ep.IdProyecto AND ep.Activo=TRUE
        WHERE c.Activo=TRUE GROUP BY c.Id, COALESCE(c.NombreComercial,c.RazonSocial)
    )
    SELECT cb.nombre, per.mes, per.anio, cb.recursos,
           COALESCE(SUM(a.CantidadHoras),0)
    FROM cli_base cb
    CROSS JOIN (SELECT DISTINCT anio, mes FROM act) per(anio,mes)
    LEFT JOIN act a ON a.anio=per.anio AND a.mes=per.mes
    LEFT JOIN time_report.tbl_time_report_proyecto p ON a.IdProyecto=p.Id AND p.IdCliente=cb.Id
    GROUP BY cb.nombre, per.mes, per.anio, cb.recursos
    ORDER BY per.anio DESC, per.mes DESC, cb.nombre;
END; $$;

-- =============================================================================
--     8 — STORED PROCEDURES
-- =============================================================================

-- ---------------------------------------------------------------------------
-- sp_ins_time_report_homologacion_banco
-- ---------------------------------------------------------------------------
-- TASK-012: SP refactorizado con UPSERT para evitar pérdida de datos en fallo parcial
CREATE OR REPLACE PROCEDURE time_report.sp_ins_time_report_homologacion_banco()
LANGUAGE plpgsql AS $$
DECLARE
    v_count INTEGER;
BEGIN
    -- Insertar/actualizar sin truncar primero — preserva NombreCompletoBanco ya homologado
    INSERT INTO time_report.tbl_time_report_homologacion_banco
        (IdEmpleado, NombreCompletoTR, CedulaTR, ProyectoTR, ClienteTR, NombreCompletoBanco, Observacion)
    SELECT DISTINCT e.Id,
           (pers.Nombres||' '||pers.Apellidos),
           pers.NumeroIdentificacion, p.Nombre,
           COALESCE(c.NombreComercial,c.RazonSocial),
           NULL::VARCHAR(200), 'Pendiente homologación manual'
    FROM administracion.tbl_administracion_empleado e
    JOIN administracion.tbl_administracion_persona         pers ON e.IdPersona=pers.Id
    JOIN time_report.tbl_time_report_empleado_proyecto     ep   ON ep.IdEmpleado=e.Id AND ep.Activo=TRUE
    JOIN time_report.tbl_time_report_proyecto              p    ON ep.IdProyecto=p.Id AND p.Activo=TRUE
    LEFT JOIN administracion.tbl_administracion_cliente    c    ON p.IdCliente=c.Id
    WHERE e.Activo=TRUE
    ON CONFLICT (IdEmpleado, ProyectoTR) DO UPDATE SET
        NombreCompletoTR = EXCLUDED.NombreCompletoTR,
        CedulaTR         = EXCLUDED.CedulaTR,
        ClienteTR        = EXCLUDED.ClienteTR;
    GET DIAGNOSTICS v_count = ROW_COUNT;
    RAISE NOTICE 'sp_ins_time_report_homologacion_banco: % registros procesados.', v_count;
EXCEPTION WHEN OTHERS THEN
    RAISE EXCEPTION 'Error en sp_ins_time_report_homologacion_banco: % — %', SQLSTATE, SQLERRM;
END; $$;

-- =============================================================================
-- ██████╗     9 — TRIGGERS
-- =============================================================================

-- Funciones de trigger — empleado
CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_registrar_empleado_insert()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_empleado(IdEmpleado,TipoCambio,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'INSERT',row_to_json(NEW)::JSONB,NEW.UsuarioCreacion);
    RETURN NEW;
END; $$;

CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_registrar_empleado_update()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_empleado(IdEmpleado,TipoCambio,ValoresAnteriores,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'UPDATE',row_to_json(OLD)::JSONB,row_to_json(NEW)::JSONB,NEW.UsuarioModificacion);
    RETURN NEW;
END; $$;

CREATE TRIGGER trg_tbl_administracion_empleado_ins
AFTER INSERT ON administracion.tbl_administracion_empleado
FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_empleado_insert();

CREATE TRIGGER trg_tbl_administracion_empleado_upd
AFTER UPDATE ON administracion.tbl_administracion_empleado
FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_empleado_update();

-- Funciones de trigger — equipo
CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_registrar_equipo_insert()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_equipo(IdEquipo,TipoCambio,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'INSERT',row_to_json(NEW)::JSONB,NEW.UsuarioCreacion);
    RETURN NEW;
END; $$;

CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_registrar_equipo_update()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_equipo(IdEquipo,TipoCambio,ValoresAnteriores,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'UPDATE',row_to_json(OLD)::JSONB,row_to_json(NEW)::JSONB,NEW.UsuarioModificacion);
    RETURN NEW;
END; $$;

CREATE TRIGGER trg_tbl_inventario_equipo_ins
AFTER INSERT ON inventario.tbl_inventario_equipo
FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_equipo_insert();

CREATE TRIGGER trg_tbl_inventario_equipo_upd
AFTER UPDATE ON inventario.tbl_inventario_equipo
FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_equipo_update();

-- Funciones de trigger — asignación equipo
CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_registrar_asignacion_equipo_insert()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_asignacion_equipo(IdAsignacion,TipoCambio,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'INSERT',row_to_json(NEW)::JSONB,NEW.UsuarioCreacion);
    RETURN NEW;
END; $$;

CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_registrar_asignacion_equipo_update()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_asignacion_equipo(IdAsignacion,TipoCambio,ValoresAnteriores,ValoresNuevos,CambiadoPor)
    VALUES(NEW.Id,'UPDATE',row_to_json(OLD)::JSONB,row_to_json(NEW)::JSONB,NEW.UsuarioModificacion);
    RETURN NEW;
END; $$;

CREATE TRIGGER trg_tbl_inventario_asignacion_equipo_ins
AFTER INSERT ON inventario.tbl_inventario_asignacion_equipo
FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_asignacion_equipo_insert();

CREATE TRIGGER trg_tbl_inventario_asignacion_equipo_upd
AFTER UPDATE ON inventario.tbl_inventario_asignacion_equipo
FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_asignacion_equipo_update();

-- Funciones de trigger — sesión (login / logout)
CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_registrar_login()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    INSERT INTO auditoria.tbl_auditoria_sesion_usuario(IdUsuario,TipoCambio,DireccionIp)
    VALUES(NEW.IdUsuario,'LOGIN',NEW.IpCreacion);
    RETURN NEW;
END; $$;

CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_registrar_logout()
RETURNS TRIGGER LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.HoraSalida IS NOT NULL AND OLD.HoraSalida IS NULL THEN
        INSERT INTO auditoria.tbl_auditoria_sesion_usuario(IdUsuario,TipoCambio,DireccionIp)
        VALUES(NEW.IdUsuario,'LOGOUT',NEW.DireccionIp);
    END IF;
    RETURN NEW;
END; $$;

CREATE TRIGGER trg_tbl_autenticacion_sesion_ins
AFTER INSERT ON autenticacion.tbl_autenticacion_sesion
FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_login();

CREATE TRIGGER trg_tbl_autenticacion_sesion_upd
AFTER UPDATE ON autenticacion.tbl_autenticacion_sesion
FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_logout();

-- =============================================================================
--    TASK-017 — TRIGGERS DE AUDITORÍA ADICIONALES (cobertura normativa)
-- =============================================================================

-- Función genérica de auditoría histórica (alimenta tbl_auditoria_historico_general)
-- CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_registrar_historico()
-- RETURNS TRIGGER LANGUAGE plpgsql AS $$
-- BEGIN
--     INSERT INTO auditoria.tbl_auditoria_historico_general(
--         NombreTabla, TipoCambio, ValoresAnteriores, ValoresNuevos, CambiadoPor, FechaCambio
--     )
--     VALUES(
--         TG_TABLE_SCHEMA || '.' || TG_TABLE_NAME,
--         TG_OP,
--         CASE WHEN TG_OP = 'INSERT' THEN NULL ELSE row_to_json(OLD)::JSONB END,
--         CASE WHEN TG_OP = 'DELETE' THEN NULL ELSE row_to_json(NEW)::JSONB END,
--         CASE WHEN TG_OP = 'DELETE' THEN OLD.UsuarioModificacion
--              ELSE NEW.UsuarioModificacion END,
--         NOW()
--     );
--     RETURN COALESCE(NEW, OLD);
-- END; $$;

-- Auditoría de proyectos
-- CREATE TRIGGER trg_tbl_time_report_proyecto_aud
-- AFTER INSERT OR UPDATE OR DELETE ON time_report.tbl_time_report_proyecto
-- FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_historico();

-- -- Auditoría de actividades diarias
-- CREATE TRIGGER trg_tbl_time_report_actividad_diaria_aud
-- AFTER INSERT OR UPDATE OR DELETE ON time_report.tbl_time_report_actividad_diaria
-- FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_historico();

-- -- Auditoría de permisos
-- CREATE TRIGGER trg_tbl_time_report_permiso_aud
-- AFTER INSERT OR UPDATE OR DELETE ON time_report.tbl_time_report_permiso
-- FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_historico();

-- -- Auditoría de usuarios (cambios en credenciales)
-- CREATE TRIGGER trg_tbl_autenticacion_usuario_aud
-- AFTER INSERT OR UPDATE OR DELETE ON autenticacion.tbl_autenticacion_usuario
-- FOR EACH ROW EXECUTE FUNCTION auditoria.fn_auditoria_registrar_historico();

-- =============================================================================
-- ██████╗    10 — VERIFICACIÓN FINAL
-- =============================================================================
DO $$
DECLARE
    v_tablas   INTEGER;
    v_funcs    INTEGER;
    v_triggers INTEGER;
    v_indices  INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_tablas
    FROM information_schema.tables
    WHERE table_schema IN ('administracion','inventario','autenticacion','auditoria','time_report')
      AND table_type = 'BASE TABLE';

    SELECT COUNT(*) INTO v_funcs
    FROM information_schema.routines
    WHERE routine_schema IN ('administracion','inventario','autenticacion','auditoria','time_report');

    SELECT COUNT(*) INTO v_triggers
    FROM information_schema.triggers
    WHERE trigger_schema IN ('administracion','inventario','autenticacion','auditoria','time_report');

    SELECT COUNT(*) INTO v_indices
    FROM pg_indexes
    WHERE schemaname IN ('administracion','inventario','autenticacion','auditoria','time_report');

    RAISE NOTICE '=========================================================';
    RAISE NOTICE '  Inv_tmr_db — DESPLIEGUE COMPLETADO';
    RAISE NOTICE '=========================================================';
    RAISE NOTICE '  Tablas   : %', v_tablas;
    RAISE NOTICE '  Funciones: %', v_funcs;
    RAISE NOTICE '  Triggers : %', v_triggers;
    RAISE NOTICE '  Índices  : %', v_indices;
    RAISE NOTICE '=========================================================';
END $$;
COMMIT;