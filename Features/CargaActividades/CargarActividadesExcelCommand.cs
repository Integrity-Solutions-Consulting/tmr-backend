using Microsoft.AspNetCore.Http;

namespace tmr_backend.Features.CargaActividades // <--- CAMBIADO AQUÍ
{
    public record CargarActividadesExcelCommand(IFormFile File, string ColaboradorId);
}
