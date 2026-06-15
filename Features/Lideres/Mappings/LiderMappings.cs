using tmr_backend.Features.Lideres.DTOs.Response;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Lideres.Mappings;

public static class LiderMappings
{
    public static LiderResponse ToLiderResponse(this TblAdministracionLider lider) =>
        new(
            lider.Id,
            lider.IdpersonaNavigation.Nombres,
            lider.IdpersonaNavigation.Apellidos,
            lider.IdpersonaNavigation.Email,
            lider.IdpersonaNavigation.Telefono,
            lider.IdpersonaNavigation.Tipopersona,
            lider.Idtipo,
            lider.IdtipoNavigation?.Descripcion,
            lider.IdpersonaNavigation.Numeroidentificacion,
            lider.Activo,
            lider.Fechacreacion);
}