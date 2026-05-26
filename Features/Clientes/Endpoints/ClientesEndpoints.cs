using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Clientes.Domain;
using tmr_backend.Features.Clientes.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Clientes;

public static class ClientesEndpoints
{
    public static void MapClientesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clientes").WithTags("Clientes");

        // 1. Obtener todos los clientes para combo dinámico
        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var clientes = await db.TblAdministracionClientes
                .Where(c => c.Activo)
                .OrderBy(c => c.Nombrecomercial ?? c.Razonsocial)
                .Select(c => new ClienteLookupResponse(c.Id, c.Nombrecomercial ?? c.Razonsocial ?? string.Empty))
                .ToListAsync();

            return Results.Ok(clientes);
        });

        // 2. Obtener cliente por ID numérico
        group.MapGet("/{id:int}", async (int id, ApplicationDbContext db) =>
        {
            var cliente = await db.TblAdministracionClientes.FindAsync(id);

            if (cliente is null) return Results.NotFound();

            return Results.Ok(new ClienteLookupResponse(cliente.Id, cliente.Nombrecomercial ?? cliente.Razonsocial ?? string.Empty));
        });

        // 3. Crear cliente (Command)
        group.MapPost("/", async (CrearClienteRequest request, ApplicationDbContext db) =>
        {
            try
            {
                var nuevoCliente = Cliente.Crear(request.Nombre, request.Empresa);
                
                db.Clientes.Add(nuevoCliente);
                await db.SaveChangesAsync();

                var response = new ClienteResponse(nuevoCliente.Id, nuevoCliente.Nombre, nuevoCliente.Empresa, nuevoCliente.Activo, nuevoCliente.FechaCreacion);
                return Results.Created($"/api/clientes/{nuevoCliente.Id}", response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        // 4. Actualizar cliente (Command)
        group.MapPut("/{id:guid}", async (Guid id, ActualizarClienteRequest request, ApplicationDbContext db) =>
        {
            var cliente = await db.Clientes.FindAsync(id);

            if (cliente is null) return Results.NotFound();

            try
            {
                cliente.ActualizarDetalles(request.Nombre, request.Empresa);
                await db.SaveChangesAsync();

                return Results.NoContent();
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { Mensaje = ex.Message });
            }
        });

        // 5. Desactivar cliente (Command)
        group.MapDelete("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var cliente = await db.Clientes.FindAsync(id);

            if (cliente is null) return Results.NotFound();

            cliente.Desactivar();
            await db.SaveChangesAsync();

            return Results.NoContent();
        });
    }
}
