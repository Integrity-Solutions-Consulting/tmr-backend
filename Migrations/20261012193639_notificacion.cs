using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace tmr_backend.Migrations
{
    /// <inheritdoc />
    public partial class notificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_sesion_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion");

            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_usuario_persona",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario");

            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_usuario_rol_rol",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_usuario_rol_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropForeignKey(
                name: "fk_time_report_proyecto_lider",
                schema: "time_report",
                table: "tbl_time_report_proyecto");

            migrationBuilder.DropTable(
                name: "ConfiguracionesSistema");

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
                name: "tbl_autenticacion_privilegio_rol",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_privilegio_usuario",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_rol_modulo",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_sesion_app",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_usuario_aplicacion",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_usuario_modulo",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_time_report_empleado_proyecto",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_menu",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_aplicacion",
                schema: "autenticacion");

            migrationBuilder.DropIndex(
                name: "idx_tr_proyecto_lider",
                schema: "time_report",
                table: "tbl_time_report_proyecto");

            migrationBuilder.DropPrimaryKey(
                name: "pk_autenticacion_usuario_rol",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropIndex(
                name: "IX_tbl_autenticacion_usuario_rol_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropPrimaryKey(
                name: "pk_autenticacion_token_blacklist",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropIndex(
                name: "idx_aut_token_blacklist_token",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Lider",
                table: "Lider");

            migrationBuilder.DropColumn(
                name: "idlider",
                schema: "time_report",
                table: "tbl_time_report_proyecto");

            migrationBuilder.DropColumn(
                name: "id",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropColumn(
                name: "estaactivo",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario");

            migrationBuilder.DropColumn(
                name: "id",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropColumn(
                name: "tokensesion",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion");

            migrationBuilder.DropColumn(
                name: "EsInterno",
                schema: "administracion",
                table: "tbl_administracion_lider");

            migrationBuilder.EnsureSchema(
                name: "notificacion");

            migrationBuilder.RenameTable(
                name: "Lider",
                newName: "Lideres");

            migrationBuilder.RenameColumn(
                name: "token",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                newName: "jti");

            migrationBuilder.RenameColumn(
                name: "horasalida",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                newName: "revocadofecha");

            migrationBuilder.RenameColumn(
                name: "horaingreso",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                newName: "ultimaactividad");

            migrationBuilder.RenameColumn(
                name: "idsubmodulo",
                schema: "autenticacion",
                table: "tbl_autenticacion_modulo",
                newName: "idmodulopadre");

            migrationBuilder.RenameColumn(
                name: "FechaCreacion",
                table: "Proyectos",
                newName: "Fechacreacion");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:autenticacion.audit_evento_enum", "LOGIN_OK,LOGIN_FAIL,LOGOUT,REFRESH_OK,REFRESH_FAIL,TOKEN_REVOKE,PASSWORD_CHANGE,USER_CREATE,USER_BLOCK")
                .Annotation("Npgsql:Enum:autenticacion.revocacion_razon_enum", "LOGOUT,ADMIN_REVOKE,ADMIN_REVOKE_ALL,PASSWORD_CHANGE,SECURITY_BREACH,EXPIRED,SESSION_LIMIT,TOKEN_REUSED,TOKEN_REVOKED,TOKEN_INVALID,TOKEN_EXPIRED,IP_MISMATCH,USER_AGENT_MISMATCH,SESSION_IDLE_TIMEOUT,SESSION_EXPIRED,REVOKE")
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.AddColumn<DateTime>(
                name: "asignadoen",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "asignadopor",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fechaexpiracion",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<DateTime>(
                name: "bloqueadohasta",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "emailverificado",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "intentosfallidos",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "fecharevocado",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "estaactiva",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "agenteusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "fechaexpiracion",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revocadorazon",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "aniosexperiencia",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "comentariosalida",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "idcausasalida",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "idempleadoreemplazo",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "idtiposalida",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "apellidos",
                schema: "administracion",
                table: "tbl_administracion_cliente",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "nombres",
                schema: "administracion",
                table: "tbl_administracion_cliente",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Proyectos",
                type: "integer",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<int>(
                name: "ClienteId",
                table: "Proyectos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EstadoProyectoId",
                table: "Proyectos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFinEspera",
                table: "Proyectos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaFinReal",
                table: "Proyectos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicioEspera",
                table: "Proyectos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaInicioReal",
                table: "Proyectos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Fechamodificacion",
                table: "Proyectos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdCliente",
                table: "Proyectos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdEstadoProyecto",
                table: "Proyectos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdLider",
                table: "Proyectos",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Ipcreacion",
                table: "Proyectos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Ipmodificacion",
                table: "Proyectos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LiderId",
                table: "Proyectos",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacion",
                table: "Proyectos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Usuariocreacion",
                table: "Proyectos",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Usuariomodificacion",
                table: "Proyectos",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_autenticacion_usuario_rol",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                columns: new[] { "idusuario", "idrol" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_tbl_autenticacion_token_blacklist",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                column: "jti");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Lideres",
                table: "Lideres",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "evento_outbox",
                schema: "notificacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    tipo_evento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ultimo_error = table.Column<string>(type: "text", nullable: true),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    fecha_procesado = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evento_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notificacion_envio",
                schema: "notificacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id_empleado = table.Column<int>(type: "integer", nullable: false),
                    inicio_periodo = table.Column<DateOnly>(type: "date", nullable: false),
                    fin_periodo = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_corte = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    origen = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    horas_faltantes = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    id_usuario_ejecutor = table.Column<int>(type: "integer", nullable: true),
                    fecha_envio = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    detalle_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacion_envio", x => x.id);
                    table.ForeignKey(
                        name: "fk_notificacion_envio_empleado",
                        column: x => x.id_empleado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "notificacion_fecha_corte",
                schema: "notificacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    anio = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    inicio_periodo = table.Column<DateOnly>(type: "date", nullable: false),
                    fin_periodo = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_corte = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ejecutada = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    fecha_ejecucion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    id_usuario_creador = table.Column<int>(type: "integer", nullable: false),
                    fecha_creacion = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notificacion_fecha_corte", x => x.id);
                    table.CheckConstraint("chk_notificacion_fecha_corte_mes", "mes BETWEEN 1 AND 12");
                });

            migrationBuilder.CreateTable(
                name: "tbl_administracion_empleado_contrato",
                schema: "administracion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idempleado = table.Column<int>(type: "integer", nullable: false),
                    idtipocontrato = table.Column<int>(type: "integer", nullable: false),
                    fechadesde = table.Column<DateOnly>(type: "date", nullable: false),
                    fechahasta = table.Column<DateOnly>(type: "date", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_administracion_empleado_contrato", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_audit_log",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: true),
                    agenteusuario = table.Column<string>(type: "text", nullable: true),
                    detalles = table.Column<string>(type: "jsonb", nullable: true),
                    direccionip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_audit_log_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_password_reset",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    tokenhash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    fechaexpiracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    utilizado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    fechautilizacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_password_reset", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_password_reset_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_permiso",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idmodulo = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    accion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_permiso", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_permiso_modulo",
                        column: x => x.idmodulo,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_modulo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_refresh_token",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idsesion = table.Column<long>(type: "bigint", nullable: false),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    tokenhash = table.Column<string>(type: "text", nullable: false),
                    familiatoken = table.Column<Guid>(type: "uuid", nullable: false),
                    estausado = table.Column<bool>(type: "boolean", nullable: false),
                    estarevocado = table.Column<bool>(type: "boolean", nullable: false),
                    fechaexpiracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fechausado = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_refresh_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_autenticacion_refresh_token_sesion",
                        column: x => x.idsesion,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_sesion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_autenticacion_refresh_token_usuario",
                        column: x => x.idusuario,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_rol",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    essistema = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_autenticacion_rol", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_time_report_asignacion_proyecto",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idempleado = table.Column<int>(type: "integer", nullable: true),
                    idproyecto = table.Column<int>(type: "integer", nullable: false),
                    idlider = table.Column<int>(type: "integer", nullable: true),
                    idproveedor = table.Column<int>(type: "integer", nullable: true),
                    fechaasignacion = table.Column<DateOnly>(type: "date", nullable: true),
                    fechafinasignacion = table.Column<DateOnly>(type: "date", nullable: true),
                    rolasignado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    costoporhora = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    horasasignadas = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    lidercosto = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: true),
                    liderhoras = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    estadoasignacion = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_time_report_asignacion_proyecto", x => x.id);
                    table.ForeignKey(
                        name: "fk_time_report_asignacion_proyecto_empleado",
                        column: x => x.idempleado,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_empleado",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_asignacion_proyecto_lider",
                        column: x => x.idlider,
                        principalSchema: "administracion",
                        principalTable: "tbl_administracion_lider",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_time_report_asignacion_proyecto_proyecto",
                        column: x => x.idproyecto,
                        principalSchema: "time_report",
                        principalTable: "tbl_time_report_proyecto",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_rol_permiso",
                schema: "autenticacion",
                columns: table => new
                {
                    idrol = table.Column<int>(type: "integer", nullable: false),
                    idpermiso = table.Column<int>(type: "integer", nullable: false),
                    otorgado = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_rol_permiso", x => new { x.idrol, x.idpermiso });
                    table.ForeignKey(
                        name: "fk_autenticacion_rol_permiso_permiso",
                        column: x => x.idpermiso,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_permiso",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_autenticacion_rol_permiso_rol",
                        column: x => x.idrol,
                        principalSchema: "autenticacion",
                        principalTable: "tbl_autenticacion_rol",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_usuario_rol_asignadopor",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "asignadopor");

            migrationBuilder.CreateIndex(
                name: "uq_autenticacion_usuario_email",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_aut_token_blacklist_exp",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                column: "fechaexpiracion");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_token_blacklist_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "uq_autenticacion_token_blacklist_jti",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                column: "jti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_empleado_idcausasalida",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idcausasalida");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_empleado_idempleadoreemplazo",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idempleadoreemplazo");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_administracion_empleado_idtiposalida",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idtiposalida");

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_ClienteId",
                table: "Proyectos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_EstadoProyectoId",
                table: "Proyectos",
                column: "EstadoProyectoId");

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_LiderId",
                table: "Proyectos",
                column: "LiderId");

            migrationBuilder.CreateIndex(
                name: "idx_evento_outbox_estado_fecha",
                schema: "notificacion",
                table: "evento_outbox",
                columns: new[] { "estado", "fecha_creacion" });

            migrationBuilder.CreateIndex(
                name: "uq_idx_notificacion_envio_auto",
                schema: "notificacion",
                table: "notificacion_envio",
                columns: new[] { "id_empleado", "fecha_corte" },
                unique: true,
                filter: "origen = 'Automatico'");

            migrationBuilder.CreateIndex(
                name: "uq_notificacion_fecha_corte_periodo",
                schema: "notificacion",
                table: "notificacion_fecha_corte",
                columns: new[] { "anio", "mes", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_adm_empleado_contrato_empleado",
                schema: "administracion",
                table: "tbl_administracion_empleado_contrato",
                columns: new[] { "idempleado", "fechadesde" });

            migrationBuilder.CreateIndex(
                name: "idx_aut_audit_log_fecha",
                schema: "autenticacion",
                table: "tbl_autenticacion_audit_log",
                column: "fechacreacion");

            migrationBuilder.CreateIndex(
                name: "idx_aut_audit_log_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_audit_log",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_password_reset_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_password_reset",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_permiso_idmodulo",
                schema: "autenticacion",
                table: "tbl_autenticacion_permiso",
                column: "idmodulo");

            migrationBuilder.CreateIndex(
                name: "uq_autenticacion_permiso_codigo",
                schema: "autenticacion",
                table: "tbl_autenticacion_permiso",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_aut_refresh_token_exp",
                schema: "autenticacion",
                table: "tbl_autenticacion_refresh_token",
                column: "fechaexpiracion");

            migrationBuilder.CreateIndex(
                name: "idx_aut_refresh_token_familia",
                schema: "autenticacion",
                table: "tbl_autenticacion_refresh_token",
                column: "familiatoken");

            migrationBuilder.CreateIndex(
                name: "idx_aut_refresh_token_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_refresh_token",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_refresh_token_idsesion",
                schema: "autenticacion",
                table: "tbl_autenticacion_refresh_token",
                column: "idsesion");

            migrationBuilder.CreateIndex(
                name: "uq_autenticacion_refresh_token_hash",
                schema: "autenticacion",
                table: "tbl_autenticacion_refresh_token",
                column: "tokenhash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_autenticacion_rol_nombre",
                schema: "autenticacion",
                table: "tbl_autenticacion_rol",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_rol_permiso_idpermiso",
                schema: "autenticacion",
                table: "tbl_autenticacion_rol_permiso",
                column: "idpermiso");

            migrationBuilder.CreateIndex(
                name: "idx_tr_asignacion_proyecto_emp",
                schema: "time_report",
                table: "tbl_time_report_asignacion_proyecto",
                column: "idempleado");

            migrationBuilder.CreateIndex(
                name: "idx_tr_asignacion_proyecto_lider",
                schema: "time_report",
                table: "tbl_time_report_asignacion_proyecto",
                column: "idlider");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_asignacion_proyecto_idproyecto",
                schema: "time_report",
                table: "tbl_time_report_asignacion_proyecto",
                column: "idproyecto");

            migrationBuilder.AddForeignKey(
                name: "FK_Proyectos_tbl_administracion_catalogo_detalle_EstadoProyect~",
                table: "Proyectos",
                column: "EstadoProyectoId",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_catalogo_detalle",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_Proyectos_tbl_administracion_cliente_ClienteId",
                table: "Proyectos",
                column: "ClienteId",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_cliente",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_Proyectos_tbl_administracion_lider_LiderId",
                table: "Proyectos",
                column: "LiderId",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_lider",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_administracion_empleado_tbl_administracion_catalogo_det~",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idcausasalida",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_catalogo_detalle",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_administracion_empleado_tbl_administracion_catalogo_de~1",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idtiposalida",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_catalogo_detalle",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tbl_administracion_empleado_tbl_administracion_empleado_ide~",
                schema: "administracion",
                table: "tbl_administracion_empleado",
                column: "idempleadoreemplazo",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_empleado",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_sesion_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                column: "idusuario",
                principalSchema: "autenticacion",
                principalTable: "tbl_autenticacion_usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_token_blacklist_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                column: "idusuario",
                principalSchema: "autenticacion",
                principalTable: "tbl_autenticacion_usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_usuario_persona",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                column: "idpersona",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_persona",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_usuario_rol_asignado_por",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "asignadopor",
                principalSchema: "autenticacion",
                principalTable: "tbl_autenticacion_usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_usuario_rol_rol",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "idrol",
                principalSchema: "autenticacion",
                principalTable: "tbl_autenticacion_rol",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_usuario_rol_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "idusuario",
                principalSchema: "autenticacion",
                principalTable: "tbl_autenticacion_usuario",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proyectos_tbl_administracion_catalogo_detalle_EstadoProyect~",
                table: "Proyectos");

            migrationBuilder.DropForeignKey(
                name: "FK_Proyectos_tbl_administracion_cliente_ClienteId",
                table: "Proyectos");

            migrationBuilder.DropForeignKey(
                name: "FK_Proyectos_tbl_administracion_lider_LiderId",
                table: "Proyectos");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_administracion_empleado_tbl_administracion_catalogo_det~",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_administracion_empleado_tbl_administracion_catalogo_de~1",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropForeignKey(
                name: "FK_tbl_administracion_empleado_tbl_administracion_empleado_ide~",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_sesion_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion");

            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_token_blacklist_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_usuario_persona",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario");

            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_usuario_rol_asignado_por",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_usuario_rol_rol",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropForeignKey(
                name: "fk_autenticacion_usuario_rol_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropTable(
                name: "evento_outbox",
                schema: "notificacion");

            migrationBuilder.DropTable(
                name: "notificacion_envio",
                schema: "notificacion");

            migrationBuilder.DropTable(
                name: "notificacion_fecha_corte",
                schema: "notificacion");

            migrationBuilder.DropTable(
                name: "tbl_administracion_empleado_contrato",
                schema: "administracion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_audit_log",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_password_reset",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_refresh_token",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_rol_permiso",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_time_report_asignacion_proyecto",
                schema: "time_report");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_permiso",
                schema: "autenticacion");

            migrationBuilder.DropTable(
                name: "tbl_autenticacion_rol",
                schema: "autenticacion");

            migrationBuilder.DropPrimaryKey(
                name: "pk_autenticacion_usuario_rol",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropIndex(
                name: "IX_tbl_autenticacion_usuario_rol_asignadopor",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropIndex(
                name: "uq_autenticacion_usuario_email",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tbl_autenticacion_token_blacklist",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropIndex(
                name: "idx_aut_token_blacklist_exp",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropIndex(
                name: "IX_tbl_autenticacion_token_blacklist_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropIndex(
                name: "uq_autenticacion_token_blacklist_jti",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropIndex(
                name: "IX_tbl_administracion_empleado_idcausasalida",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropIndex(
                name: "IX_tbl_administracion_empleado_idempleadoreemplazo",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropIndex(
                name: "IX_tbl_administracion_empleado_idtiposalida",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropIndex(
                name: "IX_Proyectos_ClienteId",
                table: "Proyectos");

            migrationBuilder.DropIndex(
                name: "IX_Proyectos_EstadoProyectoId",
                table: "Proyectos");

            migrationBuilder.DropIndex(
                name: "IX_Proyectos_LiderId",
                table: "Proyectos");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Lideres",
                table: "Lideres");

            migrationBuilder.DropColumn(
                name: "asignadoen",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropColumn(
                name: "asignadopor",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropColumn(
                name: "fechaexpiracion",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol");

            migrationBuilder.DropColumn(
                name: "bloqueadohasta",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario");

            migrationBuilder.DropColumn(
                name: "emailverificado",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario");

            migrationBuilder.DropColumn(
                name: "intentosfallidos",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario");

            migrationBuilder.DropColumn(
                name: "fecharevocado",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropColumn(
                name: "idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist");

            migrationBuilder.DropColumn(
                name: "fechaexpiracion",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion");

            migrationBuilder.DropColumn(
                name: "revocadorazon",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion");

            migrationBuilder.DropColumn(
                name: "aniosexperiencia",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropColumn(
                name: "comentariosalida",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropColumn(
                name: "idcausasalida",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropColumn(
                name: "idempleadoreemplazo",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropColumn(
                name: "idtiposalida",
                schema: "administracion",
                table: "tbl_administracion_empleado");

            migrationBuilder.DropColumn(
                name: "apellidos",
                schema: "administracion",
                table: "tbl_administracion_cliente");

            migrationBuilder.DropColumn(
                name: "nombres",
                schema: "administracion",
                table: "tbl_administracion_cliente");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "EstadoProyectoId",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "FechaFinEspera",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "FechaFinReal",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "FechaInicioEspera",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "FechaInicioReal",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "Fechamodificacion",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "IdCliente",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "IdEstadoProyecto",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "IdLider",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "Ipcreacion",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "Ipmodificacion",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "LiderId",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "Observacion",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "Usuariocreacion",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "Usuariomodificacion",
                table: "Proyectos");

            migrationBuilder.RenameTable(
                name: "Lideres",
                newName: "Lider");

            migrationBuilder.RenameColumn(
                name: "jti",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                newName: "token");

            migrationBuilder.RenameColumn(
                name: "ultimaactividad",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                newName: "horaingreso");

            migrationBuilder.RenameColumn(
                name: "revocadofecha",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                newName: "horasalida");

            migrationBuilder.RenameColumn(
                name: "idmodulopadre",
                schema: "autenticacion",
                table: "tbl_autenticacion_modulo",
                newName: "idsubmodulo");

            migrationBuilder.RenameColumn(
                name: "Fechacreacion",
                table: "Proyectos",
                newName: "FechaCreacion");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .Annotation("Npgsql:PostgresExtension:uuid-ossp", ",,")
                .OldAnnotation("Npgsql:Enum:autenticacion.audit_evento_enum", "LOGIN_OK,LOGIN_FAIL,LOGOUT,REFRESH_OK,REFRESH_FAIL,TOKEN_REVOKE,PASSWORD_CHANGE,USER_CREATE,USER_BLOCK")
                .OldAnnotation("Npgsql:Enum:autenticacion.revocacion_razon_enum", "LOGOUT,ADMIN_REVOKE,ADMIN_REVOKE_ALL,PASSWORD_CHANGE,SECURITY_BREACH,EXPIRED,SESSION_LIMIT,TOKEN_REUSED,TOKEN_REVOKED,TOKEN_INVALID,TOKEN_EXPIRED,IP_MISMATCH,USER_AGENT_MISMATCH,SESSION_IDLE_TIMEOUT,SESSION_EXPIRED,REVOKE")
                .OldAnnotation("Npgsql:PostgresExtension:pgcrypto", ",,")
                .OldAnnotation("Npgsql:PostgresExtension:uuid-ossp", ",,");

            migrationBuilder.AddColumn<int>(
                name: "idlider",
                schema: "time_report",
                table: "tbl_time_report_proyecto",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "id",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AlterColumn<string>(
                name: "email",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<bool>(
                name: "estaactivo",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "id",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.AlterColumn<bool>(
                name: "estaactiva",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "agenteusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "tokensesion",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EsInterno",
                schema: "administracion",
                table: "tbl_administracion_lider",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                table: "Proyectos",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "pk_autenticacion_usuario_rol",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_autenticacion_token_blacklist",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Lider",
                table: "Lider",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ConfiguracionesSistema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionesSistema", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_aplicacion",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    descripcion = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    nombreaplicacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    urlbase = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    token = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_autenticacion_inventario_token", x => x.id);
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
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                name: "tbl_autenticacion_privilegio_usuario",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idprivilegio = table.Column<int>(type: "integer", nullable: false),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                name: "tbl_autenticacion_rol_modulo",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idmodulo = table.Column<int>(type: "integer", nullable: false),
                    idrol = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    puedecrear = table.Column<bool>(type: "boolean", nullable: true),
                    puedeeditar = table.Column<bool>(type: "boolean", nullable: true),
                    puedeeliminar = table.Column<bool>(type: "boolean", nullable: true),
                    puedever = table.Column<bool>(type: "boolean", nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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
                name: "tbl_autenticacion_usuario_modulo",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idmodulo = table.Column<int>(type: "integer", nullable: false),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    puedecrear = table.Column<bool>(type: "boolean", nullable: true),
                    puedeeditar = table.Column<bool>(type: "boolean", nullable: true),
                    puedeeliminar = table.Column<bool>(type: "boolean", nullable: true),
                    puedever = table.Column<bool>(type: "boolean", nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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
                name: "tbl_time_report_empleado_proyecto",
                schema: "time_report",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idempleado = table.Column<int>(type: "integer", nullable: true),
                    idproyecto = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    costoporhora = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    fechaasignacion = table.Column<DateOnly>(type: "date", nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    fechafinasignacion = table.Column<DateOnly>(type: "date", nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    horasasignadas = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    idproveedor = table.Column<int>(type: "integer", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    rolasignado = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Descripcion = table.Column<string>(type: "text", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tbl_autenticacion_menu",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idaplicacion = table.Column<int>(type: "integer", nullable: true),
                    idmenupadre = table.Column<int>(type: "integer", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    icono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    ipmodificacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    nombremenu = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ordenvisualizacion = table.Column<int>(type: "integer", nullable: true),
                    rutamenu = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
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
                name: "tbl_autenticacion_sesion_app",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idaplicacion = table.Column<int>(type: "integer", nullable: false),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    fechaexpiracion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    tokenapp = table.Column<string>(type: "text", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                    idaplicacion = table.Column<int>(type: "integer", nullable: false),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                name: "tbl_autenticacion_menu_rol",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idmenu = table.Column<int>(type: "integer", nullable: false),
                    idrol = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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
                name: "tbl_autenticacion_menu_usuario",
                schema: "autenticacion",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    idmenu = table.Column<int>(type: "integer", nullable: false),
                    idusuario = table.Column<int>(type: "integer", nullable: false),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ipcreacion = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "idx_tr_proyecto_lider",
                schema: "time_report",
                table: "tbl_time_report_proyecto",
                column: "idlider");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_autenticacion_usuario_rol_idusuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "idusuario");

            migrationBuilder.CreateIndex(
                name: "idx_aut_token_blacklist_token",
                schema: "autenticacion",
                table: "tbl_autenticacion_token_blacklist",
                column: "token");

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
                name: "idx_tr_empleado_proyecto_emp",
                schema: "time_report",
                table: "tbl_time_report_empleado_proyecto",
                column: "idempleado");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_time_report_empleado_proyecto_idproyecto",
                schema: "time_report",
                table: "tbl_time_report_empleado_proyecto",
                column: "idproyecto");

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_sesion_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_sesion",
                column: "idusuario",
                principalSchema: "autenticacion",
                principalTable: "tbl_autenticacion_usuario",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_usuario_persona",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario",
                column: "idpersona",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_persona",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_usuario_rol_rol",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "idrol",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_catalogo_detalle",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_autenticacion_usuario_rol_usuario",
                schema: "autenticacion",
                table: "tbl_autenticacion_usuario_rol",
                column: "idusuario",
                principalSchema: "autenticacion",
                principalTable: "tbl_autenticacion_usuario",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_time_report_proyecto_lider",
                schema: "time_report",
                table: "tbl_time_report_proyecto",
                column: "idlider",
                principalSchema: "administracion",
                principalTable: "tbl_administracion_lider",
                principalColumn: "id");
        }
    }
}
