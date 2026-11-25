namespace tmr_backend.Infrastructure.Database;

internal static class MigrationHistory
{
    public const string TableName = "__EFMigrationsHistory";

    public static string GetMigrationsHistorySchema(IConfiguration configuration) =>
        configuration["Database:MigrationsHistorySchema"] ?? "public";
}
