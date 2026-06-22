using tmr_backend.Features.Lideres.DTOs.Request;
using tmr_backend.Features.Lideres.DTOs.Response;

namespace tmr_backend.Features.Lideres.Services;

public interface ILiderService
{
    Task<IEnumerable<LiderResponse>> ObtenerTodosAsync(bool? activo, CancellationToken ct);
    Task<LiderResponse?> ObtenerPorIdAsync(int id, CancellationToken ct);
    Task<LiderResponse> CrearAsync(CrearLiderRequest request, CancellationToken ct);
    Task<LiderResponse?> ActualizarAsync(int id, ActualizarLiderRequest request, CancellationToken ct);
    Task<bool> DesactivarAsync(int id, CancellationToken ct);
    Task<bool> EliminarFisicoAsync(int id, CancellationToken ct);
    Task<ContadoresLiderResponse> ObtenerContadoresAsync(CancellationToken ct);
    Task<IEnumerable<PersonaDisponibleResponse>> ObtenerPersonasDisponiblesAsync(CancellationToken ct);
    Task<IEnumerable<TipoLiderResponse>> ObtenerTiposAsync(CancellationToken ct);
    Task<List<PersonaResponse>> ObtenerPersonasNoLideresAsync(CancellationToken ct);
}