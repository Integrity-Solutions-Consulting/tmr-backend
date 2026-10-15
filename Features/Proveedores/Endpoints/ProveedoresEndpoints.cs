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

        group.MapGet("/", async (string? busqueda, int? tipoProveedorId, bool? activo, int? pagina, int? tamanoPagina, string? ordenarPor, bool? ascendente, ApplicationDbContext db, CancellationToken ct) =>
        {
            var paginaActual = Math.Max(pagina ?? 1, 1);
            var limite = Math.Clamp(tamanoPagina ?? 10, 1, 10_000);
            var usaTablaTipos = await UsaTablaTiposDedicadaAsync(db, ct);
            var query = db.TblInventarioProveedors.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(busqueda))
            {
                var term = busqueda.Trim();
                query = query.Where(x => EF.Functions.ILike(x.Nombreproveedor, $"%{term}%") ||
                    (x.Ruc != null && EF.Functions.ILike(x.Ruc, $"%{term}%")) ||
                    (x.Email != null && EF.Functions.ILike(x.Email, $"%{term}%")) ||
                    (x.Telefono != null && EF.Functions.ILike(x.Telefono, $"%{term}%")));
            }
            if (tipoProveedorId.HasValue) query = query.Where(x => x.Idtipoproveedor == tipoProveedorId);
            if (activo.HasValue) query = query.Where(x => x.Activo == activo);
            var total = await query.CountAsync(ct);
            if (usaTablaTipos)
            {
                IQueryable<TblInventarioProveedor> queryDedicada = query.Include(x => x.IdtipoproveedorNavigation);
                queryDedicada = (ordenarPor?.ToLowerInvariant(), ascendente ?? true) switch
                {
                    ("ruc", true) => queryDedicada.OrderBy(x => x.Ruc),
                    ("ruc", false) => queryDedicada.OrderByDescending(x => x.Ruc),
                    ("tipo", true) => queryDedicada.OrderBy(x => x.IdtipoproveedorNavigation!.Nombre),
                    ("tipo", false) => queryDedicada.OrderByDescending(x => x.IdtipoproveedorNavigation!.Nombre),
                    ("estado", true) => queryDedicada.OrderBy(x => x.Activo),
                    ("estado", false) => queryDedicada.OrderByDescending(x => x.Activo),
                    (_, false) => queryDedicada.OrderByDescending(x => x.Nombreproveedor),
                    _ => queryDedicada.OrderBy(x => x.Nombreproveedor)
                };
                var itemsDedicados = await queryDedicada.Skip((paginaActual - 1) * limite).Take(limite)
                    .ToListAsync(ct);
                return Results.Ok(new { items = itemsDedicados.Select(ToResponse), total, pagina = paginaActual, tamanoPagina = limite });
            }

            // Compatibilidad con entornos previos: IdTipoProveedor apunta al catálogo administrativo.
            query = (ordenarPor?.ToLowerInvariant(), ascendente ?? true) switch
            {
                ("ruc", true) => query.OrderBy(x => x.Ruc),
                ("ruc", false) => query.OrderByDescending(x => x.Ruc),
                ("tipo", true) => query.OrderBy(x => db.TblAdministracionCatalogoDetalles
                    .Where(t => t.Id == x.Idtipoproveedor).Select(t => t.Valor).FirstOrDefault()),
                ("tipo", false) => query.OrderByDescending(x => db.TblAdministracionCatalogoDetalles
                    .Where(t => t.Id == x.Idtipoproveedor).Select(t => t.Valor).FirstOrDefault()),
                ("estado", true) => query.OrderBy(x => x.Activo),
                ("estado", false) => query.OrderByDescending(x => x.Activo),
                (_, false) => query.OrderByDescending(x => x.Nombreproveedor),
                _ => query.OrderBy(x => x.Nombreproveedor)
            };
            var proveedores = await query.Skip((paginaActual - 1) * limite).Take(limite).ToListAsync(ct);
            var tipos = await TiposCatalogoAsync(db, ct);
            var items = proveedores.Select(x => ToResponse(x, x.Idtipoproveedor.HasValue && tipos.TryGetValue(x.Idtipoproveedor.Value, out var nombre) ? nombre : null));
            return Results.Ok(new { items, total, pagina = paginaActual, tamanoPagina = limite });
        });

        group.MapGet("/tipos", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            if (await UsaTablaTiposDedicadaAsync(db, ct))
                return Results.Ok(await db.TblInventarioTipoProveedors.AsNoTracking()
                    .Where(x => x.Activo).OrderBy(x => x.Nombre).Select(x => new { x.Id, x.Nombre }).ToListAsync(ct));
            var tipos = await db.TblAdministracionCatalogoDetalles.AsNoTracking().Where(x => x.Activo)
                .Where(x => db.TblInventarioProveedors.Any(p => p.Idtipoproveedor == x.Id))
                .OrderBy(x => x.Valor).Select(x => new { x.Id, Nombre = x.Valor }).ToListAsync(ct);
            return Results.Ok(tipos);
        });

        group.MapGet("/resumen", async (ApplicationDbContext db, CancellationToken ct) =>
        {
            var resumen = await db.TblInventarioProveedors.AsNoTracking()
                .GroupBy(_ => 1)
                .Select(grupo => new
                {
                    Total = grupo.Count(),
                    Activos = grupo.Count(x => x.Activo),
                    Inactivos = grupo.Count(x => !x.Activo)
                })
                .FirstOrDefaultAsync(ct);

            return Results.Ok(resumen ?? new { Total = 0, Activos = 0, Inactivos = 0 });
        });

        group.MapGet("/{id:int}", async (int id, ApplicationDbContext db, CancellationToken ct) =>
        {
            var usaTablaTipos = await UsaTablaTiposDedicadaAsync(db, ct);
            var item = usaTablaTipos
                ? await db.TblInventarioProveedors.AsNoTracking().Include(x => x.IdtipoproveedorNavigation).FirstOrDefaultAsync(x => x.Id == id, ct)
                : await db.TblInventarioProveedors.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (item is null) return Results.NotFound();
            var facturas = await db.TblInventarioFacturas.AsNoTracking().CountAsync(x => x.Idproveedor == id, ct);
            var tipo = usaTablaTipos ? item.IdtipoproveedorNavigation?.Nombre : await NombreTipoCatalogoAsync(db, item.Idtipoproveedor, ct);
            return Results.Ok(new { proveedor = ToResponse(item, tipo), facturasRelacionadas = facturas });
        });

        group.MapPost("/", async (ProveedorRequest request, ApplicationDbContext db, ICurrentUserService current, HttpContext context, CancellationToken ct) =>
        {
            var error = await Validate(request, null, db, ct); if (error is not null) return error;
            var proveedor = CreateEntity(request, current, context);
            db.TblInventarioProveedors.Add(proveedor); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/proveedores/{proveedor.Id}", new { proveedor.Id });
        }).RequireAuthorization("PROVEEDORES_CREATE");

        group.MapPut("/{id:int}", async (int id, ProveedorRequest request, ApplicationDbContext db, ICurrentUserService current, HttpContext context, CancellationToken ct) =>
        {
            var proveedor = await db.TblInventarioProveedors.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (proveedor is null) return Results.NotFound();
            var error = await Validate(request, id, db, ct); if (error is not null) return error;
            proveedor.Nombreproveedor = request.NombreProveedor.Trim(); proveedor.Ruc = Clean(request.Ruc); proveedor.Email = Clean(request.Email);
            proveedor.Telefono = Clean(request.Telefono); proveedor.Direccion = Clean(request.Direccion); proveedor.Idtipoproveedor = request.IdTipoProveedor;
            proveedor.Activo = request.Activo; proveedor.Usuariomodificacion = User(current); proveedor.Fechamodificacion = DateTime.UtcNow;
            proveedor.Ipmodificacion = Ip(context); await db.SaveChangesAsync(ct); return Results.NoContent();
        }).RequireAuthorization("PROVEEDORES_UPDATE");

        group.MapDelete("/{id:int}", async (int id, ApplicationDbContext db, ICurrentUserService current, HttpContext context, CancellationToken ct) =>
        {
            var proveedor = await db.TblInventarioProveedors.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (proveedor is null) return Results.NotFound();
            if (await db.TblInventarioFacturas.AnyAsync(x => x.Idproveedor == id, ct))
                return Results.Conflict(new ProblemDetails { Title = "No se puede eliminar", Detail = "El proveedor tiene facturas relacionadas." });
            proveedor.Activo = false; proveedor.Usuariomodificacion = User(current); proveedor.Fechamodificacion = DateTime.UtcNow; proveedor.Ipmodificacion = Ip(context);
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
        var tipoValido = request.IdTipoProveedor.HasValue && (await UsaTablaTiposDedicadaAsync(db, ct)
            ? await db.TblInventarioTipoProveedors.AnyAsync(x => x.Id == request.IdTipoProveedor && x.Activo, ct)
            : await db.TblAdministracionCatalogoDetalles.AnyAsync(x => x.Id == request.IdTipoProveedor && x.Activo, ct));
        if (!tipoValido)
            return Results.BadRequest(new ProblemDetails { Title = "Tipo inválido", Detail = "El tipo de proveedor seleccionado no existe o está inactivo." });
        var ruc = Clean(request.Ruc);
        if (ruc is not null && !EsRucEcuatorianoValido(ruc))
            return Results.BadRequest(new ProblemDetails { Title = "RUC inválido", Detail = "Ingrese un RUC ecuatoriano válido de 13 dígitos." });
        if (ruc is not null && await db.TblInventarioProveedors.AnyAsync(x => x.Ruc == ruc && x.Id != id, ct))
            return Results.Conflict(new ProblemDetails { Title = "RUC duplicado", Detail = "Ya existe un proveedor con ese RUC." });
        return null;
    }
    private static object ToResponse(TblInventarioProveedor x) => ToResponse(x, x.IdtipoproveedorNavigation?.Nombre);
    private static object ToResponse(TblInventarioProveedor x, string? tipoProveedor) => new { x.Id, x.Nombreproveedor, x.Ruc, x.Email, x.Telefono, x.Direccion, x.Activo, x.Idtipoproveedor, TipoProveedor = tipoProveedor, x.Fechacreacion };
    private static TblInventarioProveedor CreateEntity(ProveedorRequest x, ICurrentUserService current, HttpContext context) => new() { Nombreproveedor = x.NombreProveedor.Trim(), Ruc = Clean(x.Ruc), Email = Clean(x.Email), Telefono = Clean(x.Telefono), Direccion = Clean(x.Direccion), Idtipoproveedor = x.IdTipoProveedor, Activo = x.Activo, Usuariocreacion = User(current), Ipcreacion = Ip(context), Fechacreacion = DateTime.UtcNow };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string User(ICurrentUserService current) => string.IsNullOrWhiteSpace(current.Email) ? "SYSTEM" : current.Email[..Math.Min(50, current.Email.Length)];
    private static string Ip(HttpContext context) => (context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0")[..Math.Min(45, (context.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0").Length)];
    private static async Task<bool> UsaTablaTiposDedicadaAsync(ApplicationDbContext db, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT to_regclass('inventario.tbl_inventario_tipo_proveedor') IS NOT NULL";
            return Convert.ToBoolean(await command.ExecuteScalarAsync(ct));
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
    private static async Task<Dictionary<int, string>> TiposCatalogoAsync(ApplicationDbContext db, CancellationToken ct) =>
        await db.TblAdministracionCatalogoDetalles.AsNoTracking().Where(x => x.Activo)
            .ToDictionaryAsync(x => x.Id, x => x.Valor, ct);
    private static async Task<string?> NombreTipoCatalogoAsync(ApplicationDbContext db, int? idTipo, CancellationToken ct) =>
        idTipo.HasValue ? await db.TblAdministracionCatalogoDetalles.AsNoTracking().Where(x => x.Id == idTipo && x.Activo).Select(x => x.Valor).FirstOrDefaultAsync(ct) : null;
    private static bool EsRucEcuatorianoValido(string ruc)
    {
        if (ruc.Length != 13 || !ruc.All(char.IsAsciiDigit) || ruc[^3..] != "001") return false;
        var provincia = int.Parse(ruc[..2]);
        if (provincia is < 1 or > 24 || ruc[2] is '7' or '8') return false;
        var d = ruc.Select(c => c - '0').ToArray();
        return ruc[2] switch
        {
            '6' => Modulo11(d, 8, new[] { 3, 2, 7, 6, 5, 4, 3, 2 }),
            '9' => Modulo11(d, 9, new[] { 4, 3, 2, 7, 6, 5, 4, 3, 2 }),
            _ => Modulo10(d)
        };
    }
    private static bool Modulo10(int[] d)
    {
        var suma = Enumerable.Range(0, 9).Sum(i => { var n = d[i] * (i % 2 == 0 ? 2 : 1); return n > 9 ? n - 9 : n; });
        return (10 - suma % 10) % 10 == d[9];
    }
    private static bool Modulo11(int[] d, int verificador, int[] pesos) => (11 - Enumerable.Range(0, pesos.Length).Sum(i => d[i] * pesos[i]) % 11) % 11 == d[verificador];
}

public sealed record ProveedorRequest(int? IdTipoProveedor, string NombreProveedor, string? Ruc, string? Email, string? Telefono, string? Direccion, bool Activo = true);
