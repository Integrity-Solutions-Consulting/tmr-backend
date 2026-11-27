using System.Security.Claims;
using tmr_backend.Infrastructure.Database.Seeders;
using tmr_backend.Features.Permisos.Services;
using tmr_backend.Features.TimeReport.Services;

static ClaimsPrincipal Usuario(params string[] permisos) => new(new ClaimsIdentity(
    permisos.Select(p => new Claim("permission", p)), "tests"));

static void Verificar(string caso, bool esperado, bool resultado)
{
    if (resultado != esperado)
        throw new InvalidOperationException($"{caso}: esperado {esperado}, obtenido {resultado}");
}

var global = Usuario(ActividadAuthorization.PermisoGestionGlobal);
var liderAsignado = Usuario("SEGUIMIENTO_READ");
var propietario = Usuario();
var sinPermisos = Usuario();

Verificar("líder general gestiona actividad ajena", true,
    ActividadAuthorization.PuedeGestionar(global, esPropia: false, esLiderAsignado: false));
Verificar("líder asignado gestiona actividad ajena", true,
    ActividadAuthorization.PuedeGestionar(liderAsignado, esPropia: false, esLiderAsignado: true));
Verificar("propietario gestiona su actividad", true,
    ActividadAuthorization.PuedeGestionar(propietario, esPropia: true, esLiderAsignado: false));
Verificar("usuario sin permisos no gestiona actividad ajena", false,
    ActividadAuthorization.PuedeGestionar(sinPermisos, esPropia: false, esLiderAsignado: false));

Verificar("líder general puede crear actividad asignada", true,
    ActividadAuthorization.PuedeGestionar(global, esPropia: false, esLiderAsignado: false));
Verificar("líder general puede editar actividad ajena", true,
    ActividadAuthorization.PuedeGestionar(global, esPropia: false, esLiderAsignado: false));
Verificar("líder general puede eliminar actividad ajena", true,
    ActividadAuthorization.PuedeGestionar(global, esPropia: false, esLiderAsignado: false));
Verificar("líder asignado no gestiona actividad de otro proyecto", false,
    ActividadAuthorization.PuedeGestionar(liderAsignado, esPropia: false, esLiderAsignado: false));
Verificar("permiso global no concede usuarios", false,
    global.HasClaim("permission", "USUARIOS_UPDATE"));
Verificar("permiso global no concede proyectos", false,
    global.HasClaim("permission", "PROYECTOS_WRITE"));
Verificar("rol ADMINISTRADOR recibe gestión global", true,
    SeguimientoGlobalPermissionsSeeder.EsRolAdministrador("ADMINISTRADOR"));
Verificar("rol ADMIN recibe gestión global", true,
    SeguimientoGlobalPermissionsSeeder.EsRolAdministrador(" admin "));
Verificar("rol líder normal no recibe gestión global automáticamente", false,
    SeguimientoGlobalPermissionsSeeder.EsRolAdministrador("LIDER"));
Verificar("fixtures bloqueados en Production aunque la bandera esté activa", false,
    SincronizadorCuentasPermisos.DebeEjecutarFixtures("Production", true));
Verificar("fixtures bloqueados en Staging aunque la bandera esté activa", false,
    SincronizadorCuentasPermisos.DebeEjecutarFixtures("Staging", true));
Verificar("fixtures bloqueados en Development sin bandera", false,
    SincronizadorCuentasPermisos.DebeEjecutarFixtures("Development", false));
Verificar("fixtures bloqueados en Development aunque la bandera estÃ© activa", false,
    SincronizadorCuentasPermisos.DebeEjecutarFixtures("Development", true));
Verificar("fixtures bloqueados en Testing sin bandera", false,
    SincronizadorCuentasPermisos.DebeEjecutarFixtures("Testing", false));
Verificar("fixtures habilitados solo en Testing con bandera explÃ­cita", true,
    SincronizadorCuentasPermisos.DebeEjecutarFixtures("Testing", true));

Console.WriteLine("TimeReportAuthorization: 18 casos correctos.");
