using FluentValidation;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Clientes.DTOs.Request;
using tmr_backend.Features.Clientes.DTOs.Response;
using tmr_backend.Features.Clientes.Mappings;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Clientes.Services;

// Contiene toda la lógica de negocio del módulo Clientes.
// Inyección de DbContext + validadores.
public sealed class ClienteService(
    ApplicationDbContext db,
    IValidator<CrearClienteRequest> crearValidator,
    IValidator<ActualizarClienteRequest> actualizarValidator) : IClienteService
{
    // Usuario e IP que se guardan en auditoría. Mientras no exista el JWT,
    // usamos valores fijos. Cuando el token esté listo, saldrán de ahí.
    private const string UsuarioSistema = "SYSTEM";
    private const string IpSistema = "127.0.0.1";

    // =========================================================================
    // LISTAR — alimenta la tabla del frontend
    // =========================================================================
    public async Task<List<ClienteListaResponse>> ListarAsync(
        string? busqueda, bool? activo, CancellationToken ct)
    {
        // .Include trae el tipo de identificación (catálogo TID) para mostrar su Valor.
        var query = db.TblAdministracionClientes
            .Include(c => c.IdtipoidentificacionNavigation)
            .AsQueryable();

        // ── Filtro por estado (Activo / Inactivo) ──
        if (activo.HasValue)
            query = query.Where(c => c.Activo == activo.Value);

        // ── Filtro por búsqueda (nombre comercial, identificación, nombres) ──
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var b = busqueda.Trim().ToLower();
            query = query.Where(c =>
                (c.Nombrecomercial != null && c.Nombrecomercial.ToLower().Contains(b)) ||
                c.Numeroidentificacion.ToLower().Contains(b) ||
                c.Nombres.ToLower().Contains(b));
        }

        var clientes = await query
            .OrderBy(c => c.Nombrecomercial)
            .ToListAsync(ct);

        // Mapeamos cada cliente al DTO de lista.
        return clientes.Select(c => c.ToListaResponse()).ToList();
    }

    // =========================================================================
    // OBTENER POR ID — alimenta el modal de detalle
    // =========================================================================
    public async Task<ClienteDetalleResponse?> ObtenerPorIdAsync(int id, CancellationToken ct)
    {
        var cliente = await db.TblAdministracionClientes
            .Include(c => c.IdtipoidentificacionNavigation)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (cliente is null) return null;

        // Traemos los proyectos activos de este cliente.
        var proyectos = await db.TblTimeReportProyectos
            .Include(p => p.IdestadoproyectoNavigation)
            .Where(p => p.Idcliente == id && p.Activo)
            .Select(p => new ProyectoAsignadoResponse(
                p.Id,
                p.Nombre,
                // Nombre comercial del cliente (para mostrar bajo el proyecto).
                cliente.Nombrecomercial ?? cliente.Razonsocial ?? "",
                // Estado del proyecto (valor del catálogo).
                p.IdestadoproyectoNavigation != null
                    ? p.IdestadoproyectoNavigation.Valor
                    : ""
            ))
            .ToListAsync(ct);

        // Armamos el DTO de detalle con el mapper.
        return cliente.ToDetalleResponse(proyectos);
    }

    // =========================================================================
    // CREAR
    // =========================================================================
    public async Task<int> CrearAsync(CrearClienteRequest request, CancellationToken ct)
    {
        // Validar entrada con FluentValidation.
        var validation = await crearValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Verificar que el tipo de identificación exista.
        var tipoExiste = await db.TblAdministracionCatalogoDetalles
            .AnyAsync(c => c.Id == request.IdTipoIdentificacion, ct);
        if (!tipoExiste)
            throw new InvalidOperationException("El tipo de identificación seleccionado no existe.");

        // Verificar que no exista ya un cliente con el mismo número de identificación.
        var yaExiste = await db.TblAdministracionClientes
            .AnyAsync(c => c.Numeroidentificacion == request.NumeroIdentificacion, ct);
        if (yaExiste)
            throw new InvalidOperationException("Ya existe un cliente con ese número de identificación.");

        // Crear el cliente.
        var cliente = new TblAdministracionCliente
        {
            Idtipoidentificacion = request.IdTipoIdentificacion,
            Numeroidentificacion = request.NumeroIdentificacion,
            Nombrecomercial = request.NombreComercial,
            Nombres = request.Nombres,
            Apellidos = request.Apellidos,
            Email = request.Email,
            Telefono = request.Telefono,
            Direccion = request.Direccion,
            Activo = true,
            Usuariocreacion = UsuarioSistema,
            Ipcreacion = IpSistema
        };

        await db.TblAdministracionClientes.AddAsync(cliente, ct);
        await db.SaveChangesAsync(ct);
        // El trigger de auditoría se dispara solo al insertar.

        return cliente.Id;
    }

    // =========================================================================
    // ACTUALIZAR
    // =========================================================================
    public async Task ActualizarAsync(int id, ActualizarClienteRequest request, CancellationToken ct)
    {
        // Validar entrada.
        var validation = await actualizarValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Buscar el cliente.
        var cliente = await db.TblAdministracionClientes
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (cliente is null)
            throw new InvalidOperationException("El cliente no existe.");

        // Verificar que el tipo de identificación exista.
        var tipoExiste = await db.TblAdministracionCatalogoDetalles
            .AnyAsync(c => c.Id == request.IdTipoIdentificacion, ct);
        if (!tipoExiste)
            throw new InvalidOperationException("El tipo de identificación seleccionado no existe.");

        // Verificar que el número de identificación no esté repetido en OTRO cliente.
        var repetido = await db.TblAdministracionClientes
            .AnyAsync(c => c.Numeroidentificacion == request.NumeroIdentificacion && c.Id != id, ct);
        if (repetido)
            throw new InvalidOperationException("Ya existe otro cliente con ese número de identificación.");

        // Actualizar datos.
        cliente.Activo = request.Activo;
        cliente.Idtipoidentificacion = request.IdTipoIdentificacion;
        cliente.Numeroidentificacion = request.NumeroIdentificacion;
        cliente.Nombrecomercial = request.NombreComercial;
        cliente.Nombres = request.Nombres;
        cliente.Apellidos = request.Apellidos;
        cliente.Email = request.Email;
        cliente.Telefono = request.Telefono;
        cliente.Direccion = request.Direccion;
        cliente.Usuariomodificacion = UsuarioSistema;
        cliente.Fechamodificacion = DateTime.UtcNow;
        cliente.Ipmodificacion = IpSistema;

        await db.SaveChangesAsync(ct);
        // El trigger de auditoría de UPDATE se dispara solo.
    }

    // =========================================================================
    // ELIMINAR — eliminación lógica (Activo = false)
    // =========================================================================
    public async Task EliminarAsync(int id, CancellationToken ct)
    {
        var cliente = await db.TblAdministracionClientes
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (cliente is null)
            throw new InvalidOperationException("El cliente no existe.");

        // No se borra físicamente: solo se marca como inactivo.
        cliente.Activo = false;
        cliente.Usuariomodificacion = UsuarioSistema;
        cliente.Fechamodificacion = DateTime.UtcNow;
        cliente.Ipmodificacion = IpSistema;

        await db.SaveChangesAsync(ct);
    }
}