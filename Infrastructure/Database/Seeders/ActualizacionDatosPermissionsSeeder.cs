using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Database.Seeders;
public static class ActualizacionDatosPermissionsSeeder
{
    public static async Task EnsureAsync(ApplicationDbContext db, CancellationToken ct=default)
    {
        var module=await db.TblAutenticacionModulos.FirstOrDefaultAsync(x=>x.Nombremodulo=="Actualización de datos",ct);
        if(module is null){module=new TblAutenticacionModulo{Nombremodulo="Actualización de datos",Rutamodulo="/actualizaciones-datos",Ordenvisualizacion=20,Activo=true,Usuariocreacion="SYSTEM",Fechacreacion=DateTime.UtcNow,Ipcreacion="127.0.0.1"};db.Add(module);await db.SaveChangesAsync(ct);}
        var permission=await db.TblAutenticacionPermisos.FirstOrDefaultAsync(x=>x.Codigo=="COLABORADORES_DATOS_REVISAR",ct);
        if(permission is null){permission=new TblAutenticacionPermiso{Idmodulo=module.Id,Codigo="COLABORADORES_DATOS_REVISAR",Accion="REVIEW",Descripcion="Revisar y aprobar actualizaciones de datos de colaboradores",Activo=true,Usuariocreacion="SYSTEM",Fechacreacion=DateTime.UtcNow,Ipcreacion="127.0.0.1"};db.Add(permission);await db.SaveChangesAsync(ct);}
        var admins=await db.TblAutenticacionRols.Where(x=>x.Activo&&new[]{"ADMINISTRADOR","RECURSOS HUMANOS","RECURSOS_HUMANOS"}.Contains(x.Nombre.ToUpper())).ToListAsync(ct);
        foreach(var role in admins) if(!await db.TblAutenticacionRolPermisos.AnyAsync(x=>x.Idrol==role.Id&&x.Idpermiso==permission.Id,ct)) db.Add(new TblAutenticacionRolPermiso{Idrol=role.Id,Idpermiso=permission.Id,Activo=true,Otorgado=DateTime.UtcNow,Usuariocreacion="SYSTEM",Fechacreacion=DateTime.UtcNow,Ipcreacion="127.0.0.1"});
        await db.SaveChangesAsync(ct);
    }
}
