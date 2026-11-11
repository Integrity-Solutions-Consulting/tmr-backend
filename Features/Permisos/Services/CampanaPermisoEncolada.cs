using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Messaging;
using tmr_backend.Features.Notificaciones.Application.Estado;

namespace tmr_backend.Features.Permisos.Services;

// Las alertas ya están persistidas en la misma transacción que la decisión.
// Repetir este evento solo refresca SSE; nunca inserta otra alerta.
public sealed record CampanaPermisoEncolada(int[] Usuarios) : IEvento
{
    public static async Task GuardarAsync(ApplicationDbContext db, IEnumerable<int> usuarios, CancellationToken ct)
    {
        db.EventosOutbox.Add(new EventoOutbox {
            Id = Guid.NewGuid(), TipoEvento = nameof(CampanaPermisoEncolada),
            Payload = JsonSerializer.Serialize(new CampanaPermisoEncolada(usuarios.Distinct().ToArray())),
            Estado = EstadoEvento.Pendiente, FechaCreacion = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
}

public sealed class CampanaPermisoHandler(INotificacionInternaNotifier notifier) : IEventHandler<CampanaPermisoEncolada>
{
    public Task HandleAsync(CampanaPermisoEncolada evento, CancellationToken ct)
    {
        notifier.NotificarUsuarios(evento.Usuarios);
        return Task.CompletedTask;
    }
}
