using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Seeders;

namespace tmr_backend.Infrastructure.Extensions;

/// <summary>
/// Extensión para aplicar seeders a la base de datos
/// </summary>
public static class DatabaseSeedingExtensions
{
    /// <summary>
    /// Aplica los seeders de plantilla a la base de datos
    /// Uso: app.SeedTemplateData().Wait();
    /// </summary>
    public static async Task SeedTemplateDataAsync(this IApplicationBuilder app)
    {
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("🌱 INICIANDO PROCESO DE SEEDING");
                Console.WriteLine("═══════════════════════════════════════════");

                var seeder = new SeedTemplateSeeder(db);
                await seeder.ExecuteAsync();

                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("✅ SEEDING COMPLETADO");
                Console.WriteLine("═══════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine("═══════════════════════════════════════════");
                Console.WriteLine("❌ ERROR DURANTE SEEDING");
                Console.WriteLine($"Detalle: {ex.Message}");
                Console.WriteLine("═══════════════════════════════════════════");
                throw;
            }
        }
    }
}
