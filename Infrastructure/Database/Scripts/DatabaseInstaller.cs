using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using tmr_backend.Migrations;
using Npgsql;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Database.Seeders;
using tmr_backend.Infrastructure.Security;

namespace tmr_backend.Infrastructure.Database.Scripts;

/// <summary>Etapas de instalación y actualización del esquema y de los datos iniciales.</summary>
public static class DatabaseInstaller
{
    public static async Task RunAsync(string command, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Configure ConnectionStrings__DefaultConnection para esta operación.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        // Serializa instaladores sin bloquear el arranque normal ni otros servicios.
        await new NpgsqlCommand("SELECT pg_advisory_lock(846720261017)", connection).ExecuteNonQueryAsync();
        try
        {
            var migrationHistorySchema = MigrationHistory.GetMigrationsHistorySchema(configuration);
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable(MigrationHistory.TableName, migrationHistorySchema));
            var options = optionsBuilder.Options;
            await using var db = new ApplicationDbContext(options);
            switch (command)
            {
                case "--adopt-baseline":
                    await CheckMigrationHistoryAsync(connection, migrationHistorySchema);
                    await BaselineAdoption.RegisterAsync(db, connection, migrationHistorySchema);
                    break;
                case "--prepare-database":
                case "--apply-migrations":
                    await CheckMigrationHistoryAsync(connection, migrationHistorySchema);
                    await EnsureDatabaseReadyAsync(db, connection);
                    if (command == "--apply-migrations")
                        await db.Database.MigrateAsync();
                    else if (!(await db.Database.GetAppliedMigrationsAsync()).Contains(BaselineProduccion.Id))
                        await db.GetService<IMigrator>().MigrateAsync(BaselineProduccion.Id);
                    break;
                case "--initialize-data":
                    await using (var transaction = await db.Database.BeginTransactionAsync())
                    {
                        await ProveedorPermissionsSeeder.EnsureAsync(db);
                        await ActualizacionDatosPermissionsSeeder.EnsureAsync(db);
                        await CreateAdministratorAsync(db, configuration);
                        await transaction.CommitAsync();
                    }
                    break;
                case "--verify-database":
                    await VerifyAsync(db, connection);
                    break;
                default:
                    throw new ArgumentException("Operación de instalación desconocida.");
            }
            Console.WriteLine($"{command}: completado.");
        }
        finally
        {
            await new NpgsqlCommand("SELECT pg_advisory_unlock(846720261017)", connection).ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureDatabaseReadyAsync(ApplicationDbContext db, NpgsqlConnection connection)
    {
        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        if (applied.Contains(BaselineProduccion.Id))
            return;

        const string sql = """
            SELECT EXISTS (SELECT FROM pg_catalog.pg_tables
            WHERE schemaname IN ('administracion','auditoria','autenticacion','inventario','inventory','notificacion','time_report')
              AND tablename <> '__EFMigrationsHistory')
            """;
        await using var check = new NpgsqlCommand(sql, connection);
        if (applied.Length != 0 || (bool)(await check.ExecuteScalarAsync())!)
            throw new InvalidOperationException("La base contiene TMR. Verifique el respaldo y ejecute --adopt-baseline antes de migrar.");
    }

    private static async Task CheckMigrationHistoryAsync(NpgsqlConnection connection, string schema)
    {
        const string sql = """
            SELECT schemaname FROM pg_catalog.pg_tables
            WHERE tablename = @table AND schemaname <> @schema
            ORDER BY schemaname LIMIT 1
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("table", MigrationHistory.TableName);
        command.Parameters.AddWithValue("schema", schema);
        if (await command.ExecuteScalarAsync() is string otherSchema)
            throw new InvalidOperationException(
                $"Hay un historial EF en '{otherSchema}' y el instalador está configurado para '{schema}'. Revise Database:MigrationsHistorySchema antes de aplicar migraciones.");
    }

    private static async Task CreateAdministratorAsync(ApplicationDbContext db, IConfiguration configuration)
    {
        var keys = new[] { "AdminEmail", "AdminPassword", "AdminIdentification", "AdminName" };
        var values = keys.Select(x => configuration[$"Bootstrap:{x}"]).ToArray();
        if (values.All(string.IsNullOrWhiteSpace))
        {
            if (!await db.TblAutenticacionUsuarios.AnyAsync())
                throw new InvalidOperationException("Base sin usuarios. Proporcione Bootstrap__AdminEmail, Bootstrap__AdminPassword, Bootstrap__AdminIdentification y Bootstrap__AdminName.");
            return;
        }
        if (values.Any(string.IsNullOrWhiteSpace) || values[1]!.Length < 12)
            throw new InvalidOperationException("Complete las cuatro variables Bootstrap__Admin*. La contraseña debe tener al menos 12 caracteres.");
        var email = values[0]!.Trim().ToLowerInvariant();
        // Una segunda ejecución no cambia contraseñas, roles ni cuentas existentes.
        if (await db.TblAutenticacionUsuarios.AnyAsync(x => x.Email.ToLower() == email))
            return;
        if (await db.TblAutenticacionUsuarios.AnyAsync())
            throw new InvalidOperationException("El administrador inicial solo puede crearse en una instalación sin usuarios.");
        var role = await db.TblAutenticacionRols.SingleAsync(x => x.Nombre.ToUpper() == "ADMINISTRADOR" && x.Activo);
        var now = DateTime.UtcNow;
        var person = new TblAdministracionPersona
        {
            Numeroidentificacion = values[2]!, Nombres = values[3]!, Apellidos = "",
            Tipopersona = "NATURAL", Email = email, Activo = true,
            Usuariocreacion = "INSTALLER", Fechacreacion = now, Ipcreacion = "127.0.0.1"
        };
        var user = new TblAutenticacionUsuario
        {
            IdpersonaNavigation = person, Email = email,
            Hashpassword = new PasswordHasher().Hash(values[1]!), Emailverificado = true,
            Debecambiarpassword = true, Activo = true, Usuariocreacion = "INSTALLER",
            Fechacreacion = now, Ipcreacion = "127.0.0.1"
        };
        db.TblAutenticacionUsuarioRols.Add(new TblAutenticacionUsuarioRol
        {
            IdusuarioNavigation = user, Idrol = role.Id, Asignadoen = now, Activo = true,
            Usuariocreacion = "INSTALLER", Fechacreacion = now, Ipcreacion = "127.0.0.1"
        });
        await db.SaveChangesAsync();
    }

    private static async Task VerifyAsync(ApplicationDbContext db, NpgsqlConnection connection)
    {
        // La imagen debe soportar la cultura utilizada por las notificaciones.
        _ = System.Globalization.CultureInfo.GetCultureInfo("es-ES");
        // Comprueba todas las tablas/columnas mapeadas en los cinco esquemas TMR.
        // Los modelos públicos de ejemplo no forman parte del esquema SQL oficial.
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        var errors = new List<string>();
        foreach (var entity in db.Model.GetEntityTypes().Where(x => x.GetSchema() is not null))
        {
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            var columns = entity.GetProperties().Select(x => Quote(x.GetColumnName(table)!)).Distinct();
            await using var query = new NpgsqlCommand($"SELECT {string.Join(',', columns)} FROM {Quote(table.Schema!)}.{Quote(table.Name)} LIMIT 0", connection);
            try { await query.ExecuteNonQueryAsync(); }
            catch (PostgresException exception) when (exception.SqlState is "42703" or "42P01")
            {
                errors.Add($"{table.Schema}.{table.Name}: {exception.MessageText}");
            }
        }
        if (errors.Count > 0)
            throw new InvalidOperationException("Esquema incompleto: " + string.Join("; ", errors));
        if (await db.TblAutenticacionPermisos.CountAsync(x => x.Activo &&
            new[] { "PROVEEDORES_READ", "PROVEEDORES_CREATE", "PROVEEDORES_UPDATE", "PROVEEDORES_DELETE" }.Contains(x.Codigo)) != 4)
            throw new InvalidOperationException("Faltan permisos de Proveedores. Ejecute --initialize-data.");
        var requiredPermissionCodes = new[] { "COLABORADORES_DATOS_REVISAR" };
        var requiredPermissions = await db.TblAutenticacionPermisos
            .Where(x => x.Activo && requiredPermissionCodes.Contains(x.Codigo))
            .Select(x => x.Codigo).ToListAsync();
        if (requiredPermissions.Count != requiredPermissionCodes.Length)
            throw new InvalidOperationException("Faltan permisos requeridos del módulo de actualización de colaboradores.");
        var assignedReviewer = await db.TblAutenticacionRolPermisos.AnyAsync(x =>
            x.Activo && x.IdpermisoNavigation.Activo && x.IdpermisoNavigation.Codigo == "COLABORADORES_DATOS_REVISAR" &&
            db.TblAutenticacionRols.Any(role => role.Id == x.Idrol && role.Activo) &&
            db.TblAutenticacionUsuarioRols.Any(userRole => userRole.Idrol == x.Idrol && userRole.Activo));
        if (!assignedReviewer ||
            !await db.TblAdministracionCatalogos.AnyAsync() || !await db.TblTimeReportTipoActividads.AnyAsync())
            throw new InvalidOperationException("Instalación sin roles, administrador o catálogos indispensables.");
        Console.WriteLine("Esquema mapeado, catálogos, administrador y permisos verificados.");
    }
}
