using tmr_backend.Infrastructure.Database;
using tmr_backend.Features.Lideres.DTOs.Response;
using Microsoft.EntityFrameworkCore;


namespace tmr_backend.Features.Lideres.Services;

public sealed class LiderService(ApplicationDbContext db): ILiderService
{
    public async Task<List<PersonaResponse>> ObtenerPersonasNoLideresAsync(CancellationToken ct)
    {
        var personasNoLideres = await db.TblAdministracionPersonas
            .Where(p => !db.TblAdministracionLiders.Any(l => l.Idpersona == p.Id))
            .Select(p => new PersonaResponse(p.Id, p.Nombres, p.Apellidos, p.Email?? string.Empty))
            .ToListAsync(ct);

        return personasNoLideres;
    }
}