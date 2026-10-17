using tmr_backend.Features.HealthCheck.DTOs;
using tmr_backend.Features.HealthCheck.Services;

namespace tmr_backend.Features.HealthCheck.Endpoints;

public static class HealthCheckEndpoints
{
    public static void MapHealthCheckEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/health")
            .WithName("Health");

        group.MapGet("/check", GetHealthCheck)
            .WithName("Health Check")
            .WithDescription("Valida la conexión a la base de datos y el estado de las tablas principales")
            .Produces(200)
            .Produces(503);

        // Liveness probe endpoint for orchestrators (Kubernetes, Docker, etc.)
        app.MapGet("/health/live", GetHealthCheckLive)
            .WithName("Health Live")
            .WithDescription("Verificación rápida de disponibilidad de la aplicación")
            .Produces(200)
            .WithOpenApi()
            .AllowAnonymous();

        app.MapGet("/health/ready", GetHealthCheckReady)
            .WithName("Health Ready")
            .Produces(200)
            .Produces(503)
            .WithOpenApi()
            .AllowAnonymous();
    }

    private static async Task<IResult> GetHealthCheck(IHealthCheckService healthCheckService)
    {
        var health = await healthCheckService.CheckHealthAsync();

        if (health.Status == "Healthy")
        {
            return Results.Ok(health);
        }

        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    private static IResult GetHealthCheckLive()
    {
        return Results.Ok(new HealthCheckLiveResponse
        {
            Status = "Healthy",
            Message = "Application is running"
        });
    }

    private static async Task<IResult> GetHealthCheckReady(IHealthCheckService healthCheckService)
    {
        var health = await healthCheckService.CheckReadyAsync();

        if (health.Status == "Healthy")
        {
            return Results.Ok(health);
        }

        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}
