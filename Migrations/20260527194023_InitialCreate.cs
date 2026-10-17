using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace tmr_backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "administracion");

            migrationBuilder.EnsureSchema(
                name: "auditoria");

            migrationBuilder.EnsureSchema(
                name: "autenticacion");

            migrationBuilder.EnsureSchema(
                name: "inventario");

            migrationBuilder.EnsureSchema(
                name: "time_report");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.CreateTable(
                name: "Actividades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Actividades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Empresa = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Colaboradores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Colaboradores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracionesSistema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesSistema", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DashboardItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lider",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PrimerNombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Apellidos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CorreoElectronico = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lider", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Proyectos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proyectos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosTiempo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosTiempo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reportes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reportes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_apariencia",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    fondologin = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    tipografia = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    encabezadofijo = table.Column<bool>(type: "boolean", nullable: false),
                    posicionmenu = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    menucolapsado = table.Column<bool>(type: "boolean", nullable: false),
                    colorfondo = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    bordercaja = table.Column<int>(type: "integer", nullable: false),
                    fondocaja = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_apariencia", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_catalogo",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipocatalogo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    codigo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_catalogo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_auditoria_asignacion_equipo",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idasignacion = table.Column<int>(type: "integer", nullable: false),
                    tipocambio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    camposmodificados = table.Column<string>(type: "jsonb", nullable: true),
                    valoresanteriores = table.Column<string>(type: "jsonb", nullable: true),
                    valoresnuevos = table.Column<string>(type: "jsonb", nullable: true),
                    cambiadopor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    direccionip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    agenteusuario = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria_asignacion_equipo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_auditoria_empleado",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idempleado = table.Column<int>(type: "integer", nullable: false),
                    tipocambio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    camposmodificados = table.Column<string>(type: "jsonb", nullable: true),
                    valoresanteriores = table.Column<string>(type: "jsonb", nullable: true),
                    valoresnuevos = table.Column<string>(type: "jsonb", nullable: true),
                    cambiadopor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    direccionip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    agenteusuario = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria_empleado", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_auditoria_equipo",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idequipo = table.Column<int>(type: "integer", nullable: false),
                    tipocambio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    camposmodificados = table.Column<string>(type: "jsonb", nullable: true),
                    valoresanteriores = table.Column<string>(type: "jsonb", nullable: true),
                    valoresnuevos = table.Column<string>(type: "jsonb", nullable: true),
                    cambiadopor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    direccionip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    agenteusuario = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria_equipo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_auditoria_historico_general",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombretabla = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    idregistro = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tipooperacion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    datosanteriores = table.Column<string>(type: "jsonb", nullable: true),
                    datosnuevos = table.Column<string>(type: "jsonb", nullable: true),
                    cambiadopor = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechacambio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    direccionip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria_historico_general", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_auditoria_sesion_usuario",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    tipocambio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    direccionip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    agenteusuario = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria_sesion_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_aplicacion",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombreaplicacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    urlbase = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_aplicacion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_inventario_token",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    token = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_inventario_token", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_modulo",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombremodulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rutamodulo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    icono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ordenvisualizacion = table.Column<int>(type: "integer", nullable: true),
                    idsubmodulo = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_modulo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_token_blacklist",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    token = table.Column<string>(type: "text", nullable: false),
                    fechaexpiracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_token_blacklist", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_empresa",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombreempresa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    direccion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_empresa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_feriado",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombreferiado = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    fechaferiado = table.Column<DateOnly>(type: "date", nullable: false),
                    esrecurrente = table.Column<bool>(type: "boolean", nullable: true),
                    tipoferiado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_feriado", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_outbox_cargo",
                schema: "time_report",
                columns: table => new
                {
                    idoutbox = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    idagregado = table.Column<int>(type: "integer", nullable: false),
                    operacion = table.Column<char>(type: "character(1)", maxLength: 1, nullable: false),
                    payloadjson = table.Column<string>(type: "jsonb", nullable: true),
                    intentos = table.Column<short>(type: "smallint", nullable: false),
                    proximointento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    procesadoen = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    mensajeerror = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    creadoen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_outbox_cargo", x => x.idoutbox);
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_tipo_actividad",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombretipo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    codigocolor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_tipo_actividad", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_tipo_proyecto",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombretipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    essubtipo = table.Column<bool>(type: "boolean", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_tipo_proyecto", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_catalogo_detalle",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idcatalogo = table.Column<int>(type: "integer", nullable: false),
                    codigovalor = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    valor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    orden = table.Column<short>(type: "smallint", nullable: true),
                    valorextra = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_catalogo_detalle", x => x.id);
                    table.ForeignKey(
                        name: "fk_administracion_catalogo_detalle_catalogo",
                        column: x => x.idcatalogo,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_menu",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idaplicacion = table.Column<int>(type: "integer", nullable: true),
                    nombremenu = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    rutamenu = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    icono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ordenvisualizacion = table.Column<int>(type: "integer", nullable: true),
                    idmenupadre = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_menu", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_menu_aplicacion",
                        column: x => x.idaplicacion,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_aplicacion",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_menu_padre",
                        column: x => x.idmenupadre,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_menu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_cargo",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    iddepartamento = table.Column<int>(type: "integer", nullable: true),
                    nombrecargo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_cargo", x => x.id);
                    table.ForeignKey(
                        name: "fk_administracion_cargo_departamento",
                        column: x => x.iddepartamento,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_cliente",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    numeroidentificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    idtipoidentificacion = table.Column<int>(type: "integer", nullable: true),
                    nombrecomercial = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    razonsocial = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    direccion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_cliente", x => x.id);
                    table.ForeignKey(
                        name: "fk_administracion_cliente_tipo_identificacion",
                        column: x => x.idtipoidentificacion,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_persona",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    numeroidentificacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    idtipoidentificacion = table.Column<int>(type: "integer", nullable: true),
                    idgenero = table.Column<int>(type: "integer", nullable: true),
                    idnacionalidad = table.Column<int>(type: "integer", nullable: true),
                    tipopersona = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nombres = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    apellidos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fechanacimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    direccion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_persona", x => x.id);
                    table.ForeignKey(
                        name: "fk_administracion_persona_genero",
                        column: x => x.idgenero,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_administracion_persona_nacionalidad",
                        column: x => x.idnacionalidad,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_administracion_persona_tipo_identificacion",
                        column: x => x.idtipoidentificacion,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_privilegio_rol",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idprivilegio = table.Column<int>(type: "integer", nullable: false),
                    idrol = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_privilegio_rol", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_privilegio_rol_privilegio",
                        column: x => x.idprivilegio,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_privilegio_rol_rol",
                        column: x => x.idrol,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_rol_modulo",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idrol = table.Column<int>(type: "integer", nullable: false),
                    idmodulo = table.Column<int>(type: "integer", nullable: false),
                    puedever = table.Column<bool>(type: "boolean", nullable: true),
                    puedecrear = table.Column<bool>(type: "boolean", nullable: true),
                    puedeeditar = table.Column<bool>(type: "boolean", nullable: true),
                    puedeeliminar = table.Column<bool>(type: "boolean", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_rol_modulo", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_rol_modulo_modulo",
                        column: x => x.idmodulo,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_modulo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_rol_modulo_rol",
                        column: x => x.idrol,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_proveedor",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idtipoproveedor = table.Column<int>(type: "integer", nullable: true),
                    nombreproveedor = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ruc = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    direccion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_proveedor", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_proveedor_tipo_proveedor",
                        column: x => x.idtipoproveedor,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_stock_categoria",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idcategoria = table.Column<int>(type: "integer", nullable: false),
                    stockminimo = table.Column<int>(type: "integer", nullable: false),
                    stockmaximo = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_stock_categoria", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_stock_categoria_categoria",
                        column: x => x.idcategoria,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_menu_rol",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idmenu = table.Column<int>(type: "integer", nullable: false),
                    idrol = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_menu_rol", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_menu_rol_menu",
                        column: x => x.idmenu,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_menu",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_menu_rol_rol",
                        column: x => x.idrol,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_proyeccion_horas",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    grupoproyeccion = table.Column<Guid>(type: "uuid", nullable: true),
                    idtiporecurso = table.Column<int>(type: "integer", nullable: false),
                    nombrerecurso = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    nombreproyeccion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    costoporhora = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cantidadrecurso = table.Column<int>(type: "integer", nullable: false),
                    distribuciontiempo = table.Column<string>(type: "text", nullable: false),
                    tiempototal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    costorecurso = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    porcentajeparticipacion = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    tipoperiodo = table.Column<bool>(type: "boolean", nullable: false),
                    cantidadperiodo = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_proyeccion_horas", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_report_proyeccion_horas_tipo_recurso",
                        column: x => x.idtiporecurso,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_cargo",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_empleado",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idpersona = table.Column<int>(type: "integer", nullable: false),
                    idcargo = table.Column<int>(type: "integer", nullable: true),
                    idmodotrabajo = table.Column<int>(type: "integer", nullable: true),
                    idcategoriaempleado = table.Column<int>(type: "integer", nullable: true),
                    idempresacatalogo = table.Column<int>(type: "integer", nullable: true),
                    codigoempleado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fechaingreso = table.Column<DateOnly>(type: "date", nullable: true),
                    fechaterminacion = table.Column<DateOnly>(type: "date", nullable: true),
                    idtipocontrato = table.Column<int>(type: "integer", nullable: true),
                    emailcorporativo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    salario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_empleado", x => x.id);
                    table.ForeignKey(
                        name: "fk_administracion_empleado_cargo",
                        column: x => x.idcargo,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_cargo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_administracion_empleado_categoria",
                        column: x => x.idcategoriaempleado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_administracion_empleado_contrato",
                        column: x => x.idtipocontrato,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_administracion_empleado_empresa",
                        column: x => x.idempresacatalogo,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_administracion_empleado_modo_trabajo",
                        column: x => x.idmodotrabajo,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_administracion_empleado_persona",
                        column: x => x.idpersona,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_persona",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_lider",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    EsInterno = table.Column<bool>(type: "boolean", nullable: false),
                    idpersona = table.Column<int>(type: "integer", nullable: false),
                    idtipo = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_lider", x => x.id);
                    table.ForeignKey(
                        name: "fk_administracion_lider_persona",
                        column: x => x.idpersona,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_persona",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_administracion_lider_tipo",
                        column: x => x.idtipo,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_usuario",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idpersona = table.Column<int>(type: "integer", nullable: true),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    hashpassword = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ultimologin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    estaactivo = table.Column<bool>(type: "boolean", nullable: true),
                    debecambiarpassword = table.Column<bool>(type: "boolean", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_usuario", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_usuario_persona",
                        column: x => x.idpersona,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_persona",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_factura",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    numerofactura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    idproveedor = table.Column<int>(type: "integer", nullable: true),
                    fechafactura = table.Column<DateOnly>(type: "date", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_factura", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_factura_proveedor",
                        column: x => x.idproveedor,
                        principalSchema: "inventario",
                        principalTable: "tbl_inventario_proveedor",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_registro_asignacion",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idempleado = table.Column<int>(type: "integer", nullable: true),
                    fecharegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_registro_asignacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_administracion_registro_asignacion_empleado",
                        column: x => x.idempleado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_homologacion_banco",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idempleado = table.Column<int>(type: "integer", nullable: false),
                    nombrecompletotr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cedulatr = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    proyectotr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    clientetr = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nombrecompletobanco = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    observacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fecharegistro = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_homologacion_banco", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_report_homologacion_banco_empleado",
                        column: x => x.idempleado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_permiso",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idempleado = table.Column<int>(type: "integer", nullable: false),
                    idtipopermiso = table.Column<int>(type: "integer", nullable: false),
                    idestadoaprobacion = table.Column<int>(type: "integer", nullable: false),
                    fechainicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fechafin = table.Column<DateOnly>(type: "date", nullable: false),
                    totaldias = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    totalhoras = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    espagado = table.Column<bool>(type: "boolean", nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    aprobadopor = table.Column<int>(type: "integer", nullable: true),
                    fechaaprobacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observacion = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_permiso", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_report_permiso_aprobador",
                        column: x => x.aprobadopor,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_permiso_empleado",
                        column: x => x.idempleado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_permiso_estado_aprobacion",
                        column: x => x.idestadoaprobacion,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_permiso_tipo",
                        column: x => x.idtipopermiso,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_proyecto",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idcliente = table.Column<int>(type: "integer", nullable: true),
                    idestadoproyecto = table.Column<int>(type: "integer", nullable: false),
                    idtipoproyecto = table.Column<int>(type: "integer", nullable: true),
                    idlider = table.Column<int>(type: "integer", nullable: true),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    fechainicioplaneada = table.Column<DateOnly>(type: "date", nullable: true),
                    fechafinplaneada = table.Column<DateOnly>(type: "date", nullable: true),
                    fechainicioreal = table.Column<DateOnly>(type: "date", nullable: true),
                    fechafinreal = table.Column<DateOnly>(type: "date", nullable: true),
                    fechainicioespera = table.Column<DateOnly>(type: "date", nullable: true),
                    fechafinespera = table.Column<DateOnly>(type: "date", nullable: true),
                    observacion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    presupuesto = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    horasasignadas = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_proyecto", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_report_proyecto_cliente",
                        column: x => x.idcliente,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_cliente",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_proyecto_estado",
                        column: x => x.idestadoproyecto,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_proyecto_lider",
                        column: x => x.idlider,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_lider",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_proyecto_tipo",
                        column: x => x.idtipoproyecto,
                        principalSchema: "time_report",
                        principalTable: "tbl_time_report_tipo_proyecto",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_cliente_usuario",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idcliente = table.Column<int>(type: "integer", nullable: false),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_cliente_usuario", x => x.id);
                    table.ForeignKey(
                        name: "fk_administracion_cliente_usuario_cliente",
                        column: x => x.idcliente,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_cliente",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_administracion_cliente_usuario_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_menu_usuario",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idmenu = table.Column<int>(type: "integer", nullable: false),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_menu_usuario", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_menu_usuario_menu",
                        column: x => x.idmenu,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_menu",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_menu_usuario_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_password_historial",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    hashpassword = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    fechacambio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_password_historial", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_password_historial_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_pregunta_usuario",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    pregunta = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    respuesta = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_pregunta_usuario", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_pregunta_usuario_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_privilegio_usuario",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idprivilegio = table.Column<int>(type: "integer", nullable: false),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_privilegio_usuario", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_privilegio_usuario_privilegio",
                        column: x => x.idprivilegio,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_privilegio_usuario_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_sesion",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    tokensesion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    horaingreso = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    horasalida = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    direccionip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    agenteusuario = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    dispositivoinfo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ubicacioninfo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    estaactiva = table.Column<bool>(type: "boolean", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_sesion", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_sesion_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_sesion_app",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    idaplicacion = table.Column<int>(type: "integer", nullable: false),
                    tokenapp = table.Column<string>(type: "text", nullable: false),
                    fechaexpiracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_sesion_app", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_sesion_app_aplicacion",
                        column: x => x.idaplicacion,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_aplicacion",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_sesion_app_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_usuario_aplicacion",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    idaplicacion = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_usuario_aplicacion", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_usuario_aplicacion_aplicacion",
                        column: x => x.idaplicacion,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_aplicacion",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_usuario_aplicacion_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_usuario_modulo",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    idmodulo = table.Column<int>(type: "integer", nullable: false),
                    puedever = table.Column<bool>(type: "boolean", nullable: true),
                    puedecrear = table.Column<bool>(type: "boolean", nullable: true),
                    puedeeditar = table.Column<bool>(type: "boolean", nullable: true),
                    puedeeliminar = table.Column<bool>(type: "boolean", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_usuario_modulo", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_usuario_modulo_modulo",
                        column: x => x.idmodulo,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_modulo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_usuario_modulo_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_usuario_rol",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    idrol = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_usuario_rol", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_usuario_rol_rol",
                        column: x => x.idrol,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_autenticacion_usuario_rol_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_detalle_factura",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idfactura = table.Column<int>(type: "integer", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    cantidad = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    preciounitario = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    iva = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_detalle_factura", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_detalle_factura_factura",
                        column: x => x.idfactura,
                        principalSchema: "inventario",
                        principalTable: "tbl_inventario_factura",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_equipo",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idcategoria = table.Column<int>(type: "integer", nullable: false),
                    idestado = table.Column<int>(type: "integer", nullable: false),
                    idcondicion = table.Column<int>(type: "integer", nullable: true),
                    idtipogarantia = table.Column<int>(type: "integer", nullable: true),
                    idfactura = table.Column<int>(type: "integer", nullable: true),
                    marca = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    numeroserie = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fechaadquisicion = table.Column<DateOnly>(type: "date", nullable: true),
                    fechavencimientogarantia = table.Column<DateOnly>(type: "date", nullable: true),
                    valoradquisicion = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    datosadicionales = table.Column<string>(type: "jsonb", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_equipo", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_equipo_categoria",
                        column: x => x.idcategoria,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_inventario_equipo_condicion",
                        column: x => x.idcondicion,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_inventario_equipo_estado",
                        column: x => x.idestado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_inventario_equipo_factura",
                        column: x => x.idfactura,
                        principalSchema: "inventario",
                        principalTable: "tbl_inventario_factura",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_inventario_equipo_tipo_garantia",
                        column: x => x.idtipogarantia,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_actividad_diaria",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idempleado = table.Column<int>(type: "integer", nullable: false),
                    idproyecto = table.Column<int>(type: "integer", nullable: true),
                    idtipoactividad = table.Column<int>(type: "integer", nullable: false),
                    codigorequerimiento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cantidadhoras = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    fechaactividad = table.Column<DateOnly>(type: "date", nullable: false),
                    descripcionactividad = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    notas = table.Column<string>(type: "text", nullable: true),
                    esbillable = table.Column<bool>(type: "boolean", nullable: true),
                    aprobadopor = table.Column<int>(type: "integer", nullable: true),
                    fechaaprobacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_actividad_diaria", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_report_actividad_diaria_aprobador",
                        column: x => x.aprobadopor,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_actividad_diaria_empleado",
                        column: x => x.idempleado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_actividad_diaria_proyecto",
                        column: x => x.idproyecto,
                        principalSchema: "time_report",
                        principalTable: "tbl_time_report_proyecto",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_actividad_diaria_tipo",
                        column: x => x.idtipoactividad,
                        principalSchema: "time_report",
                        principalTable: "tbl_time_report_tipo_actividad",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_empleado_proyecto",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idempleado = table.Column<int>(type: "integer", nullable: true),
                    idproyecto = table.Column<int>(type: "integer", nullable: false),
                    idproveedor = table.Column<int>(type: "integer", nullable: true),
                    fechaasignacion = table.Column<DateOnly>(type: "date", nullable: true),
                    fechafinasignacion = table.Column<DateOnly>(type: "date", nullable: true),
                    rolasignado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    costoporhora = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    horasasignadas = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_empleado_proyecto", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_report_empleado_proyecto_empleado",
                        column: x => x.idempleado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_empleado_proyecto_proyecto",
                        column: x => x.idproyecto,
                        principalSchema: "time_report",
                        principalTable: "tbl_time_report_proyecto",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_proyeccion_horas_proyecto",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idproyecto = table.Column<int>(type: "integer", nullable: false),
                    idtiporecurso = table.Column<int>(type: "integer", nullable: false),
                    nombrerecurso = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    costoporhora = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cantidadrecurso = table.Column<int>(type: "integer", nullable: false),
                    distribuciontiempo = table.Column<string>(type: "text", nullable: false),
                    tiempototal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    costorecurso = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    porcentajeparticipacion = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    tipoperiodo = table.Column<bool>(type: "boolean", nullable: false),
                    cantidadperiodo = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_proyeccion_horas_proyecto", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_report_proyeccion_horas_proyecto_proyecto",
                        column: x => x.idproyecto,
                        principalSchema: "time_report",
                        principalTable: "tbl_time_report_proyecto",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_proyeccion_horas_proyecto_tipo_recurso",
                        column: x => x.idtiporecurso,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_cargo",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_asignacion_equipo",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idequipo = table.Column<int>(type: "integer", nullable: false),
                    idempleado = table.Column<int>(type: "integer", nullable: false),
                    fechaasignacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    fechadevolucion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    observacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_asignacion_equipo", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_asignacion_equipo_empleado",
                        column: x => x.idempleado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_inventario_asignacion_equipo_equipo",
                        column: x => x.idequipo,
                        principalSchema: "inventario",
                        principalTable: "tbl_inventario_equipo",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_baja_equipo",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idequipo = table.Column<int>(type: "integer", nullable: false),
                    idtipobaja = table.Column<int>(type: "integer", nullable: true),
                    fechabaja = table.Column<DateOnly>(type: "date", nullable: false),
                    motivobaja = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_baja_equipo", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_baja_equipo_equipo",
                        column: x => x.idequipo,
                        principalSchema: "inventario",
                        principalTable: "tbl_inventario_equipo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_inventario_baja_equipo_tipo_baja",
                        column: x => x.idtipobaja,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_caracteristica_equipo",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idequipo = table.Column<int>(type: "integer", nullable: false),
                    idtipocomponente = table.Column<int>(type: "integer", nullable: true),
                    nombrecaracteristica = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valorcaracteristica = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_caracteristica_equipo", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_caracteristica_equipo_equipo",
                        column: x => x.idequipo,
                        principalSchema: "inventario",
                        principalTable: "tbl_inventario_equipo",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_inventario_caracteristica_equipo_tipo_componente",
                        column: x => x.idtipocomponente,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_catalogo_detalle",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_inventario_reparacion_equipo",
                schema: "inventario",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idequipo = table.Column<int>(type: "integer", nullable: false),
                    fechainicio = table.Column<DateOnly>(type: "date", nullable: false),
                    fechafin = table.Column<DateOnly>(type: "date", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    costoreparacion = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventario_reparacion_equipo", x => x.id);
                    table.ForeignKey(
                        name: "fk_inventario_reparacion_equipo_equipo",
                        column: x => x.idequipo,
                        principalSchema: "inventario",
                        principalTable: "tbl_inventario_equipo",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_cargo_iddepartamento",
                schema: "administracion",
                table: "tbl_administracion_cargo",
                column: "iddepartamento");

            migrationBuilder.CreateIndex(
                name: "uq_administracion_catalogo_tipo_codigo",
                schema: "administracion",
                table: "tbl_administracion_catalogo",
                columns: new[] { "tipocatalogo", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_administracion_catalogo_detalle_id_codigo",
                schema: "administracion",
                table: "tbl_administracion_catalogo_detalle",
                columns: new[] { "idcatalogo", "codigovalor" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_cliente_idtipoidentificacion",
                schema: "administracion",
                table: "tbl_administracion_cliente",
                column: "idtipoidentificacion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_cliente_usuario_idcliente",
                schema: "administracion",
                table: "tbl_administracion_cliente_usuario",
                column: "idcliente");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_cliente_usuario_idusuario",
                schema: "administracion",
                table: "tbl_administracion_cliente_usuario",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "idx_adm_empleado_persona",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idpersona");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_empleado_idcargo",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idcargo");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_empleado_idcategoriaempleado",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idcategoriaempleado");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_empleado_idempresacatalogo",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idempresacatalogo");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_empleado_idmodotrabajo",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idmodotrabajo");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_empleado_idtipocontrato",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idtipocontrato");

            migrationBuilder.CreateIndex(
                name: "uq_administracion_empleado_codigo",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "codigoempleado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_lider_idpersona",
                schema: "administracion",
                table: "tbl_administracion_lider",
                column: "idpersona");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_lider_idtipo",
                schema: "administracion",
                table: "tbl_administracion_lider",
                column: "idtipo");

            migrationBuilder.CreateIndex(
                name: "idx_adm_persona_identificacion",
                schema: "administracion",
                table: "tbl_administracion_persona",
                column: "numeroidentificacion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_persona_idgenero",
                schema: "administracion",
                table: "tbl_administracion_persona",
                column: "idgenero");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_persona_idnacionalidad",
                schema: "administracion",
                table: "tbl_administracion_persona",
                column: "idnacionalidad");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_persona_idtipoidentificacion",
                schema: "administracion",
                table: "tbl_administracion_persona",
                column: "idtipoidentificacion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_registro_asignacion_idempleado",
                schema: "administracion",
                table: "tbl_administracion_registro_asignacion",
                column: "idempleado");

            migrationBuilder.CreateIndex(
                name: "idx_aud_empleado_fecha",
                schema: "auditoria",
                table: "tbl_auditoria_empleado",
                column: "fechacreacion");

            migrationBuilder.CreateIndex(
                name: "idx_aud_empleado_id",
                schema: "auditoria",
                table: "tbl_auditoria_empleado",
                column: "idempleado");

            migrationBuilder.CreateIndex(
                name: "idx_aud_historico_fecha",
                schema: "auditoria",
                table: "tbl_auditoria_historico_general",
                column: "fechacambio");

            migrationBuilder.CreateIndex(
                name: "idx_aud_historico_tabla",
                schema: "auditoria",
                table: "tbl_auditoria_historico_general",
                column: "nombretabla");

            migrationBuilder.CreateIndex(
                name: "idx_aud_sesion_usuario",
                schema: "auditoria",
                table: "tbl_auditoria_sesion_usuario",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_menu_idaplicacion",
                schema: "autenticacion",
                table: "tbl_autenticacion_menu",
                column: "idaplicacion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_menu_idmenupadre",
                schema: "autenticacion",
                table: "tbl_autenticacion_menu",
                column: "idmenupadre");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_menu_rol_idmenu",
                schema: "autenticacion",
                table: "tbl_autenticacion_menu_rol",
                column: "idmenu");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_menu_rol_idrol",
                schema: "autenticacion",
                table: "tbl_autenticacion_menu_rol",
                column: "idrol");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_menu_usuario_idmenu",
                schema: "autenticacion",
                table: "tbl_autenticacion_menu_usuario",
                column: "idmenu");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_menu_usuario_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_menu_usuario",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_password_historial_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_password_historial",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_pregunta_usuario_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_pregunta_usuario",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_privilegio_rol_idprivilegio",
                schema: "autenticacion",
                table: "tbl_autenticacion_privilegio_rol",
                column: "idprivilegio");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_privilegio_rol_idrol",
                schema: "autenticacion",
                table: "tbl_autenticacion_privilegio_rol",
                column: "idrol");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_privilegio_usuario_idprivilegio",
                schema: "autenticacion",
                table: "tbl_autenticacion_privilegio_usuario",
                column: "idprivilegio");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_privilegio_usuario_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_privilegio_usuario",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_rol_modulo_idmodulo",
                schema: "autenticacion",
                table: "tbl_autenticacion_rol_modulo",
                column: "idmodulo");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_rol_modulo_idrol",
                schema: "autenticacion",
                table: "tbl_autenticacion_rol_modulo",
                column: "idrol");

            migrationBuilder.CreateIndex(
                name: "idx_aut_sesion_activa",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                column: "estaactiva");

            migrationBuilder.CreateIndex(
                name: "idx_aut_sesion_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_sesion_app_idaplicacion",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion_app",
                column: "idaplicacion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_sesion_app_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion_app",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "idx_aut_token_blacklist_token",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                column: "token");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_usuario_idpersona",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                column: "idpersona");

            migrationBuilder.CreateIndex(
                name: "uq_autenticacion_usuario_nombre",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_usuario_aplicacion_idaplicacion",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_aplicacion",
                column: "idaplicacion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_usuario_aplicacion_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_aplicacion",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_usuario_modulo_idmodulo",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_modulo",
                column: "idmodulo");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_usuario_modulo_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_modulo",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_usuario_rol_idrol",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "idrol");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_usuario_rol_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "idx_inv_asignacion_devolucion",
                schema: "inventario",
                table: "tbl_inventario_asignacion_equipo",
                column: "fechadevolucion");

            migrationBuilder.CreateIndex(
                name: "idx_inv_asignacion_empleado",
                schema: "inventario",
                table: "tbl_inventario_asignacion_equipo",
                column: "idempleado");

            migrationBuilder.CreateIndex(
                name: "idx_inv_asignacion_equipo",
                schema: "inventario",
                table: "tbl_inventario_asignacion_equipo",
                column: "idequipo");

            migrationBuilder.CreateIndex(
                name: "idx_inv_baja_equipo",
                schema: "inventario",
                table: "tbl_inventario_baja_equipo",
                column: "idequipo");

            migrationBuilder.CreateIndex(
                name: "idx_inv_baja_tipo",
                schema: "inventario",
                table: "tbl_inventario_baja_equipo",
                column: "idtipobaja");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_inventario_caracteristica_equipo_idequipo",
                schema: "inventario",
                table: "tbl_inventario_caracteristica_equipo",
                column: "idequipo");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_inventario_caracteristica_equipo_idtipocomponente",
                schema: "inventario",
                table: "tbl_inventario_caracteristica_equipo",
                column: "idtipocomponente");

            migrationBuilder.CreateIndex(
                name: "idx_inv_detalle_factura",
                schema: "inventario",
                table: "tbl_inventario_detalle_factura",
                column: "idfactura");

            migrationBuilder.CreateIndex(
                name: "idx_inv_equipo_activo",
                schema: "inventario",
                table: "tbl_inventario_equipo",
                column: "activo");

            migrationBuilder.CreateIndex(
                name: "idx_inv_equipo_categoria",
                schema: "inventario",
                table: "tbl_inventario_equipo",
                column: "idcategoria");

            migrationBuilder.CreateIndex(
                name: "idx_inv_equipo_estado",
                schema: "inventario",
                table: "tbl_inventario_equipo",
                column: "idestado");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_inventario_equipo_idcondicion",
                schema: "inventario",
                table: "tbl_inventario_equipo",
                column: "idcondicion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_inventario_equipo_idfactura",
                schema: "inventario",
                table: "tbl_inventario_equipo",
                column: "idfactura");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_inventario_equipo_idtipogarantia",
                schema: "inventario",
                table: "tbl_inventario_equipo",
                column: "idtipogarantia");

            migrationBuilder.CreateIndex(
                name: "uq_inventario_equipo_serie",
                schema: "inventario",
                table: "tbl_inventario_equipo",
                column: "numeroserie",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_inv_factura_proveedor",
                schema: "inventario",
                table: "tbl_inventario_factura",
                column: "idproveedor");

            migrationBuilder.CreateIndex(
                name: "uq_inventario_factura_numero",
                schema: "inventario",
                table: "tbl_inventario_factura",
                column: "numerofactura",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_inventario_proveedor_idtipoproveedor",
                schema: "inventario",
                table: "tbl_inventario_proveedor",
                column: "idtipoproveedor");

            migrationBuilder.CreateIndex(
                name: "idx_inv_reparacion_equipo",
                schema: "inventario",
                table: "tbl_inventario_reparacion_equipo",
                column: "idequipo");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_inventario_stock_categoria_idcategoria",
                schema: "inventario",
                table: "tbl_inventario_stock_categoria",
                column: "idcategoria");

            migrationBuilder.CreateIndex(
                name: "idx_tr_actividad_empleado",
                schema: "time_report",
                table: "tbl_time_report_actividad_diaria",
                column: "idempleado");

            migrationBuilder.CreateIndex(
                name: "idx_tr_actividad_fecha",
                schema: "time_report",
                table: "tbl_time_report_actividad_diaria",
                column: "fechaactividad");

            migrationBuilder.CreateIndex(
                name: "idx_tr_actividad_proyecto",
                schema: "time_report",
                table: "tbl_time_report_actividad_diaria",
                column: "idproyecto");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_actividad_diaria_aprobadopor",
                schema: "time_report",
                table: "tbl_time_report_actividad_diaria",
                column: "aprobadopor");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_actividad_diaria_idtipoactividad",
                schema: "time_report",
                table: "tbl_time_report_actividad_diaria",
                column: "idtipoactividad");

            migrationBuilder.CreateIndex(
                name: "idx_tr_empleado_proyecto_emp",
                schema: "time_report",
                table: "tbl_time_report_empleado_proyecto",
                column: "idempleado");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_empleado_proyecto_idproyecto",
                schema: "time_report",
                table: "tbl_time_report_empleado_proyecto",
                column: "idproyecto");

            migrationBuilder.CreateIndex(
                name: "uq_time_report_homologacion_banco",
                schema: "time_report",
                table: "tbl_time_report_homologacion_banco",
                columns: new[] { "idempleado", "proyectotr" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_tr_outbox_procesamiento",
                schema: "time_report",
                table: "tbl_time_report_outbox_cargo",
                columns: new[] { "proximointento", "procesadoen" },
                filter: "(procesadoen IS NULL)");

            migrationBuilder.CreateIndex(
                name: "idx_tr_permiso_empleado",
                schema: "time_report",
                table: "tbl_time_report_permiso",
                column: "idempleado");

            migrationBuilder.CreateIndex(
                name: "idx_tr_permiso_estado",
                schema: "time_report",
                table: "tbl_time_report_permiso",
                column: "idestadoaprobacion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_permiso_aprobadopor",
                schema: "time_report",
                table: "tbl_time_report_permiso",
                column: "aprobadopor");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_permiso_idtipopermiso",
                schema: "time_report",
                table: "tbl_time_report_permiso",
                column: "idtipopermiso");

            migrationBuilder.CreateIndex(
                name: "idx_tr_proyeccion_grupo",
                schema: "time_report",
                table: "tbl_time_report_proyeccion_horas",
                column: "grupoproyeccion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_proyeccion_horas_idtiporecurso",
                schema: "time_report",
                table: "tbl_time_report_proyeccion_horas",
                column: "idtiporecurso");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_proyeccion_horas_proyecto_idproyecto",
                schema: "time_report",
                table: "tbl_time_report_proyeccion_horas_proyecto",
                column: "idproyecto");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_proyeccion_horas_proyecto_idtiporecurso",
                schema: "time_report",
                table: "tbl_time_report_proyeccion_horas_proyecto",
                column: "idtiporecurso");

            migrationBuilder.CreateIndex(
                name: "idx_tr_proyecto_cliente",
                schema: "time_report",
                table: "tbl_time_report_proyecto",
                column: "idcliente");

            migrationBuilder.CreateIndex(
                name: "idx_tr_proyecto_lider",
                schema: "time_report",
                table: "tbl_time_report_proyecto",
                column: "idlider");

            migrationBuilder.CreateIndex(
                name: "idx_tr_proyecto_tipo",
                schema: "time_report",
                table: "tbl_time_report_proyecto",
                column: "idtipoproyecto");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_proyecto_idestadoproyecto",
                schema: "time_report",
                table: "tbl_time_report_proyecto",
                column: "idestadoproyecto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Actividades");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "Colaboradores");

            migrationBuilder.DropTable(
                name: "ConfiguracionesSistema");

            migrationBuilder.DropTable(
                name: "DashboardItems");

            migrationBuilder.DropTable(
                name: "Lider");

            migrationBuilder.DropTable(
                name: "Proyectos");

            migrationBuilder.DropTable(
                name: "RegistrosTiempo");

            migrationBuilder.DropTable(
                name: "Reportes");

            migrationBuilder.DropTable(
                name: "tbl_administracion_apariencia",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_administracion_cliente_usuario",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_administracion_registro_asignacion",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_auditoria_asignacion_equipo",
                schema: "auditoria");

            migrationBuilder.DropTable(
                name: "tbl_auditoria_empleado",
                schema: "auditoria");

            migrationBuilder.DropTable(
                name: "tbl_auditoria_equipo",
                schema: "auditoria");

            migrationBuilder.DropTable(
                name: "tbl_auditoria_historico_general",
                schema: "auditoria");

            migrationBuilder.DropTable(
                name: "tbl_auditoria_sesion_usuario",
                schema: "auditoria");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_inventario_token",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_menu_rol",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_menu_usuario",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_password_historial",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_pregunta_usuario",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_privilegio_rol",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_privilegio_usuario",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_rol_modulo",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_sesion",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_sesion_app",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_token_blacklist",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_usuario_aplicacion",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_usuario_modulo",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_usuario_rol",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_inventario_asignacion_equipo",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_inventario_baja_equipo",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_inventario_caracteristica_equipo",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_inventario_detalle_factura",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_inventario_empresa",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_inventario_reparacion_equipo",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_inventario_stock_categoria",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_time_report_actividad_diaria",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_time_report_empleado_proyecto",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_time_report_feriado",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_time_report_homologacion_banco",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_time_report_outbox_cargo",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_time_report_permiso",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_time_report_proyeccion_horas",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_time_report_proyeccion_horas_proyecto",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_menu",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_modulo",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_usuario",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_inventario_equipo",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_time_report_tipo_actividad",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_administracion_empleado",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_time_report_proyecto",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_aplicacion",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_inventario_factura",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_administracion_cargo",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_administracion_cliente",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_administracion_lider",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_time_report_tipo_proyecto",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_inventario_proveedor",
                schema: "inventario");

            migrationBuilder.DropTable(
                name: "tbl_administracion_persona",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_administracion_catalogo_detalle",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_administracion_catalogo",
                schema: "administracion");
        }
    }
}
