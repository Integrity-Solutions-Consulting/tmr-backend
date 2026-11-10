using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.IntegracionRps;

// Endpoints de solo lectura que consume rps-backend para sincronizar su copia local.
// Se autentican con API key (no hay usuario de por medio) y nunca exponen salario ni costos.
public static class IntegracionRpsEndpoints
{
    // CodigoValor del catálogo EMP de las asociaciones que forman parte de RPS.
    private static readonly string[] AsociacionesRps = ["RPS", "RISC"];

    public static void MapIntegracionRpsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/integracion/rps")
            .WithTags("Integración RPS")
            .AllowAnonymous()
            .AddEndpointFilter(async (context, next) =>
            {
                var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var esperada = configuration["IntegracionRps:ApiKey"];
                var recibida = context.HttpContext.Request.Headers["X-Api-Key"].ToString();

                // Sin clave configurada se rechaza todo: la integración queda cerrada por defecto.
                if (string.IsNullOrEmpty(esperada) || !CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(esperada), Encoding.UTF8.GetBytes(recibida)))
                    return Results.Unauthorized();

                return await next(context);
            });

        group.MapGet("/colaboradores", async (ApplicationDbContext db, CancellationToken ct) =>
            Results.Ok(await db.TblAdministracionEmpleados.AsNoTracking()
                .Where(e => e.IdempresacatalogoNavigation != null
                    && AsociacionesRps.Contains(e.IdempresacatalogoNavigation.Codigovalor))
                .Select(e => new
                {
                    e.Id,
                    CodigoEmpleado = e.Codigoempleado,
                    e.IdpersonaNavigation.Nombres,
                    e.IdpersonaNavigation.Apellidos,
                    Identificacion = e.IdpersonaNavigation.Numeroidentificacion,
                    Correo = e.Emailcorporativo ?? e.IdpersonaNavigation.Email,
                    e.Activo
                })
                .ToListAsync(ct)));

        group.MapGet("/proyectos", async (ApplicationDbContext db, CancellationToken ct) =>
            Results.Ok(await db.TblTimeReportProyectos.AsNoTracking()
                .Select(p => new
                {
                    p.Id,
                    p.Codigo,
                    p.Nombre,
                    Cliente = p.IdclienteNavigation != null
                        ? p.IdclienteNavigation.Nombrecomercial ?? p.IdclienteNavigation.Razonsocial
                        : null,
                    p.Activo
                })
                .ToListAsync(ct)));

        // Solo la versión vigente de cada asignación: Activo = false es una versión anterior, no una salida
        // (ver ProyectosEndpoints.GuardarAsignaciones). El Id cambia en cada guardado, por eso no se expone.
        group.MapGet("/asignaciones", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            var asignaciones = await db.TblTimeReportAsignacionProyectos.AsNoTracking()
                .Where(a => a.Activo
                    && a.Idempleado != null
                    && a.IdempleadoNavigation!.IdempresacatalogoNavigation != null
                    && AsociacionesRps.Contains(a.IdempleadoNavigation.IdempresacatalogoNavigation.Codigovalor))
                .Select(a => new { a.Idempleado, a.Idproyecto, a.Fechaasignacion, a.Fechafinasignacion, a.Estadoasignacion })
                .ToListAsync(ct);

            // Fechas como texto ISO: el DateOnlyJsonConverter global de TMR las escribiría como dd-MM-yyyy.
            return Results.Ok(asignaciones.Select(a => new
            {
                IdEmpleado = a.Idempleado!.Value,
                IdProyecto = a.Idproyecto,
                FechaInicio = a.Fechaasignacion?.ToString("yyyy-MM-dd"),
                FechaSalida = a.Fechafinasignacion?.ToString("yyyy-MM-dd"),
                EstadoAsignacion = a.Estadoasignacion
            }));
        });
    }
}
