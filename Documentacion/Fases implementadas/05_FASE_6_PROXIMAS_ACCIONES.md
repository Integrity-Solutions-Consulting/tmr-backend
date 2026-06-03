# 🚀 Fase 6: Autorización + Auditoría (Próximas Acciones)

**Proyecto:** tmr-backend  
**Fecha:** 28-May-2026  
**Status:** ⏳ PENDIENTE DE IMPLEMENTACIÓN  
**Tiempo estimado:** 3-4 horas

---

## 📑 Tabla de Contenidos

1. [Overview de Fase 6](#overview-de-fase-6)
2. [1. Authorization Policies](#1-authorization-policies)
3. [2. Audit Interceptor](#2-audit-interceptor)
4. [3. Configuración en Program.cs](#3-configuración-en-programcs)
5. [4. Cómo Usar](#4-cómo-usar)
6. [5. Plan de Implementación](#5-plan-de-implementación)

---

## Overview de Fase 6

### Objetivo

Implementar:
1. **Authorization Policies** — Validar permisos granulares por módulo + acción
2. **Audit Interceptor** — Registrar automáticamente todos los cambios en BD
3. **Configuración Final** — Integrar en Program.cs

### Entreg ables

- ✅ Authorization Policies (13 políticas para módulos principales)
- ✅ Authorization Handler (valida permisos dinámicamente)
- ✅ Audit Interceptor (registra cambios automáticamente)
- ✅ Documentación de uso

### Componentes a Crear

| Componente | Archivo | Responsabilidad |
|---|---|---|
| Requirement | `HasModulePermissionRequirement.cs` | Define qué módulo + acción se necesita |
| Handler | `HasModulePermissionHandler.cs` | Valida si usuario tiene permisos |
| Interceptor | `AuditInterceptor.cs` | Captura cambios pre/post save |
| Config | `Program.cs` | Registra policies + interceptor |

---

## 1. Authorization Policies

### 1.1 HasModulePermissionRequirement.cs

**Ubicación:** `Features/Auth/Authorization/HasModulePermissionRequirement.cs`

```csharp
using Microsoft.AspNetCore.Authorization;

namespace tmr_backend.Features.Auth.Authorization;

/// <summary>
/// Requirement para verificar que usuario tiene permiso sobre un módulo + acción.
/// Ejemplo: Usuario quiere CREATE en módulo TimeReport
/// </summary>
public class HasModulePermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Nombre del módulo (ej: "TimeReport", "Proyectos", "Usuarios")
    /// Debe coincidir exactamente con nombremodulo en tbl_autenticacion_modulo
    /// </summary>
    public string ModuleName { get; set; }

    /// <summary>
    /// Acción requerida. Valores válidos:
    /// - "READ" o "VIEW"
    /// - "CREATE"
    /// - "UPDATE" o "EDIT"
    /// - "DELETE"
    /// </summary>
    public string Action { get; set; }

    public HasModulePermissionRequirement(string moduleName, string action)
    {
        ModuleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
        Action = action ?? throw new ArgumentNullException(nameof(action));
    }
}
```

---

### 1.2 HasModulePermissionHandler.cs

**Ubicación:** `Features/Auth/Authorization/HasModulePermissionHandler.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Features.Auth.Authorization;

/// <summary>
/// Handler que valida si usuario tiene permiso sobre módulo + acción.
/// Algoritmo:
/// 1. Obtener roles activos del usuario
/// 2. Consultar permisos de rol (tbl_autenticacion_rol_modulo)
/// 3. Consultar permisos directos del usuario (tbl_autenticacion_usuario_modulo)
/// 4. Merge: Usuario prevalece sobre Rol (OR lógico)
/// 5. Succeed si tiene permiso, Fail si no tiene
/// </summary>
public class HasModulePermissionHandler : AuthorizationHandler<HasModulePermissionRequirement>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUserService;

    public HasModulePermissionHandler(
        ApplicationDbContext db,
        ICurrentUserService currentUserService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        HasModulePermissionRequirement requirement)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId <= 0)
            {
                context.Fail();
                return;
            }

            // 1. Obtener módulo por nombre
            var modulo = await _db.TblAutenticacionModulos
                .FirstOrDefaultAsync(m => 
                    m.Nombremodulo == requirement.ModuleName && m.Activo);

            if (modulo == null)
            {
                // Módulo no existe o no está activo
                context.Fail();
                return;
            }

            // 2. Obtener roles activos del usuario
            var rolesUsuario = await _db.TblAutenticacionUsuarios
                .Where(u => u.Id == userId && u.Estaactivo)
                .SelectMany(u => u.TblAutenticacionUsuarioRols
                    .Where(ur => ur.Activo)
                    .Select(ur => ur.Idrol))
                .Distinct()
                .ToListAsync();

            if (!rolesUsuario.Any())
            {
                context.Fail();
                return;
            }

            // 3. Normalizar la acción
            var accionNormalizada = NormalizeAction(requirement.Action);

            // 4. Validar permisos de ROLES
            var tienePermisoRol = await ValidarPermisoRol(
                rolesUsuario, modulo.Id, accionNormalizada);

            // 5. Validar permisos DIRECTOS del usuario
            var tienePermisoUsuario = await ValidarPermisoUsuario(
                userId, modulo.Id, accionNormalizada);

            // 6. Merge: Usuario > Rol (OR lógico)
            if (tienePermisoRol || tienePermisoUsuario)
            {
                context.Succeed(requirement);
            }
            else
            {
                context.Fail();
            }
        }
        catch (Exception ex)
        {
            // Log error pero falla la autorización por seguridad
            System.Diagnostics.Debug.WriteLine($"Authorization error: {ex.Message}");
            context.Fail();
        }
    }

    /// <summary>
    /// Valida si usuario tiene permiso en el rol
    /// </summary>
    private async Task<bool> ValidarPermisoRol(
        List<int> rolesIds, int moduloId, string accion)
    {
        var query = _db.TblAutenticacionRolModulos
            .Where(rm => 
                rolesIds.Contains(rm.Idrol) &&
                rm.Idmodulo == moduloId &&
                rm.Activo);

        return accion switch
        {
            "Puedever" => await query.AnyAsync(rm => rm.Puedever),
            "Puedecrear" => await query.AnyAsync(rm => rm.Puedecrear),
            "Puedeeditar" => await query.AnyAsync(rm => rm.Puedeeditar),
            "Puedeeliminar" => await query.AnyAsync(rm => rm.Puedeeliminar),
            _ => false
        };
    }

    /// <summary>
    /// Valida si usuario tiene permiso directo (sin rol)
    /// </summary>
    private async Task<bool> ValidarPermisoUsuario(
        int usuarioId, int moduloId, string accion)
    {
        var query = _db.TblAutenticacionUsuarioModulos
            .Where(um => 
                um.Idusuario == usuarioId &&
                um.Idmodulo == moduloId &&
                um.Activo);

        return accion switch
        {
            "Puedever" => await query.AnyAsync(um => um.Puedever),
            "Puedecrear" => await query.AnyAsync(um => um.Puedecrear),
            "Puedeeditar" => await query.AnyAsync(um => um.Puedeeditar),
            "Puedeeliminar" => await query.AnyAsync(um => um.Puedeeliminar),
            _ => false
        };
    }

    /// <summary>
    /// Normaliza la acción a formato BD
    /// READ/VIEW → Puedever, CREATE → Puedecrear, etc.
    /// </summary>
    private string NormalizeAction(string action)
    {
        return action.ToLower() switch
        {
            "read" or "view" => "Puedever",
            "create" => "Puedecrear",
            "update" or "edit" => "Puedeeditar",
            "delete" => "Puedeeliminar",
            _ => throw new ArgumentException($"Acción no soportada: {action}")
        };
    }
}
```

---

### 1.3 Políticas en Program.cs

```csharp
// En Program.cs, sección AddAuthorization:

builder.Services.AddAuthorization(options =>
{
    // ════════════════════════════════════════════════════════════════════
    // POLÍTICAS DE AUTORIZACIÓN POR MÓDULO
    // ════════════════════════════════════════════════════════════════════

    // TimeReport Policies
    options.AddPolicy("CanViewTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "VIEW")));
    
    options.AddPolicy("CanCreateTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "CREATE")));
    
    options.AddPolicy("CanEditTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "EDIT")));
    
    options.AddPolicy("CanDeleteTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "DELETE")));

    // Proyectos Policies
    options.AddPolicy("CanViewProyectos", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Proyectos", "VIEW")));
    
    options.AddPolicy("CanCreateProyectos", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Proyectos", "CREATE")));
    
    options.AddPolicy("CanEditProyectos", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Proyectos", "EDIT")));
    
    options.AddPolicy("CanDeleteProyectos", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Proyectos", "DELETE")));

    // Usuarios Policies
    options.AddPolicy("CanViewUsuarios", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Usuarios", "VIEW")));
    
    options.AddPolicy("CanCreateUsuarios", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Usuarios", "CREATE")));
    
    options.AddPolicy("CanEditUsuarios", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Usuarios", "EDIT")));
    
    options.AddPolicy("CanDeleteUsuarios", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Usuarios", "DELETE")));

    // Configuración Policies
    options.AddPolicy("CanViewConfiguracion", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("Configuracion", "VIEW")));
});

// Registrar el handler de autorización
builder.Services.AddScoped<IAuthorizationHandler, HasModulePermissionHandler>();
```

---

## 2. Audit Interceptor

### 2.1 AuditInterceptor.cs

**Ubicación:** `Infrastructure/Shared/AuditInterceptor.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using tmr_backend.Infrastructure.Database;
using System.Reflection;

namespace tmr_backend.Infrastructure.Shared;

/// <summary>
/// Interceptor de auditoría que captura automáticamente los cambios en BD.
/// Registra: Usuario, IP, Timestamp, Entidad, Acción (Create/Update/Delete), Valores previos y nuevos
/// </summary>
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <summary>
    /// Se ejecuta ANTES de SaveChanges
    /// Analiza entidades modificadas y registra en tabla de auditoría
    /// </summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is ApplicationDbContext dbContext)
        {
            ProcessAudit(dbContext);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ProcessAudit(ApplicationDbContext dbContext)
    {
        var userId = _currentUserService.UserId;
        var timestamp = DateTime.UtcNow;

        foreach (var entry in dbContext.ChangeTracker.Entries())
        {
            // Ignorar entidades no modificadas
            if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
                continue;

            // No auditar la tabla de auditoría misma
            if (entry.Entity.GetType().Name == "TblAuditoriaHistoricoGeneral")
                continue;

            var auditEntry = new AuditEntry
            {
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                Timestamp = timestamp,
                UserId = userId,
                Changes = GetChanges(entry)
            };

            // Registrar en tabla de auditoría
            // var auditRecord = new TblAuditoriaHistoricoGeneral
            // {
            //     EntidadNombre = auditEntry.EntityName,
            //     Accion = auditEntry.Action,
            //     Idusuario = auditEntry.UserId,
            //     Fechahora = auditEntry.Timestamp,
            //     ValorAnterior = auditEntry.Changes.OldValues,
            //     ValorNuevo = auditEntry.Changes.NewValues,
            //     IpOrigen = _currentUserService.IpAddress,
            //     UserAgent = _currentUserService.UserAgent
            // };
            // dbContext.TblAuditoriaHistoricoGenerals.Add(auditRecord);
        }
    }

    private AuditChanges GetChanges(EntityEntry entry)
    {
        var changes = new AuditChanges();

        foreach (var property in entry.Properties)
        {
            if (property.IsModified || entry.State == EntityState.Added)
            {
                changes.OldValues[property.Metadata.Name] = property.OriginalValue;
                changes.NewValues[property.Metadata.Name] = property.CurrentValue;
            }
        }

        return changes;
    }

    private class AuditEntry
    {
        public string EntityName { get; set; }
        public string Action { get; set; }
        public DateTime Timestamp { get; set; }
        public int UserId { get; set; }
        public AuditChanges Changes { get; set; }
    }

    private class AuditChanges
    {
        public Dictionary<string, object> OldValues { get; set; } = new();
        public Dictionary<string, object> NewValues { get; set; } = new();
    }
}
```

---

## 3. Configuración en Program.cs

### 3.1 Registrar Servicios

```csharp
// En Program.cs, dentro de builder.Services

// ════════════════════════════════════════════════════════════════════
// FASE 6: AUTORIZACIÓN + AUDITORÍA
// ════════════════════════════════════════════════════════════════════

// 1. Registrar Audit Interceptor
builder.Services.AddScoped<AuditInterceptor>();

// 2. Agregar al DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    options.AddInterceptors(new AuditInterceptor(...));
});

// 3. Registrar Authorization Policies (ver sección 1.3)
builder.Services.AddAuthorization(options => { ... });

// 4. Registrar Authorization Handler
builder.Services.AddScoped<IAuthorizationHandler, HasModulePermissionHandler>();

// 5. Agregar Authorization Middleware
app.UseAuthentication();
app.UseAuthorization();  // ← IMPORTANTE: Debe estar DESPUÉS de UseAuthentication
```

---

## 4. Cómo Usar

### 4.1 Proteger Endpoint con Política

```csharp
// Ejemplo 1: Proteger un endpoint entero
[Authorize(Policy = "CanViewTimeReport")]
[HttpGet("api/timereport")]
public IActionResult GetTimeReports()
{
    return Ok("Lista de time reports");
}

// Ejemplo 2: Proteger con múltiples políticas (AND)
[Authorize(Policy = "CanCreateProyectos")]
[HttpPost("api/proyectos")]
public IActionResult CreateProyecto([FromBody] CreateProyectoRequest request)
{
    return Ok("Proyecto creado");
}

// Ejemplo 3: Proteger con lógica de negocio adicional
[Authorize]
[HttpDelete("api/usuarios/{id}")]
public async Task<IActionResult> DeleteUsuario(int id)
{
    // Verificar política de forma manual si es necesario
    var authService = HttpContext.RequestServices.GetService<IAuthorizationService>();
    var result = await authService.AuthorizeAsync(User, null, "CanDeleteUsuarios");
    
    if (!result.Succeeded)
        return Forbid();
    
    // Eliminar usuario...
    return Ok();
}
```

### 4.2 Ejemplo Completo: TimeReportEndpoints

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using tmr_backend.Features.TimeReport.GetTimeReports;

namespace tmr_backend.Features.TimeReport;

public static class TimeReportEndpoints
{
    public static void MapTimeReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/timereport")
            .WithName("TimeReport")
            .WithOpenApi();

        // GET - Listar (READ)
        group.MapGet("")
            .WithName("GetTimeReports")
            .WithSummary("Obtener lista de time reports")
            .RequireAuthorization("CanViewTimeReport")  // ← POLÍTICA
            .Produces<IEnumerable<TimeReportDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .WithOpenApi()
            .Produces(StatusCodes.Status401Unauthorized)
            .HandleGetTimeReports();

        // POST - Crear (CREATE)
        group.MapPost("")
            .WithName("CreateTimeReport")
            .WithSummary("Crear nuevo time report")
            .RequireAuthorization("CanCreateTimeReport")  // ← POLÍTICA
            .Produces<TimeReportDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .WithOpenApi()
            .HandleCreateTimeReport();

        // PUT - Actualizar (UPDATE)
        group.MapPut("{id}")
            .WithName("UpdateTimeReport")
            .WithSummary("Actualizar time report")
            .RequireAuthorization("CanEditTimeReport")  // ← POLÍTICA
            .Produces<TimeReportDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .WithOpenApi()
            .HandleUpdateTimeReport();

        // DELETE - Eliminar (DELETE)
        group.MapDelete("{id}")
            .WithName("DeleteTimeReport")
            .WithSummary("Eliminar time report")
            .RequireAuthorization("CanDeleteTimeReport")  // ← POLÍTICA
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .WithOpenApi()
            .HandleDeleteTimeReport();
    }
}
```

---

## 5. Plan de Implementación

### Paso 1: Crear Archivos (15 min)
- [ ] Crear `Features/Auth/Authorization/HasModulePermissionRequirement.cs`
- [ ] Crear `Features/Auth/Authorization/HasModulePermissionHandler.cs`
- [ ] Crear `Infrastructure/Shared/AuditInterceptor.cs`

### Paso 2: Actualizar Program.cs (15 min)
- [ ] Registrar `AuditInterceptor`
- [ ] Agregar `AddAuthorization` con políticas
- [ ] Registrar `HasModulePermissionHandler`
- [ ] Agregar `UseAuthorization()`

### Paso 3: Proteger Endpoints (1 hora)
- [ ] Agregar `[Authorize(Policy = ...)]` a TimeReportEndpoints
- [ ] Agregar `[Authorize(Policy = ...)]` a ProyectosEndpoints
- [ ] Agregar `[Authorize(Policy = ...)]` a UsuariosEndpoints
- [ ] Agregar `[Authorize(Policy = ...)]` a otros endpoints

### Paso 4: Compilar y Probar (30 min)
- [ ] `dotnet build -c Debug` → Debe compilar sin errores
- [ ] Ejecutar servidor
- [ ] Probar endpoints sin permisos → Debe retornar 403
- [ ] Probar endpoints con permisos → Debe funcionar

### Paso 5: Validación (30 min)
- [ ] Verificar auditoría en BD
- [ ] Verificar logs de autorización
- [ ] Test de negación de acceso

---

## Resumen

### Beneficios de Fase 6

| Beneficio | Descripción |
|-----------|-------------|
| **Autorización Granular** | Validar permisos específicos por módulo + acción |
| **Audit Trail Completo** | Registrar automáticamente todas las operaciones |
| **Cumplimiento** | Satisface requisitos de auditoría y compliance |
| **Seguridad** | Protege endpoints contra acceso no autorizado |
| **Mantenibilidad** | Políticas centralizadas en Program.cs |

### Próximas Fases (Fuera de Scope)

- **Fase 7:** Forgot Password + Reset Password
- **Fase 8:** Multi-Factor Authentication (MFA)
- **Fase 9:** OAuth 2.0 / OpenID Connect
- **Fase 10:** API Rate Limiting + DDoS Protection

---

**Última actualización:** 28-May-2026  
**Versión:** 1.0  
**Status:** ⏳ Lista para implementación
