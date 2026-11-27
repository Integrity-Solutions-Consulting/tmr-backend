using System.Data.Common;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Security;

/// <summary>
/// Invalida la caché de permisos (PermissionService) cuando se guardan cambios en roles de usuario,
/// en la matriz rol-permiso, en permisos o en roles. Así ningún camino administrativo depende de
/// acordarse de llamar a la invalidación, y un rol retirado deja de autorizar en la siguiente petición.
/// Si el cambio ocurre dentro de una transacción, se invalida otra vez al confirmarla: una petición
/// concurrente pudo volver a cachear los permisos viejos antes del commit.
/// Alcance: caché en memoria de este proceso. Con varias réplicas, cada una conserva su copia
/// hasta 15 minutos; para eso hace falta una invalidación distribuida (fase siguiente).
/// </summary>
public sealed class PermisosCacheInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
{
    // Contextos con cambios de autorización guardados dentro de una transacción aún abierta.
    private static readonly ConditionalWeakTable<DbContext, object> PendientesDeCommit = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Marcar(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Marcar(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        InvalidarSiCorresponde(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        InvalidarSiCorresponde(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        InvalidarAlConfirmar(eventData.Context);

    public Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        InvalidarAlConfirmar(eventData.Context);
        return Task.CompletedTask;
    }

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        Descartar(eventData.Context);

    public Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Descartar(eventData.Context);
        return Task.CompletedTask;
    }

    // Marca de "cambio de autorización" entre SavingChanges y SavedChanges del mismo contexto.
    private static readonly ConditionalWeakTable<DbContext, object> CambiosEnCurso = new();

    private static void Marcar(DbContext? contexto)
    {
        if (contexto is null) return;
        var hayCambios = contexto.ChangeTracker.Entries().Any(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && e.Entity is TblAutenticacionUsuarioRol or TblAutenticacionRolPermiso
                or TblAutenticacionPermiso or TblAutenticacionRol);
        if (hayCambios)
            CambiosEnCurso.AddOrUpdate(contexto, new object());
    }

    private static void InvalidarSiCorresponde(DbContext? contexto)
    {
        if (contexto is null || !CambiosEnCurso.TryGetValue(contexto, out _)) return;
        CambiosEnCurso.Remove(contexto);
        PermissionService.InvalidarTodos();
        if (contexto.Database.CurrentTransaction is not null)
            PendientesDeCommit.AddOrUpdate(contexto, new object());
    }

    private static void InvalidarAlConfirmar(DbContext? contexto)
    {
        if (contexto is null || !PendientesDeCommit.TryGetValue(contexto, out _)) return;
        PendientesDeCommit.Remove(contexto);
        PermissionService.InvalidarTodos();
    }

    private static void Descartar(DbContext? contexto)
    {
        if (contexto is null) return;
        PendientesDeCommit.Remove(contexto);
    }
}
