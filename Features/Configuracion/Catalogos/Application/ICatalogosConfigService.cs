using tmr_backend.Features.Configuracion.Catalogos.DTOs;

namespace tmr_backend.Features.Configuracion.Catalogos.Application;

public interface ICatalogosConfigService
{
    Task<SuccessResponse> CrearDetalleAsync(CreateCatalogoDetalleRequest request, string usuarioActual, string ipActual);
    Task<SuccessResponse> ActualizarDetalleAsync(int id, UpdateCatalogoDetalleRequest request, string usuarioActual, string ipActual);
    Task<SuccessResponse> EliminarDetalleAsync(int id, int? idCatalogo, string usuarioActual, string ipActual);
    Task<List<CatalogoDetalleConfigResponse>> ObtenerDetallesPorCatalogoIdAsync(int idCatalogo);
    Task<List<CatalogoDetalleConfigResponse>> ObtenerDetallesPorCatalogoCodigoAsync(string codigoCatalogo);
    Task<List<CatalogoMasterResponse>> ObtenerCatalogosAsync();
}
