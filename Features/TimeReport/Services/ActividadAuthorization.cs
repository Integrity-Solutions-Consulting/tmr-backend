using System.Security.Claims;

namespace tmr_backend.Features.TimeReport.Services;

/// <summary>Reglas de autorización de actividades gestionadas desde Seguimiento.</summary>
public static class ActividadAuthorization
{
    public const string PermisoGestionGlobal = "SEGUIMIENTO_ACTIVIDADES_GLOBAL_WRITE";

    public static bool TieneGestionGlobal(ClaimsPrincipal user) =>
        user.HasClaim("permission", PermisoGestionGlobal);

    public static bool PuedeGestionar(ClaimsPrincipal user, bool esPropia, bool esLiderAsignado) =>
        esPropia || esLiderAsignado || TieneGestionGlobal(user);
}
