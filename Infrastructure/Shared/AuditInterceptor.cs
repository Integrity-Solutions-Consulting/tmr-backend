using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Infrastructure.Shared;

/// <summary>
/// EF Core Interceptor para capturar cambios en BD y registrar auditoría automáticamente.
/// Ejecuta antes de SaveChangesAsync, capturando: Add (Create), Update, Delete.
/// 
/// Registra en: tbl_auditoria_historico_general
/// Campos capturados: usuario, timestamp, IP, tabla, registro ID, acción, valores antes/después
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<AuditInterceptor> _logger;

    public AuditInterceptor(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserService currentUserService,
        ILogger<AuditInterceptor> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var dbContext = eventData.Context as ApplicationDbContext;
        if (dbContext is null)
            return result;

        try
        {
            var userId = _currentUserService.UserId;
            var email = _currentUserService.Email ?? "system";
            var ipAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

            // Iterar sobre todas las entidades tracked (cambios no confirmados)
            var entries = dbContext.ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached)
                .ToList();

            foreach (var entry in entries)
            {
                // Saltar entidades de auditoría (evitar loops)
                if (entry.Entity.GetType() == typeof(TblAuditoriaHistoricoGeneral))
                    continue;

                var tableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name;
                var action = entry.State switch
                {
                    EntityState.Added => "INSERT",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => "UNKNOWN"
                };

                // Obtener ID de la entidad (asume que todas tienen campo Id o id)
                var recordId = GetEntityId(entry.Entity);
                if (recordId <= 0)
                    continue; // Saltar si no tiene ID válido

                // Capturar valores antes/después en formato JSON
                var oldValues = CaptureOldValues(entry);
                var newValues = CaptureNewValues(entry);

                // Crear registro de auditoría
                var auditEntry = new TblAuditoriaHistoricoGeneral
                {
                    Cambiadopor = email,
                    Fechacambio = DateTime.UtcNow,
                    Direccionip = ipAddress,
                    Nombretabla = tableName,
                    Idregistro = recordId.ToString(),
                    Tipooperacion = action,
                    Datosanteriores = oldValues,
                    Datosnuevos = newValues
                };

                await dbContext.TblAuditoriaHistoricoGenerals.AddAsync(auditEntry, cancellationToken);

                _logger.LogInformation(
                    $"[AUDIT] {action} on {tableName} (ID={recordId}) by {email} from {ipAddress}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing audit changes");
            // No lanzar excepción para no romper la transacción de negocio
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Extrae el ID de una entidad (soporta 'Id' y 'id')
    /// </summary>
    private int GetEntityId(object entity)
    {
        var idProperty = entity.GetType().GetProperty("Id") ?? entity.GetType().GetProperty("id");
        if (idProperty == null)
            return 0;

        var value = idProperty.GetValue(entity);
        return value is int intValue ? intValue : 0;
    }

    /// <summary>
    /// Captura valores ANTERIORES (para Update/Delete)
    /// </summary>
    private string CaptureOldValues(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
            return "{}";

        try
        {
            var oldValues = entry.OriginalValues
                .Properties
                .Where(p => !IsNavigationProperty(entry, p.Name))
                .ToDictionary(p => p.Name, p => entry.OriginalValues[p] ?? "null");

            return JsonSerializer.Serialize(oldValues);
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// Captura valores NUEVOS (para Create/Update)
    /// </summary>
    private string CaptureNewValues(EntityEntry entry)
    {
        if (entry.State == EntityState.Deleted)
            return "{}";

        try
        {
            var newValues = entry.CurrentValues
                .Properties
                .Where(p => !IsNavigationProperty(entry, p.Name))
                .ToDictionary(p => p.Name, p => entry.CurrentValues[p] ?? "null");

            return JsonSerializer.Serialize(newValues);
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>
    /// Determina si una propiedad es una navigation property (relation) para excluirla
    /// </summary>
    private bool IsNavigationProperty(EntityEntry entry, string propertyName)
    {
        var navigation = entry.Metadata.FindNavigation(propertyName);
        return navigation != null;
    }
}
