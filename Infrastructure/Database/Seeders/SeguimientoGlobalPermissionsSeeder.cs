using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Database.Seeders;

/// <summary>
/// Prepara el permiso y el rol dedicado para gestión global de actividades desde Seguimiento.
/// No asigna el rol a usuarios: esa decisión queda explícita en la administración de usuarios.
/// </summary>
public static class SeguimientoGlobalPermissionsSeeder
{
    private const string Permiso = "SEGUIMIENTO_ACTIVIDADES_GLOBAL_WRITE";
    private const string Rol = "Lider General";
    private static readonly string[] RolesAdministradores = ["ADMINISTRADOR", "ADMIN"];
    private const string UsuarioSistema = "SYSTEM";
    private const string IpSistema = "127.0.0.1";

    public static bool EsRolAdministrador(string? nombre) =>
        nombre is not null && RolesAdministradores.Contains(nombre.Trim().ToUpperInvariant());

    public static async Task EnsureAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var modulo = await db.TblAutenticacionModulos
            .FirstOrDefaultAsync(x => x.Nombremodulo == "Seguimiento", ct)
            ?? throw new InvalidOperationException("No existe el módulo Seguimiento para registrar el permiso global.");

        var permiso = await db.TblAutenticacionPermisos
            .FirstOrDefaultAsync(x => x.Codigo == Permiso, ct);
        if (permiso is null)
        {
            permiso = new TblAutenticacionPermiso
            {
                Idmodulo = modulo.Id,
                Codigo = Permiso,
                Accion = "WRITE_GLOBAL",
                Descripcion = "Permite crear, editar y eliminar actividades de cualquier colaborador desde Seguimiento",
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };
            db.TblAutenticacionPermisos.Add(permiso);
            await db.SaveChangesAsync(ct);
        }
        else if (!permiso.Activo)
        {
            permiso.Activo = true;
            await db.SaveChangesAsync(ct);
        }

        var rol = await db.TblAutenticacionRols
            .FirstOrDefaultAsync(x => x.Nombre == Rol, ct);
        if (rol is null)
        {
            rol = new TblAutenticacionRol
            {
                Nombre = Rol,
                Descripcion = "Gestión global de actividades desde Seguimiento",
                Essistema = true,
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };
            db.TblAutenticacionRols.Add(rol);
            await db.SaveChangesAsync(ct);
        }
        else if (!rol.Activo)
        {
            rol.Activo = true;
            await db.SaveChangesAsync(ct);
        }

        var permisoConsulta = await db.TblAutenticacionPermisos
            .FirstOrDefaultAsync(x => x.Codigo == "SEGUIMIENTO_READ" && x.Activo, ct)
            ?? throw new InvalidOperationException("No existe el permiso SEGUIMIENTO_READ.");

        var rolesConGestionGlobal = await db.TblAutenticacionRols
            .Where(x => x.Activo && EsRolAdministrador(x.Nombre))
            .ToListAsync(ct);
        rolesConGestionGlobal.Add(rol);

        foreach (var rolObjetivo in rolesConGestionGlobal.GroupBy(x => x.Id).Select(x => x.First()))
        {
            foreach (var permisoRol in new[] { permisoConsulta, permiso })
            {
                var asignacion = await db.TblAutenticacionRolPermisos
                    .FirstOrDefaultAsync(x => x.Idrol == rolObjetivo.Id && x.Idpermiso == permisoRol.Id, ct);
                if (asignacion is null)
                {
                    db.TblAutenticacionRolPermisos.Add(new TblAutenticacionRolPermiso
                    {
                        Idrol = rolObjetivo.Id,
                        Idpermiso = permisoRol.Id,
                        Otorgado = DateTime.UtcNow,
                        Activo = true,
                        Usuariocreacion = UsuarioSistema,
                        Fechacreacion = DateTime.UtcNow,
                        Ipcreacion = IpSistema
                    });
                }
                else if (!asignacion.Activo)
                {
                    asignacion.Activo = true;
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
