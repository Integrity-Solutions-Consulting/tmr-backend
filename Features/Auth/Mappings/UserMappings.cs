using tmr_backend.Features.Auth.DTOs.Response;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Auth.Mappings;

/// <summary>Mappings manuales de la entidad TblAutenticacionUsuario hacia DTOs de respuesta.</summary>
public static class UserMappings
{
    public static UserResponse ToUserResponse(this TblAutenticacionUsuario user, int? idEmpleado) =>
        new(user.Id, user.Email, user.IdpersonaNavigation?.Nombres ?? "Usuario", user.Fechacreacion, idEmpleado);
}
