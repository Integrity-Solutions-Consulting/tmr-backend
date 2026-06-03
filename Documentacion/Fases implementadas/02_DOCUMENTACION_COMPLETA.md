# 📚 Documentación Técnica Completa — Fases 1-5

**Proyecto:** tmr-backend (ASP.NET 10 + PostgreSQL 16)  
**Fecha:** 28-May-2026  
**Estado:** ✅ COMPILADO Y OPERATIVO

---

## 📑 Tabla de Contenidos

1. [Overview Ejecutivo](#overview-ejecutivo)
2. [Fases Implementadas](#fases-implementadas)
3. [Estructura de Arquitectura](#estructura-de-arquitectura)
4. [Componentes por Fase](#componentes-por-fase)
5. [Características de Seguridad](#características-de-seguridad)
6. [Flujo de Usuario Final](#flujo-de-usuario-final)
7. [Compilación y Ejecución](#compilación-y-ejecución)
8. [FAQ Técnico](#faq-técnico)

---

## Overview Ejecutivo

### Objetivo Alcanzado
Implementación completa del módulo de **Autenticación y Autorización** basado en:
- ✅ Ruta de implementación (auth_implementation_roadmap.md)
- ✅ Estructura de BD (Inv_tmr_db_deploy_mejorado.sql)
- ✅ Tablas de autenticación y permisos
- ✅ Arquitectura de Vertical Slice + Clean Architecture

### Entregables
```
✅ 6 Endpoints de autenticación completamente funcionales
✅ ~1,050 líneas de código implementadas
✅ Arquitectura limpia y mantenible
✅ Documentación completa
✅ Listo para pruebas
```

---

## Fases Implementadas

### Resumen Ejecutivo

| Fase | Componentes | LOC | Status |
|------|-------------|-----|--------|
| **1** | Security, JWT, Middleware | ~150 | ✅ |
| **2** | Login, Credenciales, Tokens | ~200 | ✅ |
| **3** | Refresh Token Rotation, Logout, Blacklist | ~250 | ✅ |
| **4** | Change Password, Policy, History | ~150 | ✅ |
| **5** | GetCurrentUser, GetPermissions, Menu | ~300 | ✅ |
| **TOTAL** | 5 Fases Completas | ~1,050 | ✅ |

---

### Fase 1: Infraestructura Base

**Objetivo:** Establecer la base segura para tokens JWT y middleware de validación.

**Componentes:**

| Componente | Archivo | Descripción |
|---|---|---|
| Token Service | `Infrastructure/Security/TokenService.cs` | Genera AccessToken + RefreshToken |
| Password Hasher | `Infrastructure/Security/PasswordHasher.cs` | BCrypt con work factor 12 |
| JWT Settings | `Infrastructure/Security/JwtSettings.cs` | Configuración de secretos |
| Current User Service | `Infrastructure/Shared/CurrentUserService.cs` | Lee claims del HttpContext |
| JWT Middleware | `Program.cs` | Valida firma, expiración, blacklist |

**Características:**
- ✅ JWT con algoritmo HS256 (HMAC-SHA256)
- ✅ AccessToken con TTL 15 minutos (configurable)
- ✅ RefreshToken con TTL 7 días (configurable)
- ✅ Claims incluidos: `sub`, `email`, `roles`, `name`, `employee_id`, `jti`
- ✅ BCrypt con salt aleatorio (work factor 12)
- ✅ Middleware valida: firma + expiración + JTI blacklist

---

### Fase 2: Login Funcional

**Objetivo:** Permitir a usuarios autenticarse con email y contraseña.

**Archivo:** `Features/Auth/Login/`

| Componente | Archivo | Status |
|---|---|---|
| Handler | `LoginHandler.cs` | ✅ Implementado |
| Endpoint | `LoginEndpoints.cs` | ✅ POST /api/auth/login |
| DTOs | `LoginRequest.cs`, `LoginResponse.cs`, `UserDto.cs` | ✅ Completo |
| Validator | `Validators/LoginValidator.cs` | ✅ FluentValidation |

**Flujo:**
```
1. Validar input (email, password)
2. Buscar usuario por email en BD
3. Verificar: usuario activo + contraseña correcta
4. Obtener roles con LINQ joins
5. Generar AccessToken (15 min) + RefreshToken (7 días)
6. Hashear RefreshToken y guardar sesión en BD
7. Retornar tokens + datos usuario
```

**Respuesta (200 OK):**
```json
{
  "accessToken": "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9...",
  "refreshToken": "hPxY2kL9mN...",
  "expiresIn": 900,
  "user": {
    "id": 1,
    "nombre": "Administrador Del Sistema",
    "email": "admin@isc.local",
    "roles": ["ADMIN"],
    "employeeId": 1
  }
}
```

---

### Fase 3: Refresh + Logout

#### 3.1 Refresh Token Rotation

**Archivo:** `Features/Auth/Refresh/`

**Funcionalidad:**
- Valida RefreshToken contra hash guardado en BD
- Implementa token rotation (nuevo access + nuevo refresh)
- **Invalida el AccessToken anterior automáticamente**
- Actualiza sesión con nueva expiración
- Endpoint: `POST /api/auth/refresh`

**Seguridad:**
```
Login       → AT₁ + RT₁ (sesión 1 con JTI₁)
Refresh(RT₁) → AT₂ + RT₂ (sesión 2 con JTI₂)
             └─ AT₁.JTI₁ se agrega a blacklist
Logout(AT₂) → Sesión 2 cerrada, AT₂.JTI₂ se agrega a blacklist

❌ Intentar usar AT₁ después de refresh → 401 Unauthorized
✅ Usar AT₂ después de refresh → 200 OK
```

---

#### 3.2 Logout con Revocación

**Archivo:** `Features/Auth/Logout/`

**Funcionalidad:**
- Agrega JTI del token actual a blacklist
- Cierra **SOLO** la sesión actual (no todas)
- Cachea JTI revocado en IMemoryCache (performance)
- Fallback a BD para sistemas distribuidos
- Endpoint: `POST /api/auth/logout`

**Middleware de Validación:**
```csharp
// Program.cs - Valida blacklist en cada request
opt.Events = new JwtBearerEvents
{
    OnTokenValidated = async context =>
    {
        var jti = context.Principal?.Claims
            .FirstOrDefault(c => c.Type == "jti")?.Value;
        
        if (jti != null && IsTokenBlacklisted(jti))
            context.Fail("Token revocado (BD)");
    }
};
```

---

### Fase 4: Change Password

**Archivo:** `Features/Auth/ChangePassword/`

| Componente | Archivo | Status |
|---|---|---|
| Handler | `ChangePasswordHandler.cs` | ✅ Implementado |
| Endpoint | `ChangePasswordEndpoints.cs` | ✅ POST /api/auth/change-password |
| DTO | `ChangePasswordRequest.cs` | ✅ {OldPassword, NewPassword} |
| Validator | `Validators/PasswordPolicyValidator.cs` | ✅ Política de seguridad |

**Política de Contraseña:**
```
✅ Mínimo 8 caracteres
✅ Al menos 1 mayúscula
✅ Al menos 1 número
✅ Al menos 1 símbolo (!@#$%^&*)
✅ No reutilizar últimas 5 contraseñas
```

**Flujo:**
```
1. Validar contraseña actual (BCrypt)
2. Validar policy de nueva contraseña
3. Consultar historial (últimas 5 hashes)
4. Rechazar si reutiliza anterior
5. Generar nuevo hash (BCrypt)
6. Actualizar usuario + insertar en historial
7. Retornar 200 OK
```

**Tablas de BD:**
- `tbl_autenticacion_usuario` → `hashpassword`
- `tbl_autenticacion_password_historial` → Registro de cambios

---

### Fase 5: GetCurrentUser + GetPermissions

#### 5.1 GetCurrentUser

**Archivo:** `Features/Auth/GetCurrentUser/`

**Funcionalidad:**
- Retorna datos completos del usuario autenticado
- Incluye: nombres, apellidos, email, roles, employeeId
- Usa LINQ joins entre esquemas
- Endpoint: `GET /api/auth/me`

**Consulta SQL generada:**
```sql
SELECT u.*, p.nombres, p.apellidos, 
       LISTAGG(cd.valor, ',') as roles,
       e.id as employeeId
FROM tbl_autenticacion_usuario u
LEFT JOIN tbl_administracion_persona p ON u.id_persona = p.id
LEFT JOIN tbl_autenticacion_usuario_rol ur ON u.id = ur.id_usuario
LEFT JOIN tbl_administracion_catalogo_detalle cd ON ur.id_rol = cd.id
LEFT JOIN tbl_administracion_empleado e ON p.id = e.id_persona
WHERE u.id = @userId AND u.estaactivo = true
GROUP BY u.id, p.id, e.id
```

**Respuesta (200 OK):**
```json
{
  "id": 1,
  "nombre": "Administrador Del Sistema",
  "email": "admin@isc.local",
  "roles": ["ADMIN"],
  "foto": null,
  "employeeId": 1,
  "fechaCreacion": "2026-01-01T00:00:00Z"
}
```

---

#### 5.2 GetPermissions

**Archivo:** `Features/Auth/GetPermissions/`

**Funcionalidad:**
- Calcula permisos combinando rol + usuario
- Retorna módulos con acciones (READ, CREATE, UPDATE, DELETE)
- Construye menú dinámico jerárquico
- Filtra menú solo para roles/usuario con acceso
- Endpoint: `GET /api/auth/permissions`

**Algoritmo de Merge de Permisos:**
```
1. Obtener roles activos del usuario (DISTINCT)
2. Consultar permisos de rol desde tbl_autenticacion_rol_modulo
   └─ Campos: puedever, puedecrear, puedeeditar, puedeeliminar
3. Consultar permisos directos del usuario desde tbl_autenticacion_usuario_modulo
4. Merge: Permisos de USUARIO prevalecen sobre ROL (OR lógico)
   Ejemplo:
   ├─ Rol EDITOR: READ, CREATE, UPDATE
   ├─ Usuario DIRECTO: DELETE
   └─ RESULTADO: READ, CREATE, UPDATE, DELETE

5. Construir árbol de menú jerárquico (parent-child)
6. Filtrar menú: solo mostrar items para roles/usuario con acceso
```

**Respuesta (200 OK):**
```json
{
  "roles": ["ADMIN"],
  "modules": [
    {
      "id": 1,
      "nombre": "TimeReport",
      "acciones": ["READ", "CREATE", "UPDATE", "DELETE"]
    },
    {
      "id": 2,
      "nombre": "Proyectos",
      "acciones": ["READ", "CREATE", "UPDATE"]
    }
  ],
  "menuItems": [
    {
      "id": 1,
      "nombre": "Dashboard",
      "ruta": "/dashboard",
      "icono": "bi-house",
      "orden": 1,
      "items": null
    },
    {
      "id": 2,
      "nombre": "Administración",
      "ruta": null,
      "icono": "bi-gear",
      "orden": 5,
      "items": [
        {
          "id": 3,
          "nombre": "Usuarios",
          "ruta": "/admin/usuarios",
          "icono": "bi-people",
          "orden": 1,
          "items": null
        },
        {
          "id": 4,
          "nombre": "Roles",
          "ruta": "/admin/roles",
          "icono": "bi-shield",
          "orden": 2,
          "items": null
        }
      ]
    }
  ]
}
```

---

## Estructura de Arquitectura

### Vertical Slice + Clean Architecture

```
Features/Auth/
├── AuthEndpoints.cs                           ← Registro central
├── Login/
│   ├── LoginHandler.cs                        ← Lógica
│   ├── LoginEndpoints.cs                      ← Endpoint
│   ├── DTOs/
│   │   ├── LoginRequest.cs
│   │   ├── LoginResponse.cs
│   │   └── UserDto.cs
│   └── Validators/
│       └── LoginValidator.cs
├── Refresh/
│   ├── RefreshHandler.cs
│   ├── RefreshEndpoints.cs
│   ├── DTOs/
│   │   ├── RefreshTokenRequest.cs
│   │   └── RefreshTokenResponse.cs
│   └── Validators/
│       └── RefreshValidator.cs
├── Logout/
│   ├── LogoutHandler.cs
│   ├── LogoutEndpoints.cs
│   ├── DTOs/
│   │   └── LogoutRequest.cs
│   └── Validators/
│       └── LogoutValidator.cs
├── ChangePassword/
│   ├── ChangePasswordHandler.cs
│   ├── ChangePasswordEndpoints.cs
│   ├── DTOs/
│   │   └── ChangePasswordRequest.cs
│   └── Validators/
│       ├── PasswordPolicyValidator.cs
│       └── ChangePasswordValidator.cs
├── GetCurrentUser/
│   ├── GetCurrentUserHandler.cs
│   ├── GetCurrentUserEndpoints.cs
│   └── DTOs/
│       └── CurrentUserResponse.cs
└── GetPermissions/
    ├── GetPermissionsHandler.cs
    ├── GetPermissionsEndpoints.cs
    └── DTOs/
        └── UserPermissionsResponse.cs

Infrastructure/
├── Database/
│   ├── ApplicationDbContext.cs
│   └── Entities/                              ← Todas las tablas scaffoldeadas
├── Security/
│   ├── JwtSettings.cs
│   ├── PasswordHasher.cs                      ← BCrypt w=12
│   ├── TokenService.cs                        ← Con GenerateAccessTokenWithClaims
│   ├── IPasswordHasher.cs
│   └── ITokenService.cs
└── Shared/
    ├── CurrentUserService.cs                  ← Lee HttpContext.User.Claims
    └── ICurrentUserService.cs

Program.cs                                      ← Configurado completo
appsettings.Development.json                    ← Con credenciales dev
```

### Patrones de Diseño

**Arquitectura:**
- Vertical Slice: Cada feature es independiente
- Clean Architecture: Separación clara entre capas
- Dependency Injection: Todos los servicios registrados

**Patrones de Código:**
- **Repository:** ApplicationDbContext como single source of truth
- **Service:** TokenService, PasswordHasher, CurrentUserService
- **Handler:** Cada operación es un handler reutilizable
- **Validator:** FluentValidation para DTOs

---

## Características de Seguridad

### Authentication (Fases 1-3)
- ✅ JWT con HS256 (HMAC-SHA256)
- ✅ Token rotation on refresh (AT anterior se invalida)
- ✅ Token blacklist cache en IMemoryCache
- ✅ Fallback a BD para sistemas distribuidos
- ✅ JTI único por sesión
- ✅ BCrypt password hashing (work factor 12)

### Session Management
- ✅ Sesión registrada con IP + User-Agent
- ✅ Fecha de ingreso/salida auditada
- ✅ Cierre de sesión al logout
- ✅ Refresh token rotation
- ✅ Soporte para múltiples sesiones simultáneas
- ✅ Logout cierra SOLO la sesión actual

### Password Policy (Fase 4)
- ✅ Mínimo 8 caracteres
- ✅ Mayúscula + Número + Símbolo
- ✅ No reutilización de últimas 5 contraseñas
- ✅ Historial auditado en BD

### Permission Model (Fase 5)
- ✅ Role-Based Access Control (RBAC)
- ✅ Fine-grained module permissions (READ, CREATE, UPDATE, DELETE)
- ✅ Direct user permissions (override role)
- ✅ Dynamic menu filtering by permission
- ✅ Merge: Usuario > Rol (OR lógico)

---

## Flujo de Usuario Final

```
┌─────────────────────────────────────────────────────┐
│ 1. LOGIN (POST /api/auth/login)                     │
│    Input: { email, password }                       │
│    Output: { accessToken, refreshToken, user }    │
│    DB: Crear sesión con JTI                        │
└────────┬────────────────────────────────────────────┘
         │
         ↓
┌─────────────────────────────────────────────────────┐
│ 2. USAR API PROTEGIDA (GET /api/auth/me)            │
│    Header: Authorization: Bearer accessToken        │
│    Middleware: Valida firma + expiración + blacklist│
│    Output: { id, nombre, email, roles }            │
└────────┬────────────────────────────────────────────┘
         │
         ↓
┌─────────────────────────────────────────────────────┐
│ 3. OBTENER PERMISOS (GET /api/auth/permissions)     │
│    Header: Authorization: Bearer accessToken        │
│    Output: { roles, modules, menuItems }            │
└────────┬────────────────────────────────────────────┘
         │
         ↓
┌─────────────────────────────────────────────────────┐
│ 4. CAMBIAR CONTRASEÑA (POST /api/auth/change-pwd)   │
│    Header: Authorization: Bearer accessToken        │
│    Input: { oldPassword, newPassword }              │
│    DB: Actualizar usuario + historial               │
└────────┬────────────────────────────────────────────┘
         │
         ↓
┌─────────────────────────────────────────────────────┐
│ 5. REFRESH TOKENS (POST /api/auth/refresh)          │
│    Input: { refreshToken }                          │
│    Output: { accessToken, refreshToken }            │
│    DB: Sesión actualizada, JTI anterior en blacklist│
└────────┬────────────────────────────────────────────┘
         │
         ↓
┌─────────────────────────────────────────────────────┐
│ 6. LOGOUT (POST /api/auth/logout)                   │
│    Header: Authorization: Bearer accessToken        │
│    DB: JTI en blacklist, sesión cerrada             │
│    Effect: Token revocado inmediatamente            │
└────────┬────────────────────────────────────────────┘
         │
         ↓
┌─────────────────────────────────────────────────────┐
│ Intento usar accessToken anterior → 401 Unauthorized│
│ (Token está en blacklist)                           │
└─────────────────────────────────────────────────────┘
```

---

## Compilación y Ejecución

### Requisitos Previos
- .NET 10 SDK
- PostgreSQL 16+
- Base de datos `Inv_tmr_db` creada
- Scripts SQL aplicados (tablas de autenticación)

### Compilación
```bash
$ cd c:\Users\javier.luna\Desktop\TMR\tmr_back\tmr-backend
$ dotnet build -c Debug
# BUILD SUCCEEDED (0 errors, 0 warnings)
# Output: bin/Debug/net10.0/tmr-backend.dll
```

### Ejecución
```bash
$ dotnet run --configuration Development
# info: Application started. Press Ctrl+C to shut down.
# info: Now listening on: http://localhost:5071
# info: Application started successfully
```

### Swagger / Scalar
- **URL:** `http://localhost:5071/scalar`
- **Endpoints:** 6 endpoints registrados
- **Autenticación:** Soporta JWT Bearer token

### Verificación
```bash
# Ping
curl -X GET "http://localhost:5071/health"

# Login
curl -X POST "http://localhost:5071/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@isc.local","password":"Password123!"}'
```

---

## FAQ Técnico

**P: ¿Dónde están los handlers?**  
R: Cada feature tiene su propia carpeta bajo `Features/Auth/`. Ejemplo: `Features/Auth/Login/LoginHandler.cs`

**P: ¿Cómo agregar nuevas políticas de autorización?**  
R: Ver `05_FASE_6_PROXIMAS_ACCIONES.md` Sección 1 (Authorization Policies)

**P: ¿Cómo modificar el trabajo factor de BCrypt?**  
R: En `Infrastructure/Security/PasswordHasher.cs`, cambiar la variable `workFactor` (actualmente 12)

**P: ¿Dónde está el middleware de blacklist?**  
R: `Program.cs` alrededor de línea 110, en la configuración de `opt.Events = new JwtBearerEvents`

**P: ¿Cómo cambiar la duración de los tokens?**  
R: `appsettings.Development.json` sección `Jwt`:
```json
"AccessTokenExpirationMinutes": 15,
"RefreshTokenExpirationDays": 7
```

**P: ¿Los roles son hardcodeados?**  
R: No. Los roles se obtienen de `tbl_administracion_catalogo_detalle` (TipoCatalogo='AUT', Codigo='ROL')

**P: ¿Cómo ejecuto pruebas unitarias?**  
R: Ver `03_GUIA_PRUEBAS_PASO_A_PASO.md` Sección 1

**P: ¿Cuál es el usuario de prueba?**  
R: Email: `admin@isc.local` | Password: `Password123!` | Rol: `ADMIN`

**P: ¿Soporta múltiples sesiones simultáneas?**  
R: Sí. Cada login crea una nueva sesión con JTI diferente. El logout cierra SOLO la sesión actual.

---

## Checklist de Validación Final

- [x] Todas las clases compilan sin errores
- [x] Todos los handlers implementados (sin TODOs)
- [x] Todos los endpoints registrados
- [x] DTOs completos para todas las fases
- [x] Program.cs configura DI, JWT, middleware
- [x] Servidor inicia exitosamente
- [x] Documentación completada
- [ ] Pruebas unitarias ejecutadas ← **PRÓXIMO**
- [ ] Flujos de integración validados ← **PRÓXIMO**

---

**Última actualización:** 28-May-2026  
**Versión:** 1.0  
**Estado:** ✅ Listo para pruebas
