using tmr_backend.Features.Colaboradores.DTOs.Request;
using tmr_backend.Features.Colaboradores.DTOs.Response;

namespace tmr_backend.Features.Colaboradores.Services;

// Contrato del servicio de colaboradores (SOLID - DIP).
public interface IColaboradorService
{
    // Lista colaboradores con filtros opcionales (búsqueda, estado, asignación).
    Task<List<ColaboradorListaResponse>> ListarAsync(
        string? busqueda, bool? activo, int? asignacion, CancellationToken ct);

    // Obtiene el detalle completo de un colaborador, incluyendo sus proyectos.
    Task<ColaboradorDetalleResponse?> ObtenerPorIdAsync(int id, CancellationToken ct);

    // Crea un colaborador (Persona + Empleado en una transacción). Devuelve el Id nuevo.
    Task<int> CrearAsync(CrearColaboradorRequest request, CancellationToken ct);

    // Actualiza un colaborador existente (Persona + Empleado en una transacción).
    Task ActualizarAsync(int id, ActualizarColaboradorRequest request, CancellationToken ct);

    // Eliminación lógica: marca el empleado como Activo = false.
    Task EliminarAsync(int id, CancellationToken ct);
}