# 🧪 Guía Completa de Pruebas Paso a Paso

**Proyecto:** tmr-backend  
**Fecha:** 28-May-2026  
**Tiempo estimado:** 2-3 horas

---

## 📑 Tabla de Contenidos

1. [Requisitos Previos](#requisitos-previos)
2. [Verificación de BD](#verificación-de-base-de-datos)
3. [Pruebas Unitarias (xUnit)](#pruebas-unitarias-xunit)
4. [Flujos de Prueba Completos](#flujos-de-prueba-completos)
5. [Comandos cURL Listos](#comandos-curl-listos)
6. [Pruebas de Seguridad](#pruebas-de-seguridad)
7. [Criterios de Aceptación](#criterios-de-aceptación)

---

## Requisitos Previos

### 1. Proyecto Compilado
```bash
cd c:\Users\javier.luna\Desktop\TMR\tmr_back\tmr-backend
dotnet build -c Debug
# ✅ BUILD SUCCEEDED
```

### 2. Base de Datos Operativa
- PostgreSQL 16+ ejecutándose
- Base de datos `Inv_tmr_db` creada
- Scripts SQL aplicados:
  - `Inv_tmr_db_deploy_mejorado.sql` (estructura)
  - `catalogos.sql` (catálogos maestros)
  - `02_insert_datos_autenticacion_tmr.sql` (usuarios de prueba)
  - `03_insert_permisos_rol_modulo_tmr.sql` (permisos rol-módulo)
  - `SCRIPT_ALTER_SESIONS_TABLE.sql` (columna ultim_jti para token rotation)

### 3. Configuración de appsettings

Verificar `appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=Inv_tmr_db;Username=postgres;Password=YOUR_PASSWORD;"
  },
  "Jwt": {
    "SecretKey": "your-secret-key-minimum-32-characters-long-1234567890",
    "Issuer": "inv-tmr-backend",
    "Audience": "time-report",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

---

## Verificación de Base de Datos

### 1. Conectarse a la BD
```sql
\c Inv_tmr_db
```

### 2. Verificar tablas del esquema autenticación
```sql
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'autenticacion' 
ORDER BY table_name;

-- Debe incluir:
-- - tbl_autenticacion_usuario
-- - tbl_autenticacion_sesion (con columna ultim_jti)
-- - tbl_autenticacion_token_blacklist
-- - tbl_autenticacion_password_historial
-- - tbl_autenticacion_modulo
-- - tbl_autenticacion_rol_modulo
-- - tbl_autenticacion_usuario_modulo
-- - tbl_autenticacion_menu
```

### 3. Verificar usuario de prueba
```sql
SELECT id, email, estaactivo, hashpassword 
FROM autenticacion.tbl_autenticacion_usuario 
WHERE email = 'admin@isc.local';

-- Debe retornar:
-- id=1, email=admin@isc.local, estaactivo=true
```

### 4. Verificar roles del usuario
```sql
SELECT ur.id, ur.idusuario, cd.valor AS nombre_rol
FROM autenticacion.tbl_autenticacion_usuario_rol ur
JOIN administracion.tbl_administracion_catalogo_detalle cd ON ur.idrol = cd.id
WHERE ur.idusuario = 1 AND ur.activo = true;

-- Debe retornar al menos un rol (ADMIN)
```

### 5. Verificar módulos
```sql
SELECT id, nombremodulo, activo 
FROM autenticacion.tbl_autenticacion_modulo 
LIMIT 5;

-- Debe retornar módulos como TimeReport, Proyectos, etc.
```

---

## Pruebas Unitarias (xUnit)

### Crear Proyecto de Tests

```bash
cd "c:\Users\javier.luna\Desktop\TMR\tmr_back"
dotnet new xunit -n tmr-backend.Tests
cd tmr-backend.Tests
dotnet add reference ../tmr-backend/tmr-backend.csproj
dotnet add package Moq
dotnet add package xunit
dotnet add package xunit.runner.visualstudio
```

### Ejemplo: Test de LoginHandler

**Archivo:** `tmr-backend.Tests/Features/Auth/LoginHandlerTests.cs`

```csharp
using Xunit;
using Moq;
using tmr_backend.Features.Auth.Login;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace tmr_backend.Tests.Features.Auth;

public class LoginHandlerTests
{
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsLoginResponse()
    {
        // Arrange
        var mockDb = new Mock<ApplicationDbContext>();
        var mockTokenService = new Mock<ITokenService>();
        var mockPasswordHasher = new Mock<IPasswordHasher>();
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

        mockPasswordHasher
            .Setup(x => x.Verify("Password123!", It.IsAny<string>()))
            .Returns(true);

        mockTokenService
            .Setup(x => x.GenerateAccessTokenWithClaims(
                It.IsAny<object>(), It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<int?>()))
            .Returns("access_token");

        mockTokenService
            .Setup(x => x.GenerateRefreshToken())
            .Returns(("refresh_token", DateTime.UtcNow.AddDays(7)));

        mockTokenService
            .Setup(x => x.HashToken(It.IsAny<string>()))
            .Returns("hashed_refresh_token");

        var handler = new LoginHandler(
            mockDb.Object,
            mockTokenService.Object,
            mockPasswordHasher.Object,
            mockHttpContextAccessor.Object
        );

        // Act
        var request = new LoginRequest("admin@isc.local", "Password123!");
        var result = await handler.Handle(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("access_token", result.AccessToken);
        Assert.Equal("refresh_token", result.RefreshToken);
    }

    [Fact]
    public async Task Handle_InvalidEmail_ThrowsUnauthorizedException()
    {
        // Arrange
        var mockDb = new Mock<ApplicationDbContext>();
        var handler = new LoginHandler(
            mockDb.Object,
            new Mock<ITokenService>().Object,
            new Mock<IPasswordHasher>().Object,
            new Mock<IHttpContextAccessor>().Object
        );

        var request = new LoginRequest("nonexistent@isc.local", "Password123!");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => handler.Handle(request, CancellationToken.None)
        );
    }
}
```

### Casos de Prueba por Handler

#### LoginHandler
- [ ] Test_Login_Success: Email + Password correctos → AccessToken + RefreshToken
- [ ] Test_Login_EmailNotFound: Email no existe → 401 Unauthorized
- [ ] Test_Login_UserInactive: Usuario inactivo → 401
- [ ] Test_Login_WrongPassword: Password incorrecto → 401
- [ ] Test_Login_SessionCreated: Sesión registrada en BD
- [ ] Test_Login_TokenContainsClaims: AccessToken incluye claims correctos

#### RefreshHandler
- [ ] Test_Refresh_Success: RefreshToken válido → Nuevos tokens
- [ ] Test_Refresh_SessionUpdated: BD actualizada
- [ ] Test_Refresh_TokenRotation: Token anterior invalidado
- [ ] Test_Refresh_SessionExpired: RefreshToken expirado → 401
- [ ] Test_Refresh_InvalidHash: Hash no coincide → 401

#### LogoutHandler
- [ ] Test_Logout_Success: Logout exitoso
- [ ] Test_Logout_TokenBlacklisted: JTI en blacklist
- [ ] Test_Logout_SessionClosed: Sesión marcada inactiva
- [ ] Test_Logout_UnauthenticatedUser: Sin autenticación → 401

#### ChangePasswordHandler
- [ ] Test_ChangePassword_Success: Password actualizado
- [ ] Test_ChangePassword_HistoryInserted: Historial registrado
- [ ] Test_ChangePassword_WrongOldPassword: Password anterior incorrecto → 400
- [ ] Test_ChangePassword_ReusesOldPassword: Reutiliza antigua → 400
- [ ] Test_ChangePassword_PolicyEnforced: Policy validada

#### GetCurrentUserHandler
- [ ] Test_GetCurrentUser_Success: Datos completos retornados
- [ ] Test_GetCurrentUser_WithEmployee: EmployeeId incluido
- [ ] Test_GetCurrentUser_MultipleRoles: Todos los roles incluidos

#### GetPermissionsHandler
- [ ] Test_GetPermissions_Success: Roles + Módulos + Menú retornados
- [ ] Test_GetPermissions_PermissionMerge: User > Role (OR lógico)
- [ ] Test_GetPermissions_MenuHierarchy: Árbol jerárquico correcto
- [ ] Test_GetPermissions_MenuVisibility: Menú filtrado por permisos

### Ejecutar Pruebas Unitarias

```bash
dotnet test --configuration Debug --verbosity normal
```

---

## Flujos de Prueba Completos

### Flujo 1: Login → Usar API → Logout

**Objetivo:** Validar el flujo completo de autenticación

**Pasos:**

1. **Iniciar servidor**
```bash
# Terminal 1
cd c:\Users\javier.luna\Desktop\TMR\tmr_back\tmr-backend
dotnet run --configuration Development
# ✅ Now listening on: http://localhost:5071
```

2. **LOGIN**
```bash
# Terminal 2
curl -X POST "http://localhost:5071/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@isc.local","password":"Password123!"}'

# Guardar responses en variables:
# AT1 = accessToken
# RT1 = refreshToken
```

3. **USAR API PROTEGIDA (GET /me)**
```bash
curl -X GET "http://localhost:5071/api/auth/me" \
  -H "Authorization: Bearer $AT1"

# Esperado: 200 OK con datos del usuario
```

4. **OBTENER PERMISOS**
```bash
curl -X GET "http://localhost:5071/api/auth/permissions" \
  -H "Authorization: Bearer $AT1"

# Esperado: 200 OK con roles, módulos y menú
```

5. **LOGOUT**
```bash
curl -X POST "http://localhost:5071/api/auth/logout" \
  -H "Authorization: Bearer $AT1" \
  -H "Content-Type: application/json" \
  -d '{}'

# Esperado: 200 OK
```

6. **INTENTAR USAR TOKEN ANTERIOR (DEBE FALLAR)**
```bash
curl -X GET "http://localhost:5071/api/auth/me" \
  -H "Authorization: Bearer $AT1"

# Esperado: 401 Unauthorized (token revocado)
```

**Criterio de Aceptación:** Todos los pasos completados exitosamente

---

### Flujo 2: Token Rotation (Refresh)

**Objetivo:** Validar que token anterior se invalida después de refresh

**Pasos:**

1. **LOGIN**
```bash
# Obtener AT1 y RT1
curl -X POST "http://localhost:5071/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@isc.local","password":"Password123!"}'
```

2. **USAR AT1 (DEBE FUNCIONAR)**
```bash
curl -X GET "http://localhost:5071/api/auth/me" \
  -H "Authorization: Bearer $AT1"

# Esperado: 200 OK
```

3. **REFRESH TOKENS**
```bash
# Obtener AT2 y RT2
curl -X POST "http://localhost:5071/api/auth/refresh" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"$RT1"}'
```

4. **USAR AT2 (DEBE FUNCIONAR)**
```bash
curl -X GET "http://localhost:5071/api/auth/me" \
  -H "Authorization: Bearer $AT2"

# Esperado: 200 OK
```

5. **INTENTAR USAR AT1 (DEBE FALLAR - ¡IMPORTANTE!)**
```bash
curl -X GET "http://localhost:5071/api/auth/me" \
  -H "Authorization: Bearer $AT1"

# Esperado: 401 Unauthorized (token en blacklist)
# Esto valida Token Rotation Security ✅
```

**Validación en BD:**
```sql
-- Verificar que AT1.JTI está en blacklist
SELECT token, activo FROM tbl_autenticacion_token_blacklist 
WHERE activo = true ORDER BY fechacreacion DESC LIMIT 1;
```

**Criterio de Aceptación:** AT1 es rechazado (401) después del refresh

---

### Flujo 3: Change Password con Policy

**Objetivo:** Validar políticas de seguridad de contraseña

**Pasos:**

1. **OBTENER TOKEN**
```bash
curl -X POST "http://localhost:5071/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@isc.local","password":"Password123!"}'
# Guardar AT
```

2. **CAMBIAR A PASSWORD SIMPLE (DEBE FALLAR - POLICY)**
```bash
curl -X POST "http://localhost:5071/api/auth/change-password" \
  -H "Authorization: Bearer $AT" \
  -H "Content-Type: application/json" \
  -d '{"oldPassword":"Password123!","newPassword":"simple"}'

# Esperado: 400 Bad Request (violación de policy)
# Error: "Mínimo 8 caracteres, mayúscula, número, símbolo"
```

3. **CAMBIAR A PASSWORD VÁLIDA**
```bash
curl -X POST "http://localhost:5071/api/auth/change-password" \
  -H "Authorization: Bearer $AT" \
  -H "Content-Type: application/json" \
  -d '{"oldPassword":"Password123!","newPassword":"NewPass456!"}'

# Esperado: 200 OK
```

4. **INTENTAR REUTILIZAR ANTERIOR (DEBE FALLAR)**
```bash
curl -X POST "http://localhost:5071/api/auth/change-password" \
  -H "Authorization: Bearer $AT" \
  -H "Content-Type: application/json" \
  -d '{"oldPassword":"NewPass456!","newPassword":"Password123!"}'

# Esperado: 400 Bad Request (reutilización detectada)
```

5. **CAMBIAR A NUEVA PASSWORD (DEBE FUNCIONAR)**
```bash
curl -X POST "http://localhost:5071/api/auth/change-password" \
  -H "Authorization: Bearer $AT" \
  -H "Content-Type: application/json" \
  -d '{"oldPassword":"NewPass456!","newPassword":"AnotherPwd789!"}'

# Esperado: 200 OK
```

**Validación en BD:**
```sql
-- Ver historial de cambios
SELECT usuario_id, hashpassword, fechacambio 
FROM autenticacion.tbl_autenticacion_password_historial 
WHERE usuario_id = 1 
ORDER BY fechacambio DESC LIMIT 5;
```

**Criterio de Aceptación:** 
- Passwords simples rechazadas ✅
- Reutilización detectada ✅
- Nuevas passwords aceptadas ✅

---

## Comandos cURL Listos

### PowerShell Setup
```powershell
# Guardar valores de tokens
$AT1 = ""  # accessToken de login
$RT1 = ""  # refreshToken de login
$AT2 = ""  # accessToken de refresh
$RT2 = ""  # refreshToken de refresh
```

### 1️⃣ TEST: LOGIN
```bash
curl -X POST "http://localhost:5071/api/auth/login" \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@isc.local","password":"Password123!"}'
```

**Respuesta esperada (200 OK):**
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "...",
  "expiresIn": 900,
  "user": { "id": 1, "nombre": "...", "roles": ["ADMIN"] }
}
```

### 2️⃣ TEST: GET CURRENT USER
```bash
curl -X GET "http://localhost:5071/api/auth/me" \
  -H "Authorization: Bearer eyJ..."
```

**Respuesta esperada (200 OK):**
```json
{
  "id": 1,
  "nombre": "Administrador Del Sistema",
  "email": "admin@isc.local",
  "roles": ["ADMIN"],
  "employeeId": 1
}
```

### 3️⃣ TEST: GET PERMISSIONS
```bash
curl -X GET "http://localhost:5071/api/auth/permissions" \
  -H "Authorization: Bearer eyJ..."
```

**Respuesta esperada (200 OK):**
```json
{
  "roles": ["ADMIN"],
  "modules": [
    { "id": 1, "nombre": "TimeReport", "acciones": ["READ", "CREATE", "UPDATE", "DELETE"] }
  ],
  "menuItems": [ { "id": 1, "nombre": "Dashboard", ... } ]
}
```

### 4️⃣ TEST: REFRESH TOKENS
```bash
curl -X POST "http://localhost:5071/api/auth/refresh" \
  -H "Content-Type: application/json" \
  -d '{"refreshToken":"..."}'
```

**Respuesta esperada (200 OK):**
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "...",
  "expiresIn": 900
}
```

### 5️⃣ TEST: CHANGE PASSWORD
```bash
curl -X POST "http://localhost:5071/api/auth/change-password" \
  -H "Authorization: Bearer eyJ..." \
  -H "Content-Type: application/json" \
  -d '{"oldPassword":"Password123!","newPassword":"NewPass456!"}'
```

**Respuesta esperada (200 OK):**
```json
{ "message": "Contraseña actualizada exitosamente." }
```

### 6️⃣ TEST: LOGOUT
```bash
curl -X POST "http://localhost:5071/api/auth/logout" \
  -H "Authorization: Bearer eyJ..." \
  -H "Content-Type: application/json" \
  -d '{}'
```

**Respuesta esperada (200 OK):**
```json
{ "message": "Sesión cerrada exitosamente." }
```

### 7️⃣ TEST: TOKEN REVOCADO (DEBE FALLAR)
```bash
curl -X GET "http://localhost:5071/api/auth/me" \
  -H "Authorization: Bearer eyJ..."
```

**Respuesta esperada (401 Unauthorized):**
```json
{ "error": "Token revocado (BD)" }
```

---

## Pruebas de Seguridad

### Test 1: Blacklist Post-Logout
```
1. Login → Obtener AT
2. Logout con AT
3. Intenta usar AT → Debe fallar (401)

Validación: AT está en tbl_autenticacion_token_blacklist
```

### Test 2: Token Rotation (Refresh)
```
1. Login → AT1 + RT1
2. Refresh → AT2 + RT2
3. Intenta usar AT1 → Debe fallar (401)

Validación: AT1.JTI está en blacklist, AT2 funciona
```

### Test 3: Password Policy
```
1. Cambiar a "abc" → 400 (< 8 chars)
2. Cambiar a "NoSymbol1" → 400 (sin símbolo)
3. Cambiar a "newsymbol1!" → 200 OK
4. Cambiar a "newsymbol1!" → 400 (reutilización)

Validación: Policy funciona correctamente
```

### Test 4: BCrypt Salting
```
1. Hashear "Password123!" → Hash1
2. Hashear "Password123!" → Hash2
3. Hash1 ≠ Hash2 (diferente salt)

Validación: BCrypt genera diferentes hashes
```

### Test 5: JWT Signature
```
1. Obtener token: "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9...."
2. Alternar última letra de la firma
3. Usar token alterado → Debe fallar (401)

Validación: Firma es validada
```

### Test 6: Token Expiration
```
1. Obtener AT (expires in 900 segundos)
2. Esperar 15 minutos
3. Intentar usar AT → Debe fallar (401)

Validación: Expiración es validada
```

---

## Criterios de Aceptación

### Para Fase 1 (Infraestructura)
- [x] JWT genera correctamente con claims
- [x] BCrypt hashea correctamente
- [x] Middleware valida firma y expiración
- [x] Blacklist cache funciona

### Para Fase 2 (Login)
- [x] Login exitoso genera AT + RT
- [x] Email/password inválidos retornan 401
- [x] Sesión se registra en BD
- [x] Usuario inactivo es rechazado

### Para Fase 3 (Refresh + Logout)
- [x] Refresh genera nuevos tokens
- [x] Token anterior se invalida (token rotation)
- [x] Logout agrega JTI a blacklist
- [x] Logout cierra SOLO la sesión actual
- [x] Token revocado retorna 401

### Para Fase 4 (Change Password)
- [x] Policy es validada (8 chars, mayús, número, símbolo)
- [x] Reutilización es detectada (últimas 5)
- [x] Historial se registra en BD
- [x] Password anterior incorrecto retorna 400

### Para Fase 5 (GetCurrentUser + GetPermissions)
- [x] GetCurrentUser retorna datos completos
- [x] Roles se obtienen correctamente
- [x] GetPermissions calcula permisos (rol + usuario)
- [x] Menú es jerárquico
- [x] Menú filtrado por permisos

---

**Última actualización:** 28-May-2026  
**Versión:** 1.0  
**Status:** ✅ Listo para ejecución
