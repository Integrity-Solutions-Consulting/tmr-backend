# Ruta de implementación — Feature Auth (Vertical Slice)

> **Proyecto:** `tmr_backend` · **BD:** `Inv_tmr_db` (PostgreSQL 16+) · **Arquitectura:** Vertical Slice + Clean Architecture

---

## Leyenda

| Etiqueta | Significado |
|---|---|
| `ya existe` | Archivo ya provisto, no modificar salvo indicación |
| `crear` | Archivo nuevo desde cero |
| `conectar` | Registrar o cablear en `Program.cs` |

---

## Fase 1 — Infraestructura base

> Entidades, DbContext, seguridad — sin lógica de negocio aún.

### `Security/` — ya existe, revisar 

`JwtSettings`, `IPasswordHasher`, `PasswordHasher` (BCrypt w=12), `ITokenService`, `TokenService` están completos y no requieren modificación.

### `Database/Entities/` — verificar scaffold `ya existe`

Confirmar que el scaffold cubre las siguientes entidades y que tienen las navigation properties entre esquemas:

- `TblAutenticacionUsuario` → `ICollection<TblAutenticacionUsuarioRol>`
- `TblAutenticacionUsuarioRol` → `TblAdministracionCatalogoDetalle` (para resolver nombre del rol)
- `TblAutenticacionSesion` (RefreshTokenHash, ExpiresAt, EstaActiva, HoraSalida)
- `TblAutenticacionTokenBlacklist`
- `TblAutenticacionPasswordHistorial`
- `TblAdministracionPersona` → `TblAdministracionEmpleado`

> **Riesgo:** el scaffold de Npgsql a veces omite navigation properties entre esquemas distintos (`autenticacion` ↔ `administracion`). Agregar manualmente las que falten.

### `Program.cs` — `ya creado, revisar que este en orden`

```csharp
// Registrar en este orden:
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt => {
        opt.TokenValidationParameters = new() {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"]!))
        };
        // Fase 3: agregar OnTokenValidated para blacklist
    });

builder.Services.AddAuthorization(); // Fase 6: agregar policies aquí
```

Orden de middleware:
```
app.UseAuthentication();
app.UseAuthorization();
// MapEndpoints de cada fase
```

### `appsettings.json` — `ya creado`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=Inv_tmr_db;Username=...;Password=..."
  },
  "Jwt": {
    "SecretKey": "<mínimo 32 chars>",
    "Issuer": "inv-tmr-backend",
    "Audience": "time-report",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  }
}
```

### `Shared/CurrentUserService.cs` — `crear`

```csharp
public interface ICurrentUserService {
    int UserId { get; }
    string Email { get; }
    IEnumerable<string> Roles { get; }
    string? Jti { get; }
}

public class CurrentUserService : ICurrentUserService {
    // Lee HttpContext.User.Claims
    // Registrar como Scoped
}
```

---

## Fase 2 — Feature: Login

> Primer endpoint funcional — valida credenciales, emite tokens, guarda sesión.

### `Features/Auth/Login/DTOs/LoginRequest.cs` — `crear`

```csharp
public record LoginRequest(string Email, string Password);
```

### `Features/Auth/Login/DTOs/LoginResponse.cs` — `crear`

```csharp
public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,        // segundos
    UserDto User
);

public record UserDto(int Id, string Nombre, string Email, string[] Roles);
```

### `Features/Auth/Login/LoginValidator.cs` — `crear`

FluentValidation:
- `Email`: no vacío, formato email válido.
- `Password`: no vacío, mínimo 6 caracteres.

### `Features/Auth/Login/LoginHandler.cs` — `crear`

Lógica en orden:

1. Buscar usuario por email con:
   ```csharp
   .Include(u => u.UsuarioRoles)
       .ThenInclude(ur => ur.CatalogoDetalle)
   ```
2. Verificar `EstaActivo = true` → 401 si no.
3. `IPasswordHasher.Verify(request.Password, user.HashPassword)` → 401 si falla.
4. `ITokenService.GenerateAccessToken(user)` — **ver nota abajo sobre claims faltantes**.
5. `ITokenService.GenerateRefreshToken()` → `(rawToken, expiresAt)`.
6. `ITokenService.HashToken(rawToken)` → guardar hash en `TblAutenticacionSesion`:
   - `RefreshTokenHash`, `UsuarioId`, `ExpiresAt`, `EstaActiva = true`, `IpCreacion`.
7. `SaveChangesAsync()` y retornar `LoginResponse`.

> **Nota importante:** `TokenService.GenerateAccessToken` actualmente solo incluye `sub` y `email`. Debe extenderse para agregar `employee_id`, `name`, `roles` y `jti` (Guid nuevo), ya que el middleware de blacklist y `GetPermissions` dependen de esos claims.

### `Features/Auth/Login/LoginEndpoint.cs` — `crear`

```csharp
app.MapPost("/api/auth/login", async (LoginRequest req, LoginHandler handler) =>
    await handler.Handle(req))
    .AllowAnonymous();
```

### `Program.cs` — `conectar`

```csharp
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddFluentValidationAutoValidation();
```

---

## Fase 3 — Feature: Refresh + Logout

> Ciclo de vida completo de tokens — rotar y revocar.

### `Features/Auth/Refresh/RefreshHandler.cs` — `crear`

1. Hashear el refreshToken recibido.
2. Buscar en `TblAutenticacionSesion` por hash donde `EstaActiva = true`.
3. Validar `ExpiresAt > DateTime.UtcNow` → 401 si expirado.
4. Cargar usuario + roles.
5. Emitir nuevo AccessToken y nuevo RefreshToken (**token rotation**).
6. Actualizar sesión: nuevo hash, nuevo ExpiresAt, `FechaUltimaActividad = NOW()`.
7. Retornar `RefreshTokenResponse`.

### `Features/Auth/Logout/LogoutHandler.cs` — `crear`

1. Extraer `jti` del AccessToken actual vía `ICurrentUserService.Jti`.
2. Insertar en `TblAutenticacionTokenBlacklist`: `Jti`, `FechaExpiracion` (extraída del claim `exp`).
3. Buscar sesión activa del usuario → `EstaActiva = false`, `HoraSalida = NOW()`.
4. `SaveChangesAsync()`.

### Extender middleware JWT para blacklist — `conectar`

En `Program.cs`, dentro de `AddJwtBearer`:

```csharp
opt.Events = new JwtBearerEvents {
    OnTokenValidated = async ctx => {
        var jti = ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (jti is not null) {
            var cache = ctx.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
            if (cache.TryGetValue($"bl:{jti}", out _)) {
                ctx.Fail("Token revocado");
                return;
            }
            // Fallback a BD si no está en caché
            var db = ctx.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
            var enBlacklist = await db.TblAutenticacionTokenBlacklists
                .AnyAsync(t => t.Jti == jti);
            if (enBlacklist) ctx.Fail("Token revocado");
        }
    }
};
```

> **Nota de rendimiento:** consultar la tabla en cada request protegido puede ser costoso. Se recomienda cachear los `jti` revocados en `IMemoryCache` con TTL igual al `exp` del token. Implementar desde el inicio.

### Endpoints — `crear`

```
POST /api/auth/refresh   → AllowAnonymous
POST /api/auth/logout    → [Authorize]
```

---

## Fase 4 — Feature: ChangePassword

> Requiere historial de contraseñas y política de no-reutilización.

### `Features/Auth/ChangePassword/ChangePasswordHandler.cs` — `crear`

1. Obtener usuario vía `ICurrentUserService.UserId`.
2. `IPasswordHasher.Verify(oldPassword, user.HashPassword)` → 400 si falla.
3. Consultar últimas 5 entradas de `TblAutenticacionPasswordHistorial` del usuario.
4. Por cada hash anterior: `IPasswordHasher.Verify(newPassword, hash)` → 400 si reutiliza.
5. `IPasswordHasher.Hash(newPassword)` → actualizar `user.HashPassword`.
6. Insertar nuevo registro en historial.
7. `SaveChangesAsync()`.

### `Features/Auth/ChangePassword/Validators/PasswordPolicyValidator.cs` — `crear`

FluentValidation reutilizable para cualquier campo de contraseña nueva:
- Mínimo 8 caracteres.
- Al menos 1 mayúscula.
- Al menos 1 número.
- Al menos 1 símbolo.

---

## Fase 5 — Features: GetCurrentUser + GetPermissions

> Datos enriquecidos post-login para el cliente frontend.

### `Features/Auth/GetCurrentUser/GetCurrentUserHandler.cs` — `crear`

```csharp
// LINQ con includes entre esquemas
var user = await _db.TblAutenticacionUsuarios
    .Include(u => u.Persona)
        .ThenInclude(p => p.Empleado)
            .ThenInclude(e => e.Cargo)
    .FirstOrDefaultAsync(u => u.Id == userId);
```

Retornar `CurrentUserResponse`: id, nombres, apellidos, email, cargo, codigoEmpleado, roles.

### `Features/Auth/GetPermissions/GetPermissionsHandler.cs` — `crear`

Query en dos partes con merge en memoria:

```
a) TblAutenticacionRolModulo JOIN TblAutenticacionUsuarioRol
   WHERE IdUsuario = userId AND PuedeVer = true

b) TblAutenticacionUsuarioModulo
   WHERE IdUsuario = userId (overrides directos)
```

Regla de merge: **el permiso de usuario prevalece sobre el de rol** (OR de flags CRUD).

Retornar `UserPermissionsResponse`:
- `Roles[]`
- `Modulos[]` con flags `PuedeVer`, `PuedeCrear`, `PuedeEditar`, `PuedeEliminar`
- `MenuItems[]` desde `TblAutenticacionMenu` filtrado por rol/usuario

> **Prerequisito:** el script `03_insert_permisos_rol_modulo_tmr.sql` debe estar aplicado en la BD antes de probar este handler. Sin esos registros el handler retornará vacío y parecerá roto.

---

## Fase 6 — Autorización + Auditoría

> Políticas de módulos, interceptor de auditoría, cierre de `Program.cs`.

### `HasModulePermissionRequirement.cs` — `crear`

```csharp
public class HasModulePermissionRequirement(string modulo, string accion)
    : IAuthorizationRequirement { }

public class HasModulePermissionHandler : AuthorizationHandler<HasModulePermissionRequirement>
{
    // Reutiliza la misma lógica de GetPermissionsHandler
    // Fail si el usuario no tiene el flag requerido en el módulo
}
```

### `Shared/AuditInterceptor.cs` — `crear`

```csharp
public class AuditInterceptor(IServiceProvider serviceProvider)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(...)
    {
        // Resolver ICurrentUserService aquí (NO en constructor — es Scoped)
        var currentUser = serviceProvider.GetRequiredService<ICurrentUserService>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property("UsuarioCreacion").CurrentValue = currentUser.Email;
                entry.Property("FechaCreacion").CurrentValue = DateTime.UtcNow;
            }
            if (entry.State == EntityState.Modified)
            {
                entry.Property("UsuarioModificacion").CurrentValue = currentUser.Email;
                entry.Property("FechaModificacion").CurrentValue = DateTime.UtcNow;
            }
        }
    }
}
```

> **Nota crítica:** `ICurrentUserService` es `Scoped` pero el interceptor se registra como singleton. Inyectarlo a través de `IServiceProvider` en `SavingChangesAsync`, **nunca en el constructor**, para evitar el error de captura de scoped en singleton.

Registrar en `ApplicationDbContext`:
```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    => optionsBuilder.AddInterceptors(_auditInterceptor);
```

### Completar `Program.cs` — `conectar`

```csharp
// Policies de módulos
builder.Services.AddAuthorization(options => {
    options.AddPolicy("CanViewTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TMR_TIME_REPORT", "VER")));
    options.AddPolicy("CanCreateProject", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TMR_PROYECTOS", "CREAR")));
    // ... resto de policies
});

builder.Services.AddScoped<IAuthorizationHandler, HasModulePermissionHandler>();
builder.Services.AddMemoryCache(); // para blacklist cache

// Swagger con Bearer
builder.Services.AddSwaggerGen(c => {
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { ... });
    c.AddSecurityRequirement(...);
});

// CORS (ajustar origins según entorno)
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
```

---

## Resumen de archivos por fase

| Fase | Archivos nuevos | Archivos a conectar |
|---|---|---|
| 1 | `Program.cs`, `appsettings.json`, `CurrentUserService.cs` | DbContext, Security, IOptions |
| 2 | `LoginRequest`, `LoginResponse`, `LoginValidator`, `LoginHandler`, `LoginEndpoint` | `AddScoped<LoginHandler>`, `MapPost /login` |
| 3 | `RefreshHandler`, `RefreshEndpoint`, `LogoutHandler`, `LogoutEndpoint` | `OnTokenValidated` + blacklist cache |
| 4 | `ChangePasswordHandler`, `ChangePasswordEndpoint`, `PasswordPolicyValidator` | `MapPost /change-password` |
| 5 | `GetCurrentUserHandler`, `GetCurrentUserEndpoint`, `GetPermissionsHandler`, `GetPermissionsEndpoint` | `MapGet /me`, `MapGet /permissions` |
| 6 | `HasModulePermissionRequirement`, `HasModulePermissionHandler`, `AuditInterceptor` | Policies, `AddMemoryCache`, Swagger, CORS |
