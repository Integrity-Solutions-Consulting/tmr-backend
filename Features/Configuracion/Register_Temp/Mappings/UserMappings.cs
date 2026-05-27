using tmr_backend.Features.Configuracion.Register_Temp.DTOs.Response;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Configuracion.Register_Temp.Mappings;

/// <summary>Mappings manuales de la entidad TblAutenticacionUsuario hacia DTOs de respuesta.</summary>
public static class UserMappings
{
    /// <summary>Convierte un TblAutenticacionUsuario de dominio a UserResponse DTO.</summary>
    public static UserResponse ToUserResponse(this TblAutenticacionUsuario user) =>
        new(user.Id, user.Email, user.Fechacreacion);
}
