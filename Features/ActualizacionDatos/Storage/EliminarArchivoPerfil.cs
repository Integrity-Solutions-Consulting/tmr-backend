using tmr_backend.Infrastructure.Messaging;

namespace tmr_backend.Features.ActualizacionDatos.Storage;

public sealed record EliminarArchivoPerfil(string StorageKey, string? Provider) : IEvento;

public sealed class EliminarArchivoPerfilHandler(IFileStorage storage) : IEventHandler<EliminarArchivoPerfil>
{
    public Task HandleAsync(EliminarArchivoPerfil evento, CancellationToken ct) =>
        storage.DeleteAsync(evento.StorageKey, ct, evento.Provider);
}
