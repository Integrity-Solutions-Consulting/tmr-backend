# 🚀 Guía Rápida - Seeder de Plantilla

#

```bash
cd tmr-backend
dotnet run
```

El seeder se ejecutará automáticamente en modo Development.

---

## 📊 Qué se crea

| Entidad | Cantidad | Detalles |
|---------|----------|----------|
| **Clientes** | 3 | Cliente Seeder 1-3 |
| **Colaboradores** | 3 | Empleados con código EMP-SEEDER-001 a 003 |
| **Usuarios** | 3 | Vinculados a colaboradores, autenticación SHA256 |
| **Proyectos** | 3 | Proyecto Seeder 1-3 |
| **Actividades** | ~100,000+ | 3 años de datos históricos (L-V) |

---

## 🔐 Credenciales para Probar

| Email | Contraseña |
|-------|-----------|
| `colaborador1@seeder.local` | `asignar hash en base + rol` |
| `colaborador2@seeder.local` | `asignar hash en base + rol` |
| `colaborador3@seeder.local` | `asignar hash en base + rol` |

---

## 🎯 Opciones de Uso

### Opción 1: Automático (Recomendado) ⭐
```bash
dotnet run  # Se ejecuta automáticamente en Development
```

### Opción 2: Manual desde Código
```csharp
var seeder = new SeedTemplateSeeder(dbContext);
await seeder.ExecuteAsync();
```

### Opción 3: Usar Extensión
```csharp
await app.SeedTemplateDataAsync();
```

---

## ⚙️ Configuración Rápida

### Personalizar Cantidad
1. Abre `Infrastructure/Database/Seeders/SeedTemplateSeeder.cs`
2. Modifica: `for (int i = 1; i <= 3; i++)` → cambiar `3` por el número que quieras

### Cambiar Período de Actividades
1. Busca `CrearActividadesAsync()`
2. Modifica las fechas: `DateTime.UtcNow.Year - 3`

### Desactivar Automático
Comenta en `Program.cs`:
```csharp
// await app.SeedTemplateDataAsync();
```

---

## 📊 Ejemplos de Datos

**Clientes**: Cliente Seeder 1, 2, 3 → cliente{1-3}@seeder.local

**Colaboradores**: 
- Colaborador Seeder 1 → EMP-SEEDER-001 → colaborador1@seeder.local
- Colaborador Seeder 2 → EMP-SEEDER-002 → colaborador2@seeder.local
- Colaborador Seeder 3 → EMP-SEEDER-003 → colaborador3@seeder.local

**Proyectos**: Proyecto Seeder 1, 2, 3 → $100M-$200M presupuesto

**Actividades**: ~1,100 días × 3 empleados × 3 proyectos ≈ 100,000+

---

## 🧪 Probar Autenticación

```bash
curl -X POST https://localhost:7001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "colaborador1@seeder.local",
    "password": "contraseña asignada en hash"
  }'
```

---

## ✅ Verificar en la BD

```csharp
// Contar colaboradores con "Seeder"
var count = await db.TblAdministracionEmpleados
    .Where(e => e.IdpersonaNavigation.Nombres.Contains("Seeder"))
    .CountAsync();  // Debe ser 3

// Contar actividades
var activities = await db.TblTimeReportActividadDiariums
    .CountAsync();  // Debe ser ~100,000+
```

---

## 📂 Archivos Clave

```
Infrastructure/Database/Seeders/
├── SeedTemplateSeeder.cs .... Lógica principal


Program.cs .................. Integración automática
```

---

## 🐛 Problemas Comunes

| Problema | Solución |
|----------|----------|
| No se ejecuta | Verifica `ASPNETCORE_ENVIRONMENT=Development` |
| "Ya existe" | Es protección contra duplicados - limpia la BD si necesitas resetear |
| Error de catálogos | Ejecuta migraciones primero |
| Tarda mucho | ~30-60s es normal (100k registros) |

---

## 📖 Más Info

Para documentación completa, consulta [README.md](README.md)
| Datos duplicados | El seeder los detecta y omite automáticamente |
| Tarda mucho tiempo | Normal para ~100k registros; usa SSD |
| BD vacía después | Verifica logs en consola y conexión a BD |

---

## 📋 Checklist de Integración

- [x] Seeder creado (`SeedTemplateSeeder.cs`)
- [x] Extensión de aplicación (`DatabaseSeedingExtensions.cs`)
- [x] Integrado en `Program.cs`
- [x] Helper para ejecución manual (`SeedRunner.cs`)
- [x] Documentación completa (`README.md`)
- [x] Protección contra duplicados
- [x] Manejo de transacciones
- [x] Sin errores de compilación

---

## 📞 Soporte

Para más detalles, consulta:
- [README.md](./README.md) - Documentación técnica completa
- [SeedTemplateSeeder.cs](./SeedTemplateSeeder.cs) - Código fuente
- [Program.cs](../../Program.cs) - Integración

---

El seeder se ejecutará automáticamente cada vez que inicies la aplicación en desarrollo.
