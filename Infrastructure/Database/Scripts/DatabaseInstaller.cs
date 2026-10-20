using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Database.Seeders;
using tmr_backend.Infrastructure.Security;

namespace tmr_backend.Infrastructure.Database.Scripts;

/// <summary>Etapas explícitas de instalación; no se invoca durante el arranque web.</summary>
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
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options;
            await using var db = new ApplicationDbContext(options);
            switch (command)
            {
                case "--prepare-database":
                    await PrepareAsync(connection);
                    break;
                case "--initialize-data":
                    await using (var transaction = await db.Database.BeginTransactionAsync())
                    {
                        await ProveedorPermissionsSeeder.EnsureAsync(db);
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

    private static async Task PrepareAsync(NpgsqlConnection connection)
    {
        // No adoptar automáticamente bases históricas sin historial: evita repetir DDL o seeds.
        const string check = """
            SELECT to_regclass('public.tmr_installation_history') IS NULL
              AND EXISTS (SELECT 1 FROM information_schema.tables
                WHERE table_schema IN ('administracion','autenticacion','inventario','time_report','auditoria'))
            """;
        if ((bool)(await new NpgsqlCommand(check, connection).ExecuteScalarAsync())!)
            throw new InvalidOperationException("La base ya contiene TMR sin historial de instalación. No se modifica; revise su actualización manualmente.");

        await new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS public.tmr_installation_history (
                script text PRIMARY KEY, sha256 text NOT NULL, applied_at timestamptz NOT NULL DEFAULT now())
            """, connection).ExecuteNonQueryAsync();

        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(x => x.Contains(".Scripts_DB.") && x.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToArray();
        if (resources.Length != 16)
            throw new InvalidOperationException("El paquete debe contener los scripts 01–17, excluyendo 06.");

        foreach (var resource in resources)
        {
            var name = resource[(resource.IndexOf(".Scripts_DB.", StringComparison.Ordinal) + 12)..];
            using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!, Encoding.UTF8);
            var sql = (await reader.ReadToEndAsync()).Replace("\r\n", "\n");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
            await using var existing = new NpgsqlCommand("SELECT sha256 FROM public.tmr_installation_history WHERE script = @name", connection);
            existing.Parameters.AddWithValue("name", name);
            var previous = await existing.ExecuteScalarAsync();
            if (previous is string previousHash)
            {
                if (previousHash != hash)
                    throw new InvalidOperationException($"El script aplicado {name} cambió. Agregue una nueva versión; no lo reaplique.");
                Console.WriteLine($"Ya aplicado: {name}");
                continue;
            }

            // Los scripts históricos tienen BEGIN/COMMIT propios. Una transacción externa
            // incluye también el historial; no se eliminan bloques PL/pgSQL BEGIN ... END.
            sql = Regex.Replace(sql, @"(?m)^\s*(BEGIN|COMMIT);[ \t]*$", "");
            await using var transaction = await connection.BeginTransactionAsync();
            await using var script = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 180 };
            await script.ExecuteNonQueryAsync();
            await using var record = new NpgsqlCommand("INSERT INTO public.tmr_installation_history(script,sha256) VALUES (@name,@hash)", connection, transaction);
            record.Parameters.AddWithValue("name", name);
            record.Parameters.AddWithValue("hash", hash);
            await record.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            Console.WriteLine($"Aplicado: {name}");
        }
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
        var role = await db.TblAutenticacionRols.SingleAsync(x => x.Nombre == "Administrador" && x.Activo);
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
        var admin = await db.TblAutenticacionRols.SingleAsync(x => x.Nombre == "Administrador" && x.Activo);
        if (await db.TblAutenticacionRolPermisos.CountAsync(x => x.Idrol == admin.Id && x.Activo &&
            x.IdpermisoNavigation.Codigo.StartsWith("PROVEEDORES_")) < 4 ||
            !await db.TblAutenticacionUsuarioRols.AnyAsync(x => x.Idrol == admin.Id && x.Activo) ||
            !await db.TblAdministracionCatalogos.AnyAsync() || !await db.TblTimeReportTipoActividads.AnyAsync())
            throw new InvalidOperationException("Instalación sin roles, administrador o catálogos indispensables.");
        Console.WriteLine("Esquema mapeado, catálogos, administrador y permisos verificados.");
    }
}
