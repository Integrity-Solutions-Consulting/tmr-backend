using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Usuarios.Domain;
using tmr_backend.Features.Usuarios.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.Usuarios.Endpoints;

/// <summary>
/// Endpoints para gestión de usuarios (CRUD).
/// </summary>
public static class UsuariosEndpoints
{
    public static void MapUsuariosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/usuarios").WithTags("Usuarios");

        // ─────────────────────────────────────────────
        // GET /api/usuarios
        // ─────────────────────────────────────────────
        group.MapGet("/", GetUsuarios)
            .WithName("ListarUsuarios")
            .WithDescription("Obtiene la lista de todos los usuarios activos.")
            .Produces<List<UsuarioResponse>>(StatusCodes.Status200OK);

        // ─────────────────────────────────────────────
        // GET /api/usuarios/{id}
        // ─────────────────────────────────────────────
        group.MapGet("/{id:guid}", GetUsuarioById)
            .WithName("ObtenerUsuario")
            .WithDescription("Obtiene un usuario específico por ID.")
            .Produces<UsuarioResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        // ─────────────────────────────────────────────
        // POST /api/usuarios
        // ─────────────────────────────────────────────
        group.MapPost("/", CrearUsuario)
            .WithName("CrearUsuario")
            .WithDescription("Crea un nuevo usuario.")
            .Produces<UsuarioResponse>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // ─────────────────────────────────────────────
        // PUT /api/usuarios/{id}
        // ─────────────────────────────────────────────
        group.MapPut("/{id:guid}", ActualizarUsuario)
            .WithName("ActualizarUsuario")
            .WithDescription("Actualiza un usuario existente.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // ─────────────────────────────────────────────
        // DELETE /api/usuarios/{id}
        // ─────────────────────────────────────────────
        group.MapDelete("/{id:guid}", EliminarUsuario)
            .WithName("EliminarUsuario")
            .WithDescription("Desactiva un usuario (eliminación lógica).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Obtiene la lista de todos los usuarios activos.
    /// </summary>
    private static async Task<IResult> GetUsuarios(ApplicationDbContext db)
    {
        var usuarios = await db.Usuarios
            .Where(u => u.Activo)
            .Select(u => new UsuarioResponse(u.Id, u.Nombre, u.Descripcion, u.Activo, u.FechaCreacion))
            .ToListAsync();

        return Results.Ok(usuarios);
    }

    /// <summary>
    /// Obtiene un usuario específico por ID.
    /// </summary>
    private static async Task<IResult> GetUsuarioById(Guid id, ApplicationDbContext db)
    {
        var usuario = await db.Usuarios.FindAsync(id);

        if (usuario is null)
            return Results.NotFound();

        return Results.Ok(new UsuarioResponse(usuario.Id, usuario.Nombre, usuario.Descripcion, usuario.Activo, usuario.FechaCreacion));
    }

    /// <summary>
    /// Crea un nuevo usuario.
    /// </summary>
    private static async Task<IResult> CrearUsuario(CrearUsuarioRequest request, ApplicationDbContext db)
    {
        try
        {
            var nuevoUsuario = Usuario.Crear(request.Nombre, request.Descripcion);

            db.Usuarios.Add(nuevoUsuario);
            await db.SaveChangesAsync();

            var response = new UsuarioResponse(nuevoUsuario.Id, nuevoUsuario.Nombre, nuevoUsuario.Descripcion, nuevoUsuario.Activo, nuevoUsuario.FechaCreacion);
            return Results.Created($"/api/usuarios/{nuevoUsuario.Id}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Validación fallida",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Actualiza un usuario existente.
    /// </summary>
    private static async Task<IResult> ActualizarUsuario(Guid id, ActualizarUsuarioRequest request, ApplicationDbContext db)
    {
        var usuario = await db.Usuarios.FindAsync(id);

        if (usuario is null)
            return Results.NotFound();

        try
        {
            usuario.ActualizarDetalles(request.Nombre, request.Descripcion);
            await db.SaveChangesAsync();

            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Validación fallida",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// Desactiva un usuario (eliminación lógica).
    /// </summary>
    private static async Task<IResult> EliminarUsuario(Guid id, ApplicationDbContext db)
    {
        var usuario = await db.Usuarios.FindAsync(id);

        if (usuario is null)
            return Results.NotFound();

        usuario.Desactivar();
        await db.SaveChangesAsync();

        return Results.NoContent();
    }
}
