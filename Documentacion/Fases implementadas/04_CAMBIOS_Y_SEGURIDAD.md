# 🔒 Cambios, Mejoras de Seguridad e Implementación

**Proyecto:** tmr-backend  
**Fecha:** 28-May-2026  
**Versión:** 1.0

---

## 📑 Tabla de Contenidos

1. [Problema Identificado](#problema-identificado)
2. [Solución Implementada](#solución-implementada)
3. [Comparativa Antes vs Después](#comparativa-antes-vs-después)
4. [Archivos Modificados](#archivos-modificados)
5. [Archivos Creados](#archivos-creados)
6. [Mejoras de Seguridad](#mejoras-de-seguridad)
7. [Pasos de Implementación](#pasos-de-implementación)

---

## Problema Identificado

### ❌ Seguridad Anterior (Insegura)

```
FLUJO CON PROBLEMAS:
════════════════════════════════════════════════════════════════════════════════════════════════════════════

1️⃣  LOGIN
    └─ usuario: admin@isc.local | password: Password123!
    
    RESPUESTA: { "accessToken": "AT₁ (jti: abc123)", "refreshToken": "RT₁" }
    
    BD SESIONES:
    ┌──────────────────────────────────────────┐
    │ id│usuario│sesion│ultim_jti│activa│      │
    ├──────────────────────────────────────────┤
    │1  │1      │hash1 │null     │true  │  ❌  │  JTI NO SE GUARDA
    └──────────────────────────────────────────┘

════════════════════════════════════════════════════════════════════════════════════════════════════════════

2️⃣  REFRESH TOKEN (enviando RT₁)
    └─ refreshToken: "RT₁"
    
    RESPUESTA: { "accessToken": "AT₂ (jti: xyz789)", "refreshToken": "RT₂" }
    
    BD SESIONES:
    ┌──────────────────────────────────────────┐
    │ id│usuario│sesion│ultim_jti│activa│      │
    ├──────────────────────────────────────────┤
    │1  │1      │hash2 │null     │true  │  ❌  │  TODAVÍA NO SE GUARDA
    └──────────────────────────────────────────┘
    
    ⚠️  PROBLEMA CRÍTICO: AT₁ (jti: abc123) SIGUE SIENDO VÁLIDO
        ├─ No fue invalidado
        ├─ Permanece en caché/validación JWT
        └─ ¡SUSCEPTIBLE A REPLAY ATTACKS!

════════════════════════════════════════════════════════════════════════════════════════════════════════════

3️⃣  LOGOUT (usando AT₁ ANTIGUO)
    └─ Authorization: Bearer AT₁ (jti: abc123)  ← ⚠️ TOKEN ANTIGUO/COMPROMETIDO
    
    PROCESAMIENTO:
    ├─ Middleware valida AT₁: FIRMA VÁLIDA ✓, EXPIRACIÓN VÁLIDA ✓
    ├─ No hay JTI en blacklist ❌
    ├─ Middleware ACEPTA el token
    ├─ LogoutHandler extrae userId del JWT
    ├─ Busca: WHERE idusuario = 1 AND estaactiva = true
    └─ CIERRA TODAS LAS SESIONES DEL USUARIO ❌❌❌
    
    BD SESIONES - DESPUÉS:
    ┌──────────────────────────────────────────┐
    │ id│usuario│sesion│ultim_jti│activa│      │
    ├──────────────────────────────────────────┤
    │1  │1      │hash2 │null     │false │  ❌  │  CERRADA INCORRECTAMENTE
    └──────────────────────────────────────────┘
    
    🔓 PROBLEMA DE SEGURIDAD GRAVE:
    ├─ Usuario con token comprometido cerró la sesión
    ├─ No pudo cerrar solo su sesión, cerró TODAS
    ├─ Si había otra sesión (web + mobile), se cierra también
    ├─ Sin validación de que sea el token actual
    └─ VIOLACIÓN DE: Session Isolation + Token Revocation

════════════════════════════════════════════════════════════════════════════════════════════════════════════
```

### Impacto de Seguridad
- 🔓 **Token Rotation Flaw:** Token antiguo sigue siendo válido
- 🔓 **Session Isolation Flaw:** Logout cierra TODAS las sesiones
- 🔓 **Replay Attack Vulnerable:** AT anterior puede reutilizarse
- 🔓 **Multi-Session Issue:** No hay soporte para sesiones simultáneas
- 🔓 **No Audit Trail:** No se rastrea qué JTI corresponde a cada sesión

---

## Solución Implementada

### ✅ Seguridad Nueva (Implementada)

```
FLUJO SEGURO:
════════════════════════════════════════════════════════════════════════════════════════════════════════════

1️⃣  LOGIN
    └─ usuario: admin@isc.local | password: Password123!
    
    RESPUESTA: { "accessToken": "AT₁ (jti: abc123)", "refreshToken": "RT₁" }
    
    BD SESIONES:
    ┌───────────────────────────────────────────────────────────────────────────┐
    │ id│usuario│sesion │ultim_jti│activa│creación                │              │
    ├───────────────────────────────────────────────────────────────────────────┤
    │1  │1      │hash1  │abc123   │true  │2026-05-28 10:00        │  ✅ GUARDADO │
    └───────────────────────────────────────────────────────────────────────────┘

════════════════════════════════════════════════════════════════════════════════════════════════════════════

2️⃣  REFRESH TOKEN (enviando RT₁)
    └─ refreshToken: "RT₁"
    
    RESPUESTA: { "accessToken": "AT₂ (jti: xyz789)", "refreshToken": "RT₂" }
    
    BD SESIONES:
    ┌───────────────────────────────────────────────────────────────────────────┐
    │ id│usuario│sesion │ultim_jti│activa│modificación            │              │
    ├───────────────────────────────────────────────────────────────────────────┤
    │1  │1      │hash2  │xyz789   │true  │2026-05-28 10:05        │  ✅ ACTUALIZADO
    └───────────────────────────────────────────────────────────────────────────┘
    
    BD BLACKLIST:
    ┌──────────────────────────────────────────────────────────────┐
    │ token  │fechaexpiracion        │activo │creación            │
    ├──────────────────────────────────────────────────────────────┤
    │abc123 │2026-05-28 10:20       │true   │2026-05-28 10:05    │  ✅ EN BLACKLIST
    └──────────────────────────────────────────────────────────────┘
    
    ✅ SEGURIDAD: AT₁ (jti: abc123) ES INVALIDADO AUTOMÁTICAMENTE

════════════════════════════════════════════════════════════════════════════════════════════════════════════

3️⃣  INTENTO LOGOUT (usando AT₁ ANTIGUO) - ❌ RECHAZADO
    └─ Authorization: Bearer AT₁ (jti: abc123)  ← TOKEN ANTIGUO
    
    PROCESAMIENTO:
    ├─ Middleware valida AT₁
    ├─ Extrae JTI: abc123
    ├─ Busca en blacklist cache: abc123 ✓ ENCONTRADO
    ├─ Consulta BD si no está en caché
    ├─ Rechaza: "Token revocado (BD)"
    └─ RETORNA 401 Unauthorized
    
    RESPUESTA:
    ❌ 401 Unauthorized
    { "error": "Token revocado (BD)" }
    
    ✅ SEGURIDAD: Token comprometido es rechazado inmediatamente

════════════════════════════════════════════════════════════════════════════════════════════════════════════

4️⃣  LOGOUT (usando AT₂ ACTUAL) - ✅ EXITOSO
    └─ Authorization: Bearer AT₂ (jti: xyz789)  ← TOKEN ACTUAL
    
    PROCESAMIENTO:
    ├─ Middleware valida AT₂ ✓ (en BD, no en blacklist)
    ├─ LogoutHandler extrae JTI: xyz789
    ├─ Busca: WHERE idusuario = 1 AND estaactiva = true AND ultim_jti = xyz789
    ├─ Cierra SOLO esta sesión (no todas)
    └─ Agrega JTI a blacklist
    
    BD SESIONES - DESPUÉS:
    ┌───────────────────────────────────────────────────────────────────────────┐
    │ id│usuario│sesion │ultim_jti│activa│horasalida              │              │
    ├───────────────────────────────────────────────────────────────────────────┤
    │1  │1      │hash2  │xyz789   │false │2026-05-28 10:06       │  ✅ CERRADA  │
    └───────────────────────────────────────────────────────────────────────────┘
    
    BD BLACKLIST:
    ┌──────────────────────────────────────────────────────────────┐
    │ token  │fechaexpiracion        │activo │creación            │
    ├──────────────────────────────────────────────────────────────┤
    │abc123 │2026-05-28 10:20       │true   │2026-05-28 10:05    │
    │xyz789 │2026-05-28 10:20       │true   │2026-05-28 10:06    │  ✅ NUEVO
    └──────────────────────────────────────────────────────────────┘
    
    RESPUESTA:
    ✓ 200 OK
    { "message": "Sesión cerrada exitosamente." }

════════════════════════════════════════════════════════════════════════════════════════════════════════════

5️⃣  INTENTO USAR AT₂ (DESPUÉS DE LOGOUT) - ❌ RECHAZADO
    └─ Authorization: Bearer AT₂ (jti: xyz789)  ← YA REVOCADO
    
    PROCESAMIENTO:
    ├─ Middleware valida AT₂
    ├─ Extrae JTI: xyz789
    ├─ Busca en blacklist: xyz789 ✓ ENCONTRADO
    ├─ Rechaza: "Token revocado (BD)"
    └─ RETORNA 401 Unauthorized
    
    ✅ SEGURIDAD: Token revocado rechazado inmediatamente
```

---

## Comparativa Antes vs Después

| Característica | Antes ❌ | Después ✅ |
|---|---|---|
| **Token Rotation** | Token anterior sigue válido | Token anterior invalidado automáticamente |
| **Session Isolation** | Logout cierra TODAS las sesiones | Logout cierra SOLO la sesión actual |
| **JTI Tracking** | No se rastrea JTI por sesión | JTI guardado en sesión (ultim_jti) |
| **Blacklist** | No implementada | Implementada (cache + BD) |
| **Multi-Session** | No soportado | Soportado (web + mobile simultáneamente) |
| **Replay Protection** | Vulnerable | Protegido (token rotation) |
| **Revocation Speed** | N/A | Inmediata (cache) + fallback BD |

---

## Archivos Modificados

### 1. TblAutenticacionSesion.cs (Entity)

**Cambio:**
```csharp
// ANTES
public class TblAutenticacionSesion
{
    public int Id { get; set; }
    public int Idusuario { get; set; }
    public string? Tokensesion { get; set; }  // hash del refresh token
    public bool Estaactiva { get; set; }
    public DateTime Fechacreacion { get; set; }
    // ... otros campos
}

// DESPUÉS
public class TblAutenticacionSesion
{
    public int Id { get; set; }
    public int Idusuario { get; set; }
    public string? Tokensesion { get; set; }  // hash del refresh token
    public string? UltimoJti { get; set; }  // ✅ NUEVO - JTI del AT actual
    public bool Estaactiva { get; set; }
    public DateTime Fechacreacion { get; set; }
    // ... otros campos
}
```

**Propósito:** Rastrear el JTI específico de cada sesión para validaciones de logout

---

### 2. LoginHandler.cs

**Cambios:**

```csharp
// ANTES
var accessToken = _tokenService.GenerateAccessTokenWithClaims(
    usuario, roles, jti: Guid.NewGuid().ToString(), null);

_db.TblAutenticacionSesions.Add(new TblAutenticacionSesion
{
    Idusuario = usuario.Id,
    Tokensesion = sesionHash,
    Estaactiva = true,
    Fechacreacion = DateTime.UtcNow,
    // NO GUARDABA JTI ❌
});

// DESPUÉS
var jti = Guid.NewGuid().ToString();
var accessToken = _tokenService.GenerateAccessTokenWithClaims(
    usuario, roles, jti, null);

var sesion = new TblAutenticacionSesion
{
    Idusuario = usuario.Id,
    Tokensesion = sesionHash,
    UltimoJti = jti,  // ✅ NUEVO - Guardar JTI
    Estaactiva = true,
    Fechacreacion = DateTime.UtcNow,
    IpIngreso = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
    UserAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"],
    // ... otros campos
};
_db.TblAutenticacionSesions.Add(sesion);
```

**Propósito:** Guardar el JTI inicial para tracking de rotación de tokens

---

### 3. RefreshHandler.cs (⭐ CRÍTICO)

**Cambios:**

```csharp
// ANTES - Sin token rotation
var nuevoAccessToken = _tokenService.GenerateAccessTokenWithClaims(
    usuario, roles, jti: Guid.NewGuid().ToString(), null);

sesion.Tokonsesion = nuevoHash;
sesion.Fechamodificacion = DateTime.UtcNow;
_db.SaveChangesAsync();

// DESPUÉS - Con token rotation
var nuevoJti = Guid.NewGuid().ToString();
var nuevoAccessToken = _tokenService.GenerateAccessTokenWithClaims(
    usuario, roles, nuevoJti, null);

// ✅ PASO CRÍTICO: Invalidar el JTI anterior
var jtiAnterior = sesion.UltimoJti;
if (!string.IsNullOrEmpty(jtiAnterior))
{
    // Agregar a blacklist
    _db.TblAutenticacionTokenBlacklists.Add(new TblAutenticacionTokenBlacklist
    {
        Token = jtiAnterior,
        Fechaexpiracion = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
        Activo = true,
        Fechacreacion = DateTime.UtcNow
    });
    
    // Cachear para performance
    _memoryCache.Set(
        $"blacklist_{jtiAnterior}",
        true,
        TimeSpan.FromMinutes(_jwtSettings.AccessTokenExpirationMinutes)
    );
}

// Actualizar sesión con nuevo JTI
sesion.UltimoJti = nuevoJti;
sesion.Tokensesion = nuevoHash;
sesion.Fechamodificacion = DateTime.UtcNow;
_db.SaveChangesAsync();
```

**Propósito:** Implementar Token Rotation Security - invalidar token anterior automáticamente

---

### 4. LogoutHandler.cs (⭐ CRÍTICO)

**Cambios:**

```csharp
// ANTES - Cerraba TODAS las sesiones
var sesionesAActualizar = _db.TblAutenticacionSesions
    .Where(s => s.Idusuario == userId && s.Estaactiva)  // ❌ CIERRA TODAS
    .ToList();

foreach (var sesion in sesionesAActualizar)
{
    sesion.Estaactiva = false;
    sesion.Horasalida = DateTime.UtcNow;
}

// DESPUÉS - Cierra SOLO la sesión actual
var jti = context.Principal?.Claims
    .FirstOrDefault(c => c.Type == "jti")?.Value;

var sesionActual = _db.TblAutenticacionSesions
    .Where(s => 
        s.Idusuario == userId && 
        s.Estaactiva &&
        s.UltimoJti == jti)  // ✅ SOLO esta sesión
    .FirstOrDefault();

if (sesionActual != null)
{
    sesionActual.Estaactiva = false;
    sesionActual.Horasalida = DateTime.UtcNow;
    
    // Agregar JTI a blacklist
    _db.TblAutenticacionTokenBlacklists.Add(new TblAutenticacionTokenBlacklist
    {
        Token = jti,
        Fechaexpiracion = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
        Activo = true,
        Fechacreacion = DateTime.UtcNow
    });
    
    _db.SaveChangesAsync();
}
```

**Propósito:** Implementar Session Isolation - logout cierra SOLO la sesión actual

---

### 5. Program.cs (Middleware JWT - Sin cambios, ya existía)

**Validación de Blacklist:**
```csharp
.AddJwtBearer(opt =>
{
    opt.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            // Extraer JTI del token
            var jti = context.Principal?.Claims
                .FirstOrDefault(c => c.Type == "jti")?.Value;
            
            if (string.IsNullOrEmpty(jti))
            {
                context.Fail("JTI no encontrado en token");
                return;
            }
            
            // Validar en caché (performance)
            if (_memoryCache.TryGetValue($"blacklist_{jti}", out _))
            {
                context.Fail("Token revocado (cache)");
                return;
            }
            
            // Validar en BD (fallback para sistemas distribuidos)
            var isBlacklisted = await _db.TblAutenticacionTokenBlacklists
                .AnyAsync(b => b.Token == jti && b.Activo);
                
            if (isBlacklisted)
            {
                context.Fail("Token revocado (BD)");
            }
        }
    };
});
```

---

## Archivos Creados

### 1. SCRIPT_ALTER_SESIONS_TABLE.sql

**Contenido:**
```sql
-- Agregar columna ultim_jti para tracking de JTI
ALTER TABLE autenticacion.tbl_autenticacion_sesion
ADD COLUMN ultim_jti VARCHAR(500) NULL;

-- Crear índices para optimizar búsquedas
CREATE INDEX idx_autenticacion_sesion_ultim_jti 
ON autenticacion.tbl_autenticacion_sesion (ultim_jti);

CREATE INDEX idx_autenticacion_sesion_user_jti
ON autenticacion.tbl_autenticacion_sesion (id_usuario, ultim_jti)
WHERE esta_activa = true;

-- ROLLBACK (en caso de necesidad)
-- ALTER TABLE autenticacion.tbl_autenticacion_sesion DROP COLUMN ultim_jti;
-- DROP INDEX idx_autenticacion_sesion_ultim_jti;
-- DROP INDEX idx_autenticacion_sesion_user_jti;
```

**Propósito:** Agregar soporte de BD para JTI tracking

---

## Mejoras de Seguridad

### 1. Token Rotation Security ✅
```
Antes: AT después de refresh sigue siendo válido
Ahora: AT anterior se invalida automáticamente en blacklist
```

### 2. Session Isolation ✅
```
Antes: Logout cierra TODAS las sesiones del usuario
Ahora: Logout cierra SOLO la sesión actual (por JTI)
```

### 3. Replay Attack Protection ✅
```
Antes: Token comprometido puede reutilizarse
Ahora: Token rotativo invalida anterior automáticamente
```

### 4. Multi-Session Support ✅
```
Antes: No soporta web + mobile simultáneamente
Ahora: Cada sesión es independiente (JTI único)
```

### 5. Revocation Caching ✅
```
Antes: Sin caching de blacklist
Ahora: Cache en IMemoryCache + fallback a BD
```

### 6. Audit Trail ✅
```
Antes: No hay rastreo de qué JTI corresponde a qué sesión
Ahora: ultim_jti guardado en tbl_autenticacion_sesion
```

---

## Pasos de Implementación

### Paso 1: Ejecutar Script SQL
```bash
cd c:\Users\javier.luna\Desktop\TMR\tmr_back\tmr-backend
psql -U postgres -d Inv_tmr_db -f Documentacion/Fases\ implementadas/SCRIPT_ALTER_SESIONS_TABLE.sql
```

### Paso 2: Compilar
```bash
dotnet build -c Debug
# BUILD SUCCEEDED
```

### Paso 3: Validar en BD
```sql
\d tbl_autenticacion_sesion
-- Debe mostrar columna: ultim_jti
```

### Paso 4: Probar Token Rotation
Ver [03_GUIA_PRUEBAS_PASO_A_PASO.md](03_GUIA_PRUEBAS_PASO_A_PASO.md) Flujo 2

### Paso 5: Probar Session Isolation
Ver [03_GUIA_PRUEBAS_PASO_A_PASO.md](03_GUIA_PRUEBAS_PASO_A_PASO.md) Flujo 1

---

## Resumen de Cambios

```
ARQUITECTURA MEJORADA:
════════════════════════════════════════════════════════════════════════════════════════════════════════════

ANTES (Vulnerable):
   Login → AT₁ + RT₁ (sin JTI tracking)
   Refresh → AT₂ + RT₂ (AT₁ sigue válido ❌)
   Logout → Cierra TODAS las sesiones (sin JTI validation ❌)

DESPUÉS (Seguro):
   Login → AT₁ + RT₁ (JTI: abc123 guardado en BD)
   Refresh → AT₂ + RT₂ (JTI anterior en blacklist ✅)
   Logout → Cierra SOLO la sesión actual (por JTI ✅)

BENEFICIOS:
   ✅ Token rotation automático
   ✅ Replay attack protection
   ✅ Multi-session support
   ✅ Session isolation
   ✅ Immediate revocation
   ✅ Audit trail

════════════════════════════════════════════════════════════════════════════════════════════════════════════
```

---

**Última actualización:** 28-May-2026  
**Versión:** 1.0  
**Status:** ✅ Implementado y compilado
