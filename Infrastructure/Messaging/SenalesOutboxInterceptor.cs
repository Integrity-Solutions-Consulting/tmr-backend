using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace tmr_backend.Infrastructure.Messaging;

/// <summary>
/// Avisa al EventDispatcher de los eventos que se publicaron dentro de una transacción, pero recién cuando esa
/// transacción se confirma. EventBus no los manda al canal en el momento (el dispatcher podría leerlos antes del
/// commit y no encontrarlos), y sin este aviso quedaban esperando la recuperación periódica: hasta 30 s por cada
/// evento encadenado (aviso pedido → envío del correo → registro del resultado), así que el correo y el toast de
/// Seguimiento llegaban tarde. Si la transacción se revierte, los avisos pendientes se descartan.
/// </summary>
public sealed class SenalesOutboxInterceptor(Channel<Guid> canal) : DbTransactionInterceptor
{
    // Eventos publicados por cada DbContext mientras su transacción sigue abierta.
    // Débil: si un DbContext se descarta sin confirmar ni revertir, su lista desaparece con él.
    private static readonly ConditionalWeakTable<DbContext, List<Guid>> Pendientes = new();

    /// <summary>EventBus lo llama al publicar dentro de una transacción.</summary>
    public static void Registrar(DbContext contexto, Guid idEvento)
    {
        var lista = Pendientes.GetOrCreateValue(contexto);
        lock (lista)
            lista.Add(idEvento);
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        Avisar(eventData.Context);

    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Avisar(eventData.Context);
        return Task.CompletedTask;
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        Descartar(eventData.Context);

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Descartar(eventData.Context);
        return Task.CompletedTask;
    }

    private void Avisar(DbContext? contexto)
    {
        foreach (var id in Tomar(contexto))
            canal.Writer.TryWrite(id);   // canal sin límite: nunca rechaza
    }

    private static void Descartar(DbContext? contexto) => Tomar(contexto);

    private static List<Guid> Tomar(DbContext? contexto)
    {
        if (contexto is null || !Pendientes.TryGetValue(contexto, out var lista))
            return [];

        Pendientes.Remove(contexto);
        lock (lista)
            return [.. lista];
    }
}
