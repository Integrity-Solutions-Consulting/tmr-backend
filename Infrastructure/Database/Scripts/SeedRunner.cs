using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Seeders;

namespace tmr_backend.Infrastructure.Database.Scripts;

/// <summary>
/// Script de ejemplo para ejecutar el seeder manualmente desde código o la CLI
/// 
/// Uso:
/// 1. Desde Program.cs: await SeedRunner.RunAsync(app);
/// 2. Desde otro servicio: var runner = new SeedRunner(); await runner.RunAsync(dbContext);
/// </summary>
public static class SeedRunner
{
    /// <summary>
    /// Ejecuta el seeder usando la aplicación web
    /// </summary>
    public static async Task RunAsync(WebApplication app)
    {
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await RunAsync(dbContext);
        }
    }

    /// <summary>
    /// Ejecuta el seeder usando un DbContext directo
    /// </summary>
    public static async Task RunAsync(ApplicationDbContext dbContext)
    {
        try
        {
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("  🌱 EJECUTANDO SEEDER DE PLANTILLA");
            Console.WriteLine(new string('=', 50) + "\n");

            var seeder = new SeedTemplateSeeder(dbContext);
            await seeder.ExecuteAsync();

            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("  ✅ SEEDER COMPLETADO EXITOSAMENTE");
            Console.WriteLine(new string('=', 50) + "\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("  ❌ ERROR EN SEEDER");
            Console.WriteLine(new string('=', 50));
            Console.WriteLine($"\n  Detalle: {ex.Message}");
            Console.WriteLine($"  Stack: {ex.StackTrace}\n");
            throw;
        }
    }
}
