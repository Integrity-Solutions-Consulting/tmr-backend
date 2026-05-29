## 🏗️ Enfoque Arquitectónico del Backend

### Principios Aplicados

1. **Vertical Slice Architecture** - Cada feature (Login, Refresh, etc.) es independiente
2. **Clean Architecture** - Separación clara entre capas
3. **Unit of Work Pattern** - Gestión transaccional centralizada
4. **CQRS Ligero** - Handlers separados para operaciones
5. **Dependency Injection** - IoC container en Program.cs



## . Middleware de autenticación y autorización

### Middleware de autenticación

El middleware debe validar en este orden:

1. **Existencia del header**: `Authorization: Bearer <token>`
2. **Parseo y estructura**: Token válido en formato JWT
3. **Firma**: Validar con la clave pública (RS256) o secreta (HS256)
4. **Expiración**: Verificar que `exp > NOW()`
5. **No está en blacklist**: Consultar `tbl_autenticacion_token_blacklist` por `jti`
6. **Sesión activa (OPTIONAL)**: Si deseas control de sesiones desde BD:


**Resultado**: Adjunta `claims` al contexto HTTP:
- `sub` (email)
- `employee_id`
- `name`
- `roles`
- `jti`

### Middleware de autorización

El middleware (o policy) debe evaluar permisos basados en:

1. **Roles**: Verificar si el usuario tiene el rol requerido
   ```csharp
   [Authorize(Roles = "ADMIN")]
   ```

2. **Módulos**: Verificar permisos sobre módulos
   ```csharp
   [Authorize(Policy = "CanViewTimeReport")]
   // Policy evalúa: usuario tiene módulo TimeReport con canView = true
   ```

3. **Permisos específicos**: Si implementas tabla de privilegios
   ```csharp
   [Authorize(Policy = "CanCreateProject")]
   ```

**Policies recomendadas** (configurar en `Startup`):
```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("CanViewTimeReport", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "VER")));
    
    options.AddPolicy("CanCreateProject", policy =>
        policy.Requirements.Add(new HasModulePermissionRequirement("TimeReport", "CREAR")));
});
```

## Endpoints recomendados

### Públicos (sin autenticación)

| Verbo | Ruta | Request | Response |
|---|---|---|---|
| POST | `/auth/login` | `{ email, password }` | `{ accessToken, refreshToken, user, expiresIn }` |
| POST | `/auth/refresh` | `{ refreshToken }` | `{ accessToken, refreshToken, expiresIn }` |
| POST | `/auth/forgot-password` | `{ email }` | `{ message: "Check email" }` |
| POST | `/auth/reset-password` | `{ token, newPassword }` | `{ message: "Password reset" }` |

### Protegidos (requieren `Authorization: Bearer <token>`)

| Verbo | Ruta | Descripción | Roles | Response |
|---|---|---|---|---|
| POST | `/auth/logout` | Cierra sesión y blacklist tokens | Cualquiera | `{ message: "Logout successful" }` |
| POST | `/auth/change-password` | Cambiar contraseña actual | Cualquiera | `{ message: "Password changed" }` |
| GET | `/auth/me` | Obtener perfil del usuario autenticado | Cualquiera | `CurrentUserResponseDto` |
| GET | `/auth/sessions` | Listar sesiones activas del usuario | Cualquiera | `SessionDto[]` |
| DELETE | `/auth/sessions/{id}` | Cerrar sesión específica | ADMIN | `{ message: "Session closed" }` |
| GET | `/auth/permissions` | Obtener permisos y módulos | Cualquiera | `UserPermissionDto[]` |
| POST | `/auth/validate-token` | Validar token (uso interno) | Cualquiera | `{ valid: true, exp: ... }` |

## Estrategia de JWT y sesiones

### Configuración de tiempos

| Parámetro | Valor | Razón |
|---|---|---|
| Access Token TTL | 15-20 minutos | Corto para limitar impacto si se roba |
| Refresh Token TTL | 7-30 días | Largo para evitar re-login frecuente |
| Session Idle Timeout | 30 minutos | Inactividad máxima (OPTIONAL) |
| Password History | 5 últimas | Evita reutilización reciente |

### Payload del Access Token

```json
{
  "alg": "HS256",
  "typ": "JWT"
}
.
{
  "sub": "admin@isc.local",
  "jti": "550e8400-e29b-41d4-a716-446655440000",
  "iat": 1716712800,
  "exp": 1716713700,
  "iss": "inv-tmr-backend",
  "aud": "time-report",
  "employee_id": 1,
  "name": "Administrador Del Sistema",
  "roles": ["ADMIN"]
}
```

### Flujo de tokens

```
┌─────────────────────────────────────────────────────────┐
│ LOGIN (POST /auth/login)                                │
├─────────────────────────────────────────────────────────┤
│ ✓ Email + Password                                      │
│ ✓ Emit Access Token (15 min)                            │
│ ✓ Emit Refresh Token (7 days)                           │
│ ✓ Create Session in tbl_autenticacion_sesion            │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ REQUESTS TO PROTECTED ENDPOINTS                         │
├─────────────────────────────────────────────────────────┤
│ Authorization: Bearer <accessToken>                     │
│ Middleware validates: signature, exp, JTI not blacklist │
└─────────────────────────────────────────────────────────┘
                          ↓
                    (14 min later)
                          ↓
┌─────────────────────────────────────────────────────────┐
│ REFRESH (POST /auth/refresh)                            │
├─────────────────────────────────────────────────────────┤
│ ✓ Send refreshToken                                     │
│ ✓ Emit NEW Access Token (15 min)                        │
│ ✓ OPTIONAL: Rotate Refresh Token                        │
│ ✓ Update Session timestamp                              │
└─────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────┐
│ LOGOUT (POST /auth/logout)                              │
├─────────────────────────────────────────────────────────┤
│ ✓ Add accessToken JTI to tbl_autenticacion_token_blacklist│
│ ✓ Mark Session as EstaActiva = FALSE                    │
│ ✓ Set HoraSalida = NOW()                                │
└─────────────────────────────────────────────────────────┘
```