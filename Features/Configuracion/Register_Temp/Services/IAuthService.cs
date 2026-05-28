using tmr_backend.Features.Configuracion.Register_Temp.DTOs.Request;
using tmr_backend.Features.Configuracion.Register_Temp.DTOs.Response;

namespace tmr_backend.Features.Configuracion.Register_Temp.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
}
