# Plan: Token Refresh + Seguridad

**Fecha**: 29-May-2026  
**Tema**: Flujo de refresh de tokens, interacción cliente-servidor y validaciones de seguridad  

---

## 1. Flujo General: Backend vs Cliente

### 1.1 Backend - Quién hace qué

| Operación | Componente | Cuándo |
|-----------|-----------|--------|
| Genera AT + RT nuevo | `RefreshHandler` | Al endpoint `/api/auth/refresh` |
| Extrae JTI del nuevo AT | `RefreshHandler` | Mismo refresh |
| Hashea el nuevo RT | `RefreshHandler` | Mismo refresh |
| Actualiza `Tokonsesion` (hash RT) | `RefreshHandler` → BD | Mismo refresh |
| Actualiza `UltimoJti` | `RefreshHandler` → BD | Mismo refresh |
| Actualiza `Horasalida` (exp RT) | `RefreshHandler` → BD | Mismo refresh |
| Agrega viejo JTI a blacklist | `RefreshHandler` → BD | Mismo refresh (token rotation) |

**Responsabilidad Backend:**
- ✅ Generar nuevo AT con expiración correcta (desde `appsettings`)
- ✅ Generar nuevo RT con expiración correcta (desde `appsettings`)
- ✅ Persistir cambios en BD (actualizar sesión)
- ✅ Invalidar viejo AT mediante blacklist
- ✅ Validar que RT anterior siga siendo válido
- ✅ Aplicar validaciones de seguridad (IP, UA, rate limiting)

### 1.2 Cliente - Quién hace qué

| Operación | Responsable | Cuándo |
|-----------|-------------|--------|
| Guardar AT en localStorage | Cliente | Después de login o refresh |
| Guardar RT en localStorage | Cliente | Después de login o refresh |
| Calcular expiración local | Cliente | Basado en `expiresIn` del response |
| Enviar AT en cada request | Cliente | En header `Authorization: Bearer <AT>` |
| Detectar expiración próxima | Cliente | Validar `tokenExpiration` antes de requests |
| Llamar a `/api/auth/refresh` | Cliente | Cuando AT próximo a expirar o 401 recibido |
| Actualizar tokens del response | Cliente | Después de refresh exitoso |

**Responsabilidad Cliente:**
- ✅ Guardar y mantener tokens en almacenamiento seguro (localStorage/sessionStorage)
- ✅ Calcular cuándo AT expira basado en `expiresIn`
- ✅ Iniciar refresh ANTES de que AT expire (proactivo)
- ✅ Manejo de 401: reintentar con refresh automático
- ✅ Actualizar localStorage con nuevos tokens del response
- ✅ Manejo de errores en refresh (logout si falla)

---

## 2. Flujo de Login

```
1. Cliente POST /api/auth/login
   {
     "email": "admin",
     "password": "password123"
   }

2. Backend Response:
   {
     "accessToken": "eyJhbGc...",
     "refreshToken": "abc123def456xyz789",
     "expiresIn": 60,  // segundos (leído de appsettings)
     "user": { 
       "id": 1, 
       "email": "admin", 
       "fullName": "Administrador Del Sistema"
     }
   }

3. Backend BD:
   TblAutenticacionSesion:
   ├─ Idusuario: 1
   ├─ Tokonsesion: HASH(abc123def456xyz789)
   ├─ UltimoJti: "630b31e6-72ec-4938-8059-3f663a9191e3" (del AT)
   ├─ Horaingreso: 2026-05-29T10:00:00Z
   ├─ Horasalida: 2026-06-05T10:00:00Z (7 días después)
   ├─ Direccionip: "192.168.1.5"
   ├─ Agenteusuario: "Mozilla/5.0..."
   └─ Estaactiva: true

4. Cliente:
   localStorage.setItem("accessToken", "eyJhbGc...")
   localStorage.setItem("refreshToken", "abc123def456xyz789")
   localStorage.setItem("tokenExpiration", Date.now() + 60000)  // 60 segundos
   localStorage.setItem("user", JSON.stringify(userObj))
```

---

## 3. Flujo de Refresh

### 3.1 Triggers para hacer Refresh

**Trigger 1: Proactivo (Cliente detecta expiración próxima)**
```javascript
const tokenExpiration = localStorage.getItem("tokenExpiration");
const timeUntilExpiry = tokenExpiration - Date.now();

if (timeUntilExpiry < 30000) { // Menos de 30 segundos
  if (!isRefreshing) {
    performRefresh(); // Llamar a refresh
  }
}
```

**Trigger 2: Reactivo (401 recibido)**
```javascript
// Interceptor de HTTP
if (response.status === 401) {
  const newTokens = await performRefresh();
  // Reintentar request original
}
```

### 3.2 Request de Refresh

```javascript
// Cliente
POST /api/auth/refresh
{
  "refreshToken": localStorage.getItem("refreshToken")
}
```

### 3.3 Validaciones en Backend

```csharp
// RefreshHandler - Línea por línea

// 1. Validar que RT no sea nulo
if (string.IsNullOrWhiteSpace(request.RefreshToken))
    throw new UnauthorizedAccessException("Refresh token inválido.");

// 2. Hashear el RT recibido
var refreshTokenHash = _tokenService.HashToken(request.RefreshToken);

// 3. Buscar sesión activa por hash
var sesion = await _db.TblAutenticacionSesions
    .Where(s => s.Tokonsesion == refreshTokenHash && s.Estaactiva == true)
    .FirstOrDefaultAsync();

if (sesion == null)
    throw new UnauthorizedAccessException("Refresh token no encontrado o inactivo.");

// 4. Validar que RT no haya expirado
if (sesion.Horasalida.HasValue && sesion.Horasalida <= DateTime.UtcNow)
    throw new UnauthorizedAccessException("Refresh token expirado.");

// ✅ VALIDACIÓN DE SEGURIDAD: IP
var currentUserIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
if (sesion.Direccionip != currentUserIp)
{
    // Opción A: Rechazar completamente
    throw new UnauthorizedAccessException(
        "Acceso desde IP diferente. Autentique de nuevo.");
    
    // Opción B: Loguear pero permitir (menos seguro)
    // logger.LogWarning($"Refresh de IP diferente: {sesion.Direccionip} → {currentUserIp}");
}

// ✅ VALIDACIÓN DE SEGURIDAD: User-Agent
var currentUserAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
if (sesion.Agenteusuario != currentUserAgent)
{
    // Similar a IP: Rechazar o loguear
    throw new UnauthorizedAccessException(
        "Acceso desde dispositivo diferente. Autentique de nuevo.");
}

// ✅ VALIDACIÓN DE SEGURIDAD: Rate Limiting
var recentRefreshes = await _db.TblAutenticacionSesions
    .Where(s => s.Idusuario == sesion.Idusuario && 
           s.Fechamodificacion > DateTime.UtcNow.AddMinutes(-5))
    .CountAsync();

if (recentRefreshes > 10) // Más de 10 refreshes en 5 minutos
    throw new UnauthorizedAccessException(
        "Demasiados intentos de refresh. Intente más tarde.");
```

### 3.4 Generación de Nuevos Tokens

```csharp
// En RefreshHandler

// 9. Generar nuevo AT
var newAccessToken = _tokenService.GenerateAccessTokenWithClaims(
    usuario, roles, fullName, employeeId);

// 9.5 Extraer JTI del nuevo AT
var handler = new JwtSecurityTokenHandler();
var tokenObj = handler.ReadJwtToken(newAccessToken);
var newJti = tokenObj.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;

// 9.6 Invalidar AT anterior mediante blacklist
if (!string.IsNullOrEmpty(sesion.UltimoJti))
{
    var oldBlacklistEntry = new TblAutenticacionTokenBlacklist
    {
        Token = sesion.UltimoJti,  // JTI anterior
        Fechaexpiracion = DateTime.UtcNow.AddMinutes(15),
        Activo = true,
        Usuariocreacion = usuario.Email,
        Fechacreacion = DateTime.UtcNow
    };
    await _db.TblAutenticacionTokenBlacklists.AddAsync(oldBlacklistEntry);
}

// 10. Generar nuevo RT (token rotation)
var (newRefreshToken, newExpiresAt) = _tokenService.GenerateRefreshToken();
var newRefreshTokenHash = _tokenService.HashToken(newRefreshToken);
```

### 3.5 Actualización en BD

```csharp
// 11. Actualizar la sesión con nuevos valores
sesion.Tokonsesion = newRefreshTokenHash;      // Nuevo hash RT
sesion.UltimoJti = newJti;                     // Nuevo JTI AT
sesion.Horasalida = newExpiresAt;              // Nueva exp del RT
sesion.Usuariomodificacion = usuario.Email;
sesion.Fechamodificacion = DateTime.UtcNow;

_db.TblAutenticacionSesions.Update(sesion);
await _db.SaveChangesAsync();  // ← PERSISTE EN BD
```

### 3.6 Response al Cliente

```csharp
// 12. Calcular expiresIn desde configuración
int accessTokenMinutes = _configuration.GetValue<int>("Jwt:AccessTokenMinutes", 15);
int expiresIn = accessTokenMinutes * 60; // Convertir a segundos

// 13. Response
return new RefreshTokenResponse(
    newAccessToken,
    newRefreshToken,
    expiresIn
);
```

**Response JSON:**
```json
{
  "newAccessToken": "eyJhbGc...",
  "newRefreshToken": "xyz789abc123def456",
  "expiresIn": 60  // segundos
}
```

### 3.7 Cliente Actualiza localStorage

```javascript
// Cliente recibe el response de refresh

const response = await fetch("/api/auth/refresh", {
  method: "POST",
  body: JSON.stringify({ 
    refreshToken: localStorage.getItem("refreshToken") 
  })
});

if (response.ok) {
  const data = await response.json();
  
  // Actualizar tokens
  localStorage.setItem("accessToken", data.newAccessToken);
  localStorage.setItem("refreshToken", data.newRefreshToken);
  
  // Actualizar expiración
  const expirationTime = Date.now() + (data.expiresIn * 1000);
  localStorage.setItem("tokenExpiration", expirationTime);
  
  // Reintentar request original si estaba en 401
  return retryOriginalRequest();
} else {
  // Refresh falló → Logout
  logout();
  redirectToLogin();
}
```

---

## 4. Comparativa: Tres Enfoques

### 4.1 Enfoque 1: Refresh Automático Silencioso

```
AT expira → Cliente intenta request → 401 recibido
  → Refresh automático en background
  → Cliente reintentar request con nuevo AT
  → Usuario no se entera
```

| Aspecto | Valor |
|--------|-------|
| UX | ⭐⭐⭐ Excelente, sin interrupciones |
| Seguridad | ⭐⭐ Media, menos control |
| Complejidad | ⭐⭐ Sencillo |
| Ideal para | Apps normales, SaaS |

### 4.2 Enfoque 2: Popup Manual

```
AT próximo a expirar (detectado por cliente)
  → Mostrar popup: "Sesión próxima a expirar, ¿continuar?"
  ├─ Sí → Refresh + Continuar navegando
  └─ No → Logout + Ir a login
```

| Aspecto | Valor |
|--------|-------|
| UX | ⭐⭐ Intermedio, puede ser molesto |
| Seguridad | ⭐⭐⭐ Alta, usuario autoriza explícitamente |
| Complejidad | ⭐⭐⭐ Moderado, requiere UI |
| Ideal para | Banking, sistemas críticos |

### 4.3 Enfoque 3: Híbrido (RECOMENDADO)

```
Caso A: Usuario ACTIVO en la app
  ├─ AT próximo a expirar
  └─ Refresh AUTOMÁTICO silencioso ✓

Caso B: Usuario INACTIVO (no ha tocado nada en 5 min)
  ├─ AT próximo a expirar
  ├─ Mostrar popup: "¿Continuar en la sesión?"
  ├─ Sí → Refresh + Continuar
  └─ No → Logout + Login
```

| Aspecto | Valor |
|--------|-------|
| UX | ⭐⭐⭐ Excelente, natural |
| Seguridad | ⭐⭐⭐ Alta, control donde importa |
| Complejidad | ⭐⭐⭐ Moderado, con lógica de actividad |
| Ideal para | **RECOMMENDED PARA TU BACKEND** |

---

## 5. Validaciones de Seguridad Propuestas

### 5.1 IP Address Validation

**Problema:** Si alguien intercepta RT, no puede usarlo desde diferente IP.

```csharp
// En RefreshHandler
var currentIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

if (sesion.Direccionip != currentIp)
{
    logger.LogWarning($"Refresh attempt from different IP: " +
        $"Original={sesion.Direccionip}, Current={currentIp}, User={usuario.Email}");
    
    throw new UnauthorizedAccessException(
        "Refresh desde IP diferente. Autentique de nuevo.");
}
```

**Consideraciones:**
- ✅ Muy efectivo contra RT interceptados
- ⚠️ VPN puede cambiar IP (usuario debe hacer login de nuevo)
- ✅ Cada login = nueva sesión, así que no hay "bloqueo permanente"

### 5.2 User-Agent Validation

**Problema:** Si alguien intercepta RT desde diferente navegador/SO.

```csharp
var currentUA = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();

if (sesion.Agenteusuario != currentUA)
{
    logger.LogWarning($"Refresh from different User-Agent: User={usuario.Email}");
    
    throw new UnauthorizedAccessException(
        "Refresh desde dispositivo diferente. Autentique de nuevo.");
}
```

**Consideraciones:**
- ✅ Detecta cambios de navegador o SO
- ⚠️ User-Agent puede variar por updates (Firefox 118 → Firefox 119)
- Alternativa: Comparar user agent fuzzy (solo SO, no versión exacta)

### 5.3 Rate Limiting

**Problema:** Atacante intenta múltiples refreshes muy rápido.

```csharp
var recentRefreshes = await _db.TblAutenticacionSesions
    .Where(s => s.Idusuario == usuario.Id && 
           s.Fechamodificacion > DateTime.UtcNow.AddMinutes(-5))
    .CountAsync();

if (recentRefreshes > 10) // Umbral configurable
{
    logger.LogWarning($"Rate limit exceeded for user {usuario.Email}");
    
    throw new UnauthorizedAccessException(
        "Demasiados intentos de refresh. Intente en 5 minutos.");
}
```

**Consideraciones:**
- ✅ Simple pero efectivo
- ✅ Impide brute force
- ⚠️ Puede afectar usuario legítimo con conexión inestable

---

## 6. Escenarios de Ataque y Defensa

### 6.1 Escenario: Atacante Intercepta RT

```
1. USUARIO en IP 192.168.1.5
   └─ Login, recibe AT + RT
      Sesión #1: {ip: 192.168.1.5, ua: "Chrome", rt_hash: abc123}

2. ATACANTE intercepta RT (abc123)
   └─ Intenta refresh desde IP 200.200.200.200
      Backend valida:
      ├─ RT encontrado? ✓ SÍ
      ├─ RT expirado? ✗ NO
      ├─ IP coincide? ✗ NO (192.168.1.5 != 200.200.200.200)
      └─ ❌ RECHAZA - "IP diferente"

3. USUARIO legítimo sigue funcionando normalmente
   └─ Si necesita cambiar IP: Hace login de nuevo (sesión #2)
```

✅ **DEFENSA EXITOSA**

### 6.2 Escenario: Atacante con Mismo User-Agent

```
1. ATACANTE intercepta RT (abc123)
2. ATACANTE con MISMA configuración de navegador (Chrome Windows)
   └─ Intenta refresh desde diferente IP
      Backend valida:
      ├─ IP coincide? ✗ NO
      └─ ❌ RECHAZA
```

✅ **DEFENSA EXITOSA** (IP es la primera línea)

### 6.3 Escenario: Brute Force Rate Limiting

```
1. ATACANTE intenta refresh 50 veces en 1 minuto
   └─ Tras 10 intentos: ❌ RECHAZA por rate limit

2. USUARIO legítimo puede hacer refresh 10 veces en 5 minutos
   └─ Suficiente para casos normales
```

✅ **DEFENSA EFECTIVA**

---

## 7. Plan de Cambios a BD

### 7.1 TblAutenticacionSesion (Sin cambios necesarios)

Ya almacena:
- `Tokonsesion`: Hash del RT actual
- `UltimoJti`: JTI del AT actual
- `Horasalida`: Expiración del RT
- `Direccionip`: IP del login
- `Agenteusuario`: User-Agent del login
- `Fechamodificacion`: Última modificación

### 7.2 TblAutenticacionTokenBlacklist (Reutilizar)

Ya almacena:
- `Token`: JTI del AT (para invalidad AT revocados)

**Usar para ambos:**
- AT revocados (JTI)
- RT revocados (hash RT) ← CONSIDERAR EN FUTURO

---

## 8. Cambios en Código Necesarios

### 8.1 RefreshHandler.cs

```csharp
// Agregar validaciones DESPUÉS de validar RT expiración

// Validación IP
var currentIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
if (sesion.Direccionip != currentIp)
{
    throw new UnauthorizedAccessException("Refresh desde IP diferente. Autentique de nuevo.");
}

// Validación User-Agent
var currentUA = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString();
if (sesion.Agenteusuario != currentUA)
{
    throw new UnauthorizedAccessException("Refresh desde dispositivo diferente. Autentique de nuevo.");
}

// Rate Limiting
var recentRefreshes = await _db.TblAutenticacionSesions
    .Where(s => s.Idusuario == usuario.Id && 
           s.Fechamodificacion > DateTime.UtcNow.AddMinutes(-5))
    .CountAsync();

if (recentRefreshes > 10)
{
    throw new UnauthorizedAccessException("Demasiados intentos de refresh.");
}
```

### 8.2 appsettings.json (Sin cambios)

```json
{
  "Jwt": {
    "AccessTokenMinutes": 1,
    "RefreshTokenDays": 7
  }
}
```

### 8.3 appsettings.Development.json (Actualizar)

```json
{
  "Jwt": {
    "AccessTokenMinutes": 1,  // ← Ya fue corregido
    "RefreshTokenDays": 7
  }
}
```

---

## 9. Implementación en Cliente (Pseudocódigo)

### 9.1 Setup inicial

```javascript
class AuthService {
  async login(email, password) {
    const response = await fetch("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({ email, password })
    });
    
    if (response.ok) {
      const data = await response.json();
      this.storeTokens(data);
      return data.user;
    }
    throw new Error("Login failed");
  }
  
  storeTokens(loginResponse) {
    localStorage.setItem("accessToken", loginResponse.accessToken);
    localStorage.setItem("refreshToken", loginResponse.refreshToken);
    
    // Guardar expiración
    const expirationTime = Date.now() + (loginResponse.expiresIn * 1000);
    localStorage.setItem("tokenExpiration", expirationTime);
  }
}
```

### 9.2 Interceptor HTTP

```javascript
// Ejecutarse ANTES de cada request
async function beforeRequest(request) {
  const tokenExpiration = localStorage.getItem("tokenExpiration");
  const now = Date.now();
  const timeRemaining = tokenExpiration - now;
  
  // Si faltan menos de 30 segundos, hacer refresh proactivo
  if (timeRemaining < 30000 && timeRemaining > 0) {
    try {
      await refreshTokens();
    } catch (error) {
      // Refresh falló, forzar logout
      logout();
    }
  }
  
  // Agregar AT al header
  const accessToken = localStorage.getItem("accessToken");
  request.headers["Authorization"] = `Bearer ${accessToken}`;
}

// Si recibe 401
async function onUnauthorized() {
  try {
    await refreshTokens();
    // Reintentar request original
    return retryRequest();
  } catch (error) {
    logout();
  }
}

async function refreshTokens() {
  const refreshToken = localStorage.getItem("refreshToken");
  
  const response = await fetch("/api/auth/refresh", {
    method: "POST",
    body: JSON.stringify({ refreshToken })
  });
  
  if (response.ok) {
    const data = await response.json();
    
    // Actualizar tokens
    localStorage.setItem("accessToken", data.newAccessToken);
    localStorage.setItem("refreshToken", data.newRefreshToken);
    localStorage.setItem(
      "tokenExpiration", 
      Date.now() + (data.expiresIn * 1000)
    );
  } else {
    throw new Error("Refresh failed");
  }
}
```

---

## 10. Casos Especiales

### 10.1 Usuario Cambia IP (VPN, Móvil a WiFi)

```
Escenario:
├─ Usuario en oficina: IP 192.168.1.5 (login)
├─ Va a casa con VPN: IP 10.0.0.1 (intenta refresh)
├─ Backend valida: 192.168.1.5 != 10.0.0.1
└─ ❌ RECHAZA refresh

Solución:
├─ Usuario puede hacer LOGIN DE NUEVO desde casa
├─ Se crea NUEVA SESIÓN con IP 10.0.0.1
└─ ✅ Funciona normal
```

**No es un "bloqueo permanente"** porque cada login crea nueva sesión.

### 10.2 Usuario con Múltiples Dispositivos

```
Escenario:
├─ Dispositivo A (Laptop, Chrome): Sesión #1
├─ Dispositivo B (Tablet, Safari): Sesión #2
├─ Dispositivo C (Teléfono, Chrome): Sesión #3

Cada dispositivo:
├─ Tiene su propia sesión en BD
├─ Sus propios tokens
└─ Puede hacer refresh independientemente
```

✅ **FUNCIONA CORRECTAMENTE** (sesiones independientes)

### 10.3 Atacante Intenta Secuestrar Sesión

```
Escenario:
├─ ATACANTE consigue RT interceptado
├─ ATACANTE está en IP 200.200.200.200
├─ ATACANTE intenta POST /api/auth/refresh

Backend:
├─ Valida RT: ✓ Existe
├─ Valida exp: ✓ No expirado
├─ Valida IP: ✗ DIFERENTE (original 192.168.1.5)
└─ ❌ RECHAZA: "IP diferente"

Resultado:
├─ ATACANTE bloqueado
├─ USUARIO legítimo NO es afectado
└─ Si USUARIO cambia IP: Puede hacer login de nuevo
```

✅ **SEGURIDAD GARANTIZADA**

---

## 11. Testing

### 11.1 Test: Refresh Token Válido

```csharp
[Fact]
public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
{
    // Arrange: Login, extraer tokens
    var loginResponse = await LoginUser("admin@example.com", "password");
    var refreshToken = loginResponse.RefreshToken;
    
    // Act: Llamar a refresh
    var refreshResponse = await RefreshTokens(refreshToken);
    
    // Assert
    Assert.NotNull(refreshResponse.NewAccessToken);
    Assert.NotNull(refreshResponse.NewRefreshToken);
    Assert.True(refreshResponse.ExpiresIn > 0);
}
```

### 11.2 Test: Refresh Token Expirado

```csharp
[Fact]
public async Task RefreshToken_WithExpiredToken_Returns401()
{
    // Arrange: RT expirado (simular)
    var expiredRT = "expired_token_hash";
    
    // Act & Assert
    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => RefreshTokens(expiredRT));
    Assert.Contains("expirado", ex.Message);
}
```

### 11.3 Test: Refresh desde IP Diferente

```csharp
[Fact]
public async Task RefreshToken_FromDifferentIP_Returns401()
{
    // Arrange
    var loginResponse = await LoginUser(); // IP: 192.168.1.5
    var refreshToken = loginResponse.RefreshToken;
    
    // Act: Cambiar IP simulado en request
    var differentIp = "200.200.200.200";
    
    // Assert
    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => RefreshTokensFromIP(refreshToken, differentIp));
    Assert.Contains("IP diferente", ex.Message);
}
```

### 11.4 Test: Rate Limiting

```csharp
[Fact]
public async Task RefreshToken_ExceedsRateLimit_Returns429()
{
    var refreshToken = (await LoginUser()).RefreshToken;
    
    // Act: Intentar 11 refreshes en 5 minutos
    for (int i = 0; i < 10; i++)
    {
        await RefreshTokens(refreshToken);
    }
    
    // Assert: El 11vo debe fallar
    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
        () => RefreshTokens(refreshToken));
    Assert.Contains("demasiados intentos", ex.Message);
}
```

---

## 12. Plan de Implementación

### 12.1 Fase 1: Validaciones de Seguridad (30 min)

- [ ] Agregar validación IP en `RefreshHandler`
- [ ] Agregar validación User-Agent en `RefreshHandler`
- [ ] Agregar Rate Limiting en `RefreshHandler`
- [ ] Compilar y verificar sin errores

### 12.2 Fase 2: Testing (45 min)

- [ ] Crear tests unitarios para validaciones
- [ ] Pruebas manuales con Postman/curl
- [ ] Verificar que refresh funciona desde misma IP
- [ ] Verificar que rechaza desde diferente IP
- [ ] Verificar rate limit tras N intentos

### 12.3 Fase 3: Documentación Cliente (30 min)

- [ ] Crear guía para equipo frontend
- [ ] Ejemplos de interceptor HTTP
- [ ] Ejemplos de manejo de 401
- [ ] Ejemplos de almacenamiento de tokens

### 12.4 Fase 4: Integración (1-2 horas)

- [ ] Cliente implementa interceptor
- [ ] Cliente implementa refresh automático
- [ ] Cliente implementa almacenamiento seguro
- [ ] Testing end-to-end
- [ ] Validar flujo completo

---

## 13. Configuración Final

### 13.1 appsettings.Development.json (ACTUAL)

```json
{
  "Jwt": {
    "AccessTokenMinutes": 1,
    "RefreshTokenDays": 7
  }
}
```

### 13.2 appsettings.json (Para Producción - Ajustar)

```json
{
  "Jwt": {
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  }
}
```

---

## 14. Referencias y Links

- `Program.cs` - Línea 98+: Configuración JWT y validaciones
- `TokenService.cs` - Generación de AT y RT
- `RefreshHandler.cs` - Lógica de refresh (donde van las validaciones)
- `JwtSettings.cs` - Configuración de valores (sin defaults)
- `TblAutenticacionSesion` - BD, almacena IP y UA

---

## 15. Resumen Ejecutivo

| Aspecto | Status | Detalles |
|--------|--------|----------|
| **Flujo Base** | ✅ Implementado | Login → Refresh → Nuevos tokens |
| **Validaciones** | 🔄 Pendiente | IP, UA, Rate Limiting |
| **Seguridad** | ⭐⭐⭐ Alta | Con validaciones propuestas |
| **UX** | ⭐⭐⭐ Buena | Refresh proactivo, transparente |
| **Testing** | 🔄 Pendiente | Tests unitarios + integración |
| **Documentación Cliente** | 🔄 Pendiente | Guía para frontend |

---

**Fecha de Revisión**: 29-May-2026  
**Versión**: 1.0  
**Estado**: Listo para implementación
