using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Proveedores.Endpoints;

/// <summary>API for the existing inventario.tbl_inventario_proveedor table.</summary>
public static class ProveedoresEndpoints
{
    public static void MapProveedoresEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/proveedores").WithTags("Proveedores")
            .RequireAuthorization("PROVEEDORES_READ");

        group.MapGet("/", async (string? busqueda, int? tipoProveedorId, bool? activo, ApplicationDbContext db, CancellationToken ct) =>
        {
            var query = db.TblInventarioProveedors.AsNoTracking().Include(x => x.IdtipoproveedorNavigation).AsQueryable();
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var term = busqueda.Trim();
                query = query.Where(x => EF.Functions.ILike(x.Nombreproveedor, $"%{term}%") ||
                    (x.Ruc != null && EF.Functions.ILike(x.Ruc, $"%{term}%")) ||
                    (x.Email != null && EF.Functions.ILike(x.Email, $"%{term}%")));
            }
            if (tipoProveedorId.HasValue) query = query.Where(x => x.Idtipoproveedor == tipoProveedorId);
            if (activo.HasValue) query = query.Where(x => x.Activo == activo);
            return Results.Ok(await query.OrderByDescending(x => x.Activo).ThenBy(x => x.Nombreproveedor)
                .Select(x => ToResponse(x)).ToListAsync(ct));
        });

        group.MapGet("/tipos", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            // The schema only declares a FK to catalog details; it does not declare a dedicated
            // supplier-type catalog code. Therefore use the real types already associated to
            // suppliers instead of guessing a catalog (TPR is project types in the seed script).
            return Results.Ok(await db.TblAdministracionCatalogoDetalles.AsNoTracking()
                .Where(x => x.Activo && db.TblInventarioProveedors.Any(p => p.Idtipoproveedor == x.Id))
                .OrderBy(x => x.Orden).ThenBy(x => x.Valor)
                .Select(x => new { x.Id, x.Valor }).ToListAsync(ct));
        });

        group.MapGet("/{id:int}", async (int id, ApplicationDbContext db, CancellationToken ct) =>
        {
            var item = await db.TblInventarioProveedors.AsNoTracking().Include(x => x.IdtipoproveedorNavigation)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound();
            var facturas = await db.TblInventarioFacturas.AsNoTracking().CountAsync(x => x.Idproveedor == id, ct);
            return Results.Ok(new { proveedor = ToResponse(item), facturasRelacionadas = facturas });
        });

        group.MapPost("/", async (ProveedorRequest request, ApplicationDbContext db, ICurrentUserService current, HttpContext context, CancellationToken ct) =>
        {
            var error = await Validate(request, null, db, ct); if (error is not null) return error;
            var supplier = CreateEntity(request, current, context);
            db.TblInventarioProveedors.Add(supplier); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/proveedores/{supplier.Id}", new { supplier.Id });
        }).RequireAuthorization("PROVEEDORES_CREATE");

        group.MapPut("/{id:int}", async (int id, ProveedorRequest request, ApplicationDbContext db, ICurrentUserService current, HttpContext context, CancellationToken ct) =>
        {
            var supplier = await db.TblInventarioProveedors.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (supplier is null) return Results.NotFound();
            var error = await Validate(request, id, db, ct); if (error is not null) return error;
            supplier.Nombreproveedor = request.NombreProveedor.Trim(); supplier.Ruc = Clean(request.Ruc); supplier.Email = Clean(request.Email);
            supplier.Telefono = Clean(request.Telefono); supplier.Direccion = Clean(request.Direccion); supplier.Idtipoproveedor = request.IdTipoProveedor;
            supplier.Activo = request.Activo; supplier.Usuariomodificacion = User(current); supplier.Fechamodificacion = DateTime.UtcNow;
            supplier.Ipmodificacion = Ip(context); await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("PROVEEDORES_UPDATE");

        group.MapDelete("/{id:int}", async (int id, ApplicationDbContext db, ICurrentUserService current, HttpContext context, CancellationToken ct) =>
        {
            var supplier = await db.TblInventarioProveedors.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (supplier is null) return Results.NotFound();
            if (await db.TblInventarioFacturas.AnyAsync(x => x.Idproveedor == id, ct))
                return Results.Conflict(new ProblemDetails { Title = "No se puede eliminar", Detail = "El proveedor tiene facturas relacionadas." });
            supplier.Activo = false; supplier.Usuariomodificacion = User(current); supplier.Fechamodificacion = DateTime.UtcNow; supplier.Ipmodificacion = Ip(context);
            await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("PROVEEDORES_DELETE");
    }

    private static async Task<IResult?> Validate(ProveedorRequest request, int? id, ApplicationDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NombreProveedor) || request.NombreProveedor.Trim().Length > 150)
            return Results.BadRequest(new ProblemDetails { Title = "Nombre inválido", Detail = "El nombre es obligatorio y admite hasta 150 caracteres." });
        if (request.Ruc?.Length > 13 || request.Email?.Length > 100 || request.Telefono?.Length > 20 || request.Direccion?.Length > 255)
            return Results.BadRequest(new ProblemDetails { Title = "Datos inválidos", Detail = "Uno o más campos supera su longitud permitida." });
        if (!string.IsNullOrWhiteSpace(request.Email) && !System.Net.Mail.MailAddress.TryCreate(request.Email, out _))
            return Results.BadRequest(new ProblemDetails { Title = "Correo inválido", Detail = "Ingrese una dirección de correo válida." });
        if (request.IdTipoProveedor.HasValue && !await db.TblAdministracionCatalogoDetalles.AnyAsync(x => x.Id == request.IdTipoProveedor && x.Activo, ct))
            return Results.BadRequest(new ProblemDetails { Title = "Tipo inválido", Detail = "El tipo de proveedor seleccionado no existe o está inactivo." });
        var ruc = Clean(request.Ruc);
        if (ruc is not null && await db.TblInventarioProveedors.AnyAsync(x => x.Ruc == ruc && x.Id != id, ct))
            return Results.Conflict(new ProblemDetails { Title = "RUC duplicado", Detail = "Ya existe un proveedor con ese RUC." });
        return null;
    }
    private static object ToResponse(TblInventarioProveedor x) => new { x.Id, x.Nombreproveedor, x.Ruc, x.Email, x.Telefono, x.Direccion, x.Activo, x.Idtipoproveedor, TipoProveedor = x.IdtipoproveedorNavigation == null ? null : x.IdtipoproveedorNavigation.Valor, x.Fechacreacion };
    private static TblInventarioProveedor CreateEntity(ProveedorRequest x, ICurrentUserService current, HttpContext context) => new() { Nombreproveedor = x.NombreProveedor.Trim(), Ruc = Clean(x.Ruc), Email = Clean(x.Email), Telefono = Clean(x.Telefono), Direccion = Clean(x.Direccion), Idtipoproveedor = x.IdTipoProveedor, Activo = x.Activo, Usuariocreacion = User(current), Ipcreacion = Ip(context), Fechacreacion = DateTime.UtcNow };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string User(ICurrentUserService current) => string.IsNullOrWhiteSpace(current.Email) ? "SYSTEM" : current.Email[..Math.Min(50, current.Email.Length)];
    private static string Ip(HttpContext context) => (context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0")[..Math.Min(45, (context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0").Length)];
}

public sealed record ProveedorRequest(int? IdTipoProveedor, string NombreProveedor, string? Ruc, string? Email, string? Telefono, string? Direccion, bool Activo = true);
