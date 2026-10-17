using tmr_backend.Features.Clientes.DTOs.Request;
using tmr_backend.Features.Clientes.DTOs.Response;

namespace tmr_backend.Features.Clientes.Services;

// Contrato del servicio de clientes (SOLID - DIP).
public interface IClienteService
{
    // Lista clientes con filtros opcionales (búsqueda, estado).
    Task<List<ClienteListaResponse>> ListarAsync(
        string? busqueda, bool? activo, CancellationToken ct);

    // Obtiene el detalle completo de un cliente, incluyendo sus proyectos.
    Task<ClienteDetalleResponse?> ObtenerPorIdAsync(int id, CancellationToken ct);

    // Crea un cliente. 
    Task<int> CrearAsync(CrearClienteRequest request, CancellationToken ct);

    // Actualiza un cliente existente.
    Task ActualizarAsync(int id, ActualizarClienteRequest request, CancellationToken ct);

    // Eliminación lógica: marca el cliente como Activo = false.
    Task EliminarAsync(int id, CancellationToken ct);
}