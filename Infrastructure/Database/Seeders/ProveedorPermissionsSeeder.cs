using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Database.Seeders;

/// <summary>
/// Mantiene la configuración RBAC mínima del módulo Proveedores en instalaciones ya existentes.
/// Solo inserta o reactiva sus propios registros, por lo que es seguro ejecutar al inicio.
/// </summary>
public static class ProveedorPermissionsSeeder
{
    private const string UsuarioSistema = "SYSTEM";
    private const string IpSistema = "127.0.0.1";

    public static async Task EnsureAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var modulo = await db.TblAutenticacionModulos
            .FirstOrDefaultAsync(x => x.Nombremodulo == "Proveedores", ct);
        if (modulo is null)
        {
            modulo = new TblAutenticacionModulo
            {
                Nombremodulo = "Proveedores",
                Rutamodulo = "/menu/proveedores",
                Ordenvisualizacion = 8,
                Idmodulopadre = 0,
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };
            db.TblAutenticacionModulos.Add(modulo);
            await db.SaveChangesAsync(ct);
        }
        else if (!modulo.Activo)
        {
            modulo.Activo = true;
            modulo.Usuariomodificacion = UsuarioSistema;
            modulo.Fechamodificacion = DateTime.UtcNow;
            modulo.Ipmodificacion = IpSistema;
            await db.SaveChangesAsync(ct);
        }

        var acciones = new Dictionary<string, string>
        {
            ["READ"] = "Permite consultar proveedores",
            ["CREATE"] = "Permite crear proveedores",
            ["UPDATE"] = "Permite editar proveedores",
            ["DELETE"] = "Permite desactivar proveedores"
        };
        foreach (var (accion, descripcion) in acciones)
        {
            var codigo = $"PROVEEDORES_{accion}";
            var permiso = await db.TblAutenticacionPermisos.FirstOrDefaultAsync(x => x.Codigo == codigo, ct);
            if (permiso is null)
            {
                db.TblAutenticacionPermisos.Add(new TblAutenticacionPermiso
                {
                    Idmodulo = modulo.Id,
                    Codigo = codigo,
                    Accion = accion,
                    Descripcion = descripcion,
                    Activo = true,
                    Usuariocreacion = UsuarioSistema,
                    Fechacreacion = DateTime.UtcNow,
                    Ipcreacion = IpSistema
                });
            }
            else if (!permiso.Activo)
            {
                permiso.Activo = true;
            }
        }
        await db.SaveChangesAsync(ct);

        var permisos = await db.TblAutenticacionPermisos
            .Where(x => x.Codigo.StartsWith("PROVEEDORES_") && x.Activo)
            .ToDictionaryAsync(x => x.Codigo, ct);
        var roles = await db.TblAutenticacionRols
            .Where(x => x.Activo && (x.Nombre.ToUpper() == "ADMINISTRADOR" || x.Nombre.ToUpper() == "GERENTE" || x.Nombre.ToUpper() == "LIDER" || x.Nombre.ToUpper() == "LÍDER"))
            .ToListAsync(ct);

        foreach (var rol in roles)
        {
            IEnumerable<string> codigos = rol.Nombre.Equals("ADMINISTRADOR", StringComparison.OrdinalIgnoreCase)
                ? permisos.Keys
                : ["PROVEEDORES_READ"];
            foreach (var codigo in codigos)
            {
                if (!permisos.TryGetValue(codigo, out var permiso)) continue;
                var asignacion = await db.TblAutenticacionRolPermisos
                    .FirstOrDefaultAsync(x => x.Idrol == rol.Id && x.Idpermiso == permiso.Id, ct);
                if (asignacion is null)
                {
                    db.TblAutenticacionRolPermisos.Add(new TblAutenticacionRolPermiso
                    {
                        Idrol = rol.Id,
                        Idpermiso = permiso.Id,
                        Activo = true,
                        Otorgado = DateTime.UtcNow,
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
