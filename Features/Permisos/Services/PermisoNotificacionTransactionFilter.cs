using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Permisos.Services;

// Mantiene decisión, revisión, campana y correos pendientes en una sola transacción.
public sealed class PermisoNotificacionTransactionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var path = http.Request.Path.Value ?? "";
        var transactional = HttpMethods.IsPost(http.Request.Method) &&
            (path.EndsWith("/decision-lider") || path.EndsWith("/decision-rrhh") ||
             path.EndsWith("/reenviar-enlace") || path.EndsWith("/decision"));
        if (!transactional) return await next(context);
        var db = http.RequestServices.GetRequiredService<ApplicationDbContext>();
        var ct = http.RequestAborted;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (int.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id))
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM time_report.tbl_time_report_solicitud WHERE id = {id} FOR UPDATE", ct);
        else if (http.Request.RouteValues["token"] is string token)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM time_report.tbl_time_report_solicitud WHERE token_externo = {token} FOR UPDATE", ct);
        var result = await next(context);
        if (result is IStatusCodeHttpResult status && status.StatusCode >= 400)
            await tx.RollbackAsync(ct);
        else
            await tx.CommitAsync(ct);
        return result;
    }
}
