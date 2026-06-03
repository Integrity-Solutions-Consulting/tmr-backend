using tmr_backend.Features.Lideres.DTOs.Response;
namespace tmr_backend.Features.Lideres.Services;

public interface ILiderService
{
    Task<List<PersonaResponse>> ObtenerPersonasNoLideresAsync(CancellationToken ct);
}