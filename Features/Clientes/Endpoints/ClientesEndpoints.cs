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

        // 1. Obtener todos los clientes (Query)
        group.MapGet("/", async (ApplicationDbContext db) =>
        {
            var clientes = await db.Clientes
                .Where(c => c.Activo)
                .Select(c => new ClienteResponse(c.Id, c.Nombre, c.Empresa, c.Activo, c.FechaCreacion))
                .ToListAsync();

            return Results.Ok(clientes);
        });

        // 2. Obtener cliente por ID (Query)
        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var cliente = await db.Clientes.FindAsync(id);

            if (cliente is null) return Results.NotFound();

            return Results.Ok(new ClienteResponse(cliente.Id, cliente.Nombre, cliente.Empresa, cliente.Activo, cliente.FechaCreacion));
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
