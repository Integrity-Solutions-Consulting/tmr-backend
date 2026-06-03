# Plan de Pruebas: Token Refresh + Validaciones de Seguridad

**Fecha**: 01-Jun-2026  
**Versión**: 1.0  
**Estado**: Listo para Ejecución

---

## 1. Resumen Ejecutivo

Este documento describe el plan de pruebas para validar:
- ✅ Flujo completo de refresh de tokens
- ✅ Generación correcta de nuevos AT y RT
- ✅ Validaciones de seguridad (IP, User-Agent, Rate Limiting)
- ✅ Persistencia en BD
- ✅ Casos de error y manejo de excepciones

---

## 2. Escenarios de Prueba por Categoría

### 2.1 Pruebas de Flujo Exitoso

#### Test 2.1.1: Refresh Básico Exitoso

**Precondiciones:**
- Usuario logged in con AT válido y RT válido
- Sesión activa en BD con IP y User-Agent correcto
- RT no expirado

**Pasos:**
```bash
1. POST /api/auth/refresh
   Body: {
     "refreshToken": "abc123def456xyz789"
   }

2. Backend:
   - Hashea RT
   - Busca sesión
   - Valida expiración
   - Valida IP ✓ (coincide)
   - Valida User-Agent ✓ (coincide)
   - Valida Rate Limit ✓ (< 10 en 5 min)
   - Genera nuevo AT con claims
   - Genera nuevo RT
   - Actualiza sesión
   - Agrega JTI anterior a blacklist

3. Response: 200 OK
   {
     "newAccessToken": "eyJhbGc...",
     "newRefreshToken": "xyz789abc123def456",
     "expiresIn": 60
   }
```

**Validaciones Esperadas:**
- ✅ Status 200 OK
- ✅ AccessToken es JWT válido
- ✅ RefreshToken es string de 64 bytes encoded
- ✅ ExpiresIn = 60 segundos (desde config)
- ✅ BD: Sesión actualizada con nuevo hash y JTI
- ✅ BD: JTI anterior en blacklist

---

#### Test 2.1.2: Múltiples Refreshes Exitosos (Token Rotation)

**Precondiciones:**
- Usuario con sesión activa

**Pasos:**
```bash
1. Refresh #1: AT1 + RT1 → AT2 + RT2
2. Refresh #2: AT2 + RT2 → AT3 + RT3
3. Refresh #3: AT3 + RT3 → AT4 + RT4
```

**Validaciones Esperadas:**
- ✅ Cada refresh retorna nuevos tokens
- ✅ RT anterior no funciona (consumed)
- ✅ JTI de AT1 en blacklist
- ✅ JTI de AT2 en blacklist
- ✅ JTI de AT3 en blacklist
- ✅ Sesión.UltimoJti = JTI de AT4

---

### 2.2 Pruebas de Validaciones de Seguridad

#### Test 2.2.1: IP Address Validation - Rechazo

**Precondiciones:**
- Usuario logged in desde IP: 192.168.1.5
- Intenta refresh desde IP: 200.200.200.200

**Pasos:**
```bash
1. POST /api/auth/refresh
   Header: X-Forwarded-For: 200.200.200.200
   Body: {
     "refreshToken": "abc123def456xyz789"
   }

2. Backend valida:
   - Sesión.Direccionip = "192.168.1.5"
   - CurrentIP = "200.200.200.200"
   - 192.168.1.5 != 200.200.200.200 ✗
```

**Respuesta Esperada:**
- ❌ Status 401 Unauthorized
- 🔐 Mensaje: "Acceso desde dirección IP diferente. Por favor, autentique de nuevo."
- ✅ BD: No se actualizan tokens
- ✅ BD: No se crea nueva sesión

**Impacto de Seguridad:**
- Previene uso de RT interceptado desde diferente IP
- Atacante no puede usar RT sin acceso a misma red/VPN

---

#### Test 2.2.2: User-Agent Validation - Rechazo

**Precondiciones:**
- Usuario logged in con User-Agent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0"
- Intenta refresh con User-Agent: "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0)"

**Pasos:**
```bash
1. POST /api/auth/refresh
   Header: User-Agent: Mozilla/5.0 (iPhone; CPU iPhone OS 17_0)
   Body: {
     "refreshToken": "abc123def456xyz789"
   }

2. Backend valida:
   - Sesión.Agenteusuario = "Mozilla/5.0 (Windows NT 10.0...)"
   - CurrentUA = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0)"
   - No coinciden ✗
```

**Respuesta Esperada:**
- ❌ Status 401 Unauthorized
- 🔐 Mensaje: "Acceso desde dispositivo diferente. Por favor, autentique de nuevo."
- ✅ BD: No se actualizan tokens

**Impacto de Seguridad:**
- Previene uso de RT desde navegador diferente
- Detecta cambios SO o navegador (indicador de compromiso)

---

#### Test 2.2.3: Rate Limiting - Rechazo

**Precondiciones:**
- Usuario realiza 11 refreshes en menos de 5 minutos

**Pasos:**
```bash
1. Loop: for i in 1..11
     POST /api/auth/refresh
     Sleep 10 segundos

2. Refreshes 1-10: ✓ Success
3. Refresh 11:
   - Backend cuenta refreshes en últimos 5 min
   - Count = 11 (incluyendo el actual)
   - 11 > 10 ✗
```

**Respuesta Esperada (Refresh 11):**
- ❌ Status 401 Unauthorized
- 🔐 Mensaje: "Demasiados intentos de refresh. Intente más tarde."
- ✅ BD: No se actualiza sesión

**Impacto de Seguridad:**
- Previene brute force attacks
- Protege contra patrones de ataque automatizado

---

### 2.3 Pruebas de Validación de Tokens

#### Test 2.3.1: Refresh Token Inválido/Nulo

**Pasos:**
```bash
1. POST /api/auth/refresh
   Body: {
     "refreshToken": ""
   }
```

**Respuesta Esperada:**
- ❌ Status 401 Unauthorized
- 🔐 Mensaje: "Refresh token inválido."

---

#### Test 2.3.2: Refresh Token No Encontrado

**Pasos:**
```bash
1. POST /api/auth/refresh
   Body: {
     "refreshToken": "invalid_token_not_in_db_xyz"
   }
```

**Respuesta Esperada:**
- ❌ Status 401 Unauthorized
- 🔐 Mensaje: "Refresh token no encontrado o inactivo."

---

#### Test 2.3.3: Refresh Token Expirado

**Precondiciones:**
- Sesión en BD con Horasalida = 2026-05-25T10:00:00Z (ya pasó)
- RT aún en localStorage del cliente (pero expired en BD)

**Pasos:**
```bash
1. POST /api/auth/refresh
   Body: {
     "refreshToken": "expired_token"
   }
```

**Respuesta Esperada:**
- ❌ Status 401 Unauthorized
- 🔐 Mensaje: "Refresh token expirado."

---

### 2.4 Pruebas de Validación de Usuario

#### Test 2.4.1: Usuario Inactivo

**Precondiciones:**
- Usuario con Estaactivo = false

**Pasos:**
```bash
1. POST /api/auth/refresh
   Body: {
     "refreshToken": "valid_rt_for_inactive_user"
   }
```

**Respuesta Esperada:**
- ❌ Status 401 Unauthorized
- 🔐 Mensaje: "Usuario no encontrado o inactivo."

---

#### Test 2.4.2: Sesión Inactiva en BD

**Precondiciones:**
- Sesión con Estaactiva = false

**Pasos:**
```bash
1. POST /api/auth/refresh
   Body: {
     "refreshToken": "valid_rt_but_session_inactive"
   }
```

**Respuesta Esperada:**
- ❌ Status 401 Unauthorized
- 🔐 Mensaje: "Refresh token no encontrado o inactivo."

---

## 3. Escenarios de Flujo Completo (End-to-End)

### Escenario 3.1: Usuario Normal - Login + Refresh + Request

```
TIMELINE:
T0: Usuario hace LOGIN
    ├─ Recibe: AT (exp 60s), RT
    ├─ Guarda en localStorage: accessToken, refreshToken, tokenExpiration
    └─ BD: Sesión creada {id, hash(RT), JTI(AT), IP, UA}

T30: Cliente detecta AT próximo a expirar (30 sec left)
    ├─ POST /api/auth/refresh
    ├─ Backend ejecuta validaciones: IP ✓, UA ✓, RateLimit ✓
    ├─ Genera: AT2, RT2
    ├─ Actualiza BD: {hash(RT2), JTI(AT2), IP, UA}
    ├─ Agrega JTI(AT) a blacklist
    └─ Response: {AT2, RT2, expiresIn: 60}

T31: Cliente actualiza localStorage
    ├─ accessToken = AT2
    ├─ refreshToken = RT2
    ├─ tokenExpiration = now + 60000ms
    └─ continúa navegando

T60: Cliente hace request normal
    ├─ Header: Authorization: Bearer AT2
    └─ Server valida JTI(AT2) ∉ blacklist ✓ Permitido

T90: Cliente detecta AT próximo a expirar de nuevo
    └─ Repite refresh...
```

**Validaciones en Cada Paso:**
- ✅ Login crea sesión correctamente
- ✅ Primer refresh exitoso con todas validaciones
- ✅ Tokens nuevos funcionan
- ✅ AT anterior en blacklist
- ✅ Cliente calcula expiración correctamente

---

### Escenario 3.2: Atacante Intercepta RT

```
TIMELINE:
T0: USUARIO legítimo en IP 192.168.1.5, Chrome
    ├─ LOGIN exitoso
    └─ Recibe RT = "abc123def456xyz789"

T5: NETWORK SNIFFING (atacante captura RT en tránsito)
    └─ Atacante tiene: RT = "abc123def456xyz789"

T10: ATACANTE intenta refresh desde IP 200.200.200.200
    ├─ POST /api/auth/refresh con RT capturado
    ├─ Backend busca sesión: ✓ Encontrada
    ├─ Backend valida expiración: ✓ OK
    ├─ Backend valida IP: 
    │   - Esperada: 192.168.1.5
    │   - Actual: 200.200.200.200
    │   - Resultado: ✗ RECHAZADO
    └─ Response: 401 "IP diferente"

T11: USUARIO legítimo sigue usando tokens
    ├─ Ya tienen AT2, RT2 válidos
    └─ Sesión sigue activa con IP correcta
```

**Resultado:**
- 🔐 ATAQUE BLOQUEADO por validación de IP
- ✅ Usuario legítimo no afectado
- ✅ Sistema seguro

---

### Escenario 3.3: VPN + IP Cambia (Usuario Legítimo)

```
TIMELINE:
T0: Usuario en red normal (IP: 192.168.1.5)
    ├─ LOGIN
    └─ Sesión con IP: 192.168.1.5

T60: Usuario activa VPN (IP: 185.220.101.45)
     ├─ Intenta refresh
     ├─ Backend valida IP:
     │   - Esperada: 192.168.1.5
     │   - Actual: 185.220.101.45
     │   - Resultado: ✗ RECHAZADO
     └─ Response: 401 "IP diferente"

T61: Usuario debe hacer LOGIN de nuevo
     ├─ Proporciona credenciales
     ├─ Nueva sesión con IP: 185.220.101.45
     └─ Recibe nuevos AT + RT
```

**Consideraciones:**
- ⚠️ Experiencia: Usuario debe hacer login nuevamente
- ✅ Seguridad: Excelente protección
- 💡 Alternativa: Implementar "device fingerprinting" más flexible

---

## 4. Plan de Ejecución de Pruebas

### Fase 1: Pruebas Unitarias (MANUAL/UNIT TESTS)

```csharp
// En test project: tmr-backend.Tests

[TestClass]
public class RefreshHandlerTests
{
    [TestMethod]
    public async Task Handle_ValidRefreshToken_ReturnsNewTokens()
    {
        // Test 2.1.1
    }

    [TestMethod]
    public async Task Handle_DifferentIP_ThrowsUnauthorized()
    {
        // Test 2.2.1
    }

    [TestMethod]
    public async Task Handle_DifferentUserAgent_ThrowsUnauthorized()
    {
        // Test 2.2.2
    }

    [TestMethod]
    public async Task Handle_RateLimitExceeded_ThrowsUnauthorized()
    {
        // Test 2.2.3
    }

    [TestMethod]
    public async Task Handle_ExpiredRefreshToken_ThrowsUnauthorized()
    {
        // Test 2.3.3
    }
}
```

**Ejecución:**
```bash
cd tmr-backend
dotnet test
```

---

### Fase 2: Pruebas de Integración (POSTMAN/THUNDER CLIENT)

#### Request 1: Login Normal

```http
POST http://localhost:5000/api/auth/login
Content-Type: application/json

{
  "email": "admin",
  "password": "password123"
}
```

**Response Esperado:**
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "abc123def456xyz789",
  "expiresIn": 60,
  "user": {
    "id": 1,
    "email": "admin",
    "fullName": "Admin User"
  }
}
```

**Guardar en Variables (Postman):**
```
accessToken = {{response.body.accessToken}}
refreshToken = {{response.body.refreshToken}}
tokenExpiration = Date.now() + ({{response.body.expiresIn}} * 1000)
userIP = {{response.headers.X-Forwarded-For}} (o Connection.RemoteIpAddress)
```

---

#### Request 2: Refresh Token - Caso Exitoso

```http
POST http://localhost:5000/api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "{{refreshToken}}"
}
```

**Validar Response:**
- ✅ Status: 200
- ✅ Body.newAccessToken: es JWT válido
- ✅ Body.newRefreshToken: diferente al anterior
- ✅ Body.expiresIn: 60

**Guardar nuevo token:**
```
accessToken = {{response.body.newAccessToken}}
refreshToken = {{response.body.newRefreshToken}}
```

---

#### Request 3: Usar Nuevo Access Token

```http
GET http://localhost:5000/api/auth/current-user
Authorization: Bearer {{accessToken}}
```

**Validar:**
- ✅ Status: 200
- ✅ Retorna datos de usuario

---

#### Request 4: IP Address Validation - Rechazo

```http
POST http://localhost:5000/api/auth/refresh
Content-Type: application/json
X-Forwarded-For: 200.200.200.200

{
  "refreshToken": "{{refreshToken}}"
}
```

**Validar Response:**
- ❌ Status: 401
- ✅ Message: contiene "IP diferente"

---

#### Request 5: Rate Limiting - Test

```bash
# Loop 15 veces
for i in {1..15}; do
  curl -X POST http://localhost:5000/api/auth/refresh \
    -H "Content-Type: application/json" \
    -d "{\"refreshToken\": \"{{refreshToken}}\"}"
  
  if [ $i -le 10 ]; then
    echo "Refresh $i: ✓ Success expected"
  else
    echo "Refresh $i: ✗ RateLimit expected"
  fi
  
  sleep 5
done
```

**Validar:**
- ✅ Refreshes 1-10: Status 200
- ❌ Refreshes 11-15: Status 401 (Rate Limit)

---

## 5. Validación en Base de Datos

### Verificar Sesión Actualizada

```sql
-- Después de refresh exitoso
SELECT 
  Idusuario,
  Tokonsesion,           -- Debe cambiar (nuevo hash)
  UltimoJti,             -- Debe cambiar (nuevo JTI)
  Horasalida,            -- Debe extenderse 7 días
  Direccionip,           -- No debe cambiar
  Agenteusuario,         -- No debe cambiar
  Fechamodificacion      -- Actualizado a NOW()
FROM TblAutenticacionSesion
WHERE Idusuario = 1
ORDER BY Fechamodificacion DESC
LIMIT 1;
```

**Validaciones:**
- ✅ Tokonsesion: hash diferente al anterior
- ✅ UltimoJti: nuevo GUID
- ✅ Horasalida: ~7 días en el futuro
- ✅ Fechamodificacion: ahora

---

### Verificar Blacklist de JTI

```sql
-- Después de refresh exitoso
SELECT 
  Token,              -- JTI anterior
  Fechaexpiracion,    -- 15 min desde refresh
  Activo,             -- TRUE
  Fechacreacion       -- Ahora
FROM TblAutenticacionTokenBlacklist
WHERE Usuariocreacion = 'admin'
ORDER BY Fechacreacion DESC
LIMIT 3;
```

**Validaciones:**
- ✅ Token: contiene JTI antiguos
- ✅ Activo: TRUE
- ✅ Fechaexpiracion: futura (~15 min)

---

## 6. Checklist de Validación Final

### Core Functionality
- [ ] Refresh exitoso retorna nuevos AT y RT
- [ ] ExpiresIn = 60 segundos (desde config)
- [ ] Token rotation funciona (RT consumido después)
- [ ] BD actualiza sesión correctamente
- [ ] JTI anterior en blacklist

### Security Validations
- [ ] IP diferente → 401 Unauthorized
- [ ] User-Agent diferente → 401 Unauthorized
- [ ] Rate Limit (>10 en 5 min) → 401 Unauthorized
- [ ] Refresh token expirado → 401 Unauthorized
- [ ] RT inválido/nulo → 401 Unauthorized

### Error Handling
- [ ] Usuario inactivo → 401 Unauthorized
- [ ] Sesión inactiva → 401 Unauthorized
- [ ] Mensajes de error claros en español

### Integration
- [ ] Nuevo AT funciona en requests posteriores
- [ ] Viejo AT rechazado (en blacklist)
- [ ] Cliente puede guardar nuevos tokens
- [ ] Múltiples refreshes funcionan en secuencia

---

## 7. Matriz de Pruebas (Resumen)

| ID | Escenario | Entrada | Salida Esperada | Status | Fecha |
|----|-----------|---------|--------------------|--------|--------|
| 2.1.1 | Refresh Básico | RT válido | 200 + nuevos tokens | ⏳ Pendiente | |
| 2.1.2 | Token Rotation | RT × 3 | Tokens únicos c/vez | ⏳ Pendiente | |
| 2.2.1 | IP Diferente | RT + IP≠ | 401 "IP diferente" | ⏳ Pendiente | |
| 2.2.2 | UA Diferente | RT + UA≠ | 401 "dispositivo" | ⏳ Pendiente | |
| 2.2.3 | Rate Limit | 11 refreshes/5min | 401 "demasiados" | ⏳ Pendiente | |
| 2.3.1 | RT Inválido | RT="" | 401 "inválido" | ⏳ Pendiente | |
| 2.3.3 | RT Expirado | RT expirado | 401 "expirado" | ⏳ Pendiente | |
| 3.1 | E2E Normal | Login → Refresh → Request | Todo funciona | ⏳ Pendiente | |
| 3.2 | E2E Ataque IP | Interceptar RT + IP≠ | Bloqueado en paso 2 | ⏳ Pendiente | |

---

## 8. Próximos Pasos

1. **Crear Unit Tests** → tmr-backend.Tests/Features/Auth/Refresh/
2. **Ejecutar tests** → `dotnet test`
3. **Probar en Postman** con requests del apartado 4
4. **Validar BD** con queries del apartado 5
5. **Documentar resultados** en esta misma tabla
6. **Deploy a producción** cuando todos tests ✓

---

**Fin del Documento**
