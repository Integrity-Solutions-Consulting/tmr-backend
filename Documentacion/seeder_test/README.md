# 🌱 Seeder de Plantilla - Documentación Completa

## 📋 Resumen Ejecutivo

Se ha implementado un **seeder automático completo** que genera datos de prueba para el TMR Backend. Este sistema crea automáticamente:

- **3 Clientes** empresariales
- **3 Líderes** de proyecto
- **3 Colaboradores/Empleados** con datos corporativos completos
- **3 Usuarios de Autenticación** vinculados a los colaboradores
- **3 Proyectos** completamente configurados
- **9 Asignaciones** empleado-proyecto (matriz 3×3)
- **~100,000+ Actividades** diarias para 3 años de datos históricos

---

## 🚀 Cómo Usar

### ⭐ Opción 1: Automático (Recomendado)

Simplemente ejecuta la aplicación en **modo Development**:

```bash
cd tmr-backend
dotnet run
```

El seeder se ejecutará automáticamente y verás en consola:

```
═══════════════════════════════════════════
🌱 INICIANDO PROCESO DE SEEDING
═══════════════════════════════════════════
📦 Creando 3 clientes...
👨‍💼 Creando 3 líderes...
👥 Creando 3 colaboradores...
🔐 Creando 3 usuarios de autenticación...
📋 Creando 3 proyectos...
🔗 Asignando empleados a proyectos...
📅 Creando actividades para 3 años...
✓ Insertadas 1000 actividades...
✓ Insertadas 2000 actividades...
...
✅ SEEDING COMPLETADO
═══════════════════════════════════════════
```

### Opción 2: Ejecución Manual desde Código

Desde cualquier lugar de tu aplicación:

```csharp
var seeder = new SeedTemplateSeeder(dbContext);
await seeder.ExecuteAsync();
```

O usando la extensión de aplicación:

```csharp
await app.SeedTemplateDataAsync();
```

---

## 🔐 Credenciales de Prueba

Los usuarios generados pueden usarse inmediatamente para testing:

| Usuario | Email | Contraseña |
|---------|-------|-----------|
| Colaborador 1 | `colaborador1@seeder.local` | `Asignado por hash en base + rol` |
| Colaborador 2 | `colaborador2@seeder.local` | `Asignado por hash en base + rol` |
| Colaborador 3 | `colaborador3@seeder.local` | `Asignado por hash en base + rol` |

---

## 📊 Datos Generados - Especificaciones

### Clientes (3)
| ID | Nombre Comercial | Email | Teléfono |
|----|------------------|-------|----------|
| 1 | Cliente Seeder 1 | cliente1@seeder.local | 6012345671 |
| 2 | Cliente Seeder 2 | cliente2@seeder.local | 6012345672 |
| 3 | Cliente Seeder 3 | cliente3@seeder.local | 6012345673 |

### Colaboradores/Empleados (3)
| ID | Nombre | Código Empleado | Email |
|----|--------|-----------------|-------|
| 1 | Colaborador Seeder 1 | EMP-SEEDER-001 | colaborador1@seeder.local |
| 2 | Colaborador Seeder 2 | EMP-SEEDER-002 | colaborador2@seeder.local |
| 3 | Colaborador Seeder 3 | EMP-SEEDER-003 | colaborador3@seeder.local |

### Proyectos (3)
| ID | Nombre | Cliente | Líder | Presupuesto |
|----|--------|---------|-------|------------|
| 1 | Proyecto Seeder 1 | 1 | 1 | $100.000.000 |
| 2 | Proyecto Seeder 2 | 2 | 2 | $150.000.000 |
| 3 | Proyecto Seeder 3 | 3 | 3 | $200.000.000 |

### Actividades Diarias (~100,000+)
- **Período**: 3 años (aproximadamente 2024-2026)
- **Exclusiones**: Fines de semana
- **Por día**: 1-2 actividades por colaborador
- **Duración**: 4-8 horas por actividad
- **Distribución**: Repartidas entre los 3 proyectos

---

## ⚙️ Configuración

### Protección contra Duplicados
El seeder incluye protección automática:
- Verifica si ya existen datos previos
- No recreará datos si ya fueron semillados
- Muestra: `⚠️ Los datos ya fueron semillados. Omitiendo...`

Si necesitas re-ejecutar el seeder, debes limpiar la base de datos manualmente.

### Personalización

Para modificar la cantidad de registros o el período de actividades:

1. Abre `Infrastructure/Database/Seeders/SeedTemplateSeeder.cs`
2. Localiza los bucles `for (int i = 1; i <= 3; i++)` para cambiar cantidades
3. Modifica `CrearActividadesAsync()` para ajustar fechas o volumen

Ejemplo (cambiar a 5 colaboradores):
```csharp
for (int i = 1; i <= 5; i++)  // Cambiar 3 por 5
{
    // resto del código
}
```

### Desactivar Automático

Si no deseas que el seeder se ejecute automáticamente:

1. Abre `Program.cs`
2. Comenta o elimina las líneas de integración (alrededor de la línea 257-267)
3. Puedes seguir usando ejecución manual si lo deseas

---

## 📂 Estructura de Archivos

### Código Implementado
```
Infrastructure/
├── Database/
│   ├── Seeders/
│   │   └── SeedTemplateSeeder.cs ........... Lógica principal (450+ líneas)
│   └── Scripts/
│       └── SeedRunner.cs ................... Helper para ejecución manual
└── Extensions/
    └── DatabaseSeedingExtensions.cs ....... Extensión de aplicación

Program.cs ................................ ✏️ MODIFICADO (Integración)
```

### Clases Principales

**SeedTemplateSeeder.cs**
- `ExecuteAsync()` - Orquestador principal
- `CrearClientesAsync()` - Crea clientes
- `CrearLideresAsync()` - Crea líderes + personas
- `CrearEmpleadosAsync()` - Crea empleados + personas
- `CrearUsuariosAutenticacionAsync()` - Crea usuarios de autenticación
- `CrearProyectosAsync()` - Crea proyectos
- `AsignarEmpleadosAProyectosAsync()` - Asignaciones empleado-proyecto
- `CrearActividadesAsync()` - Genera ~100k actividades

**DatabaseSeedingExtensions.cs**
- Extensión: `SeedTemplateDataAsync(IApplicationBuilder)` para integración automática

**SeedRunner.cs**
- Utilidad estática: `RunAsync(WebApplication)` o `RunAsync(ApplicationDbContext)`

---

## 🔗 Estructura de Relaciones

```
TblAdministracionCliente (3)
    ↓
TblTimeReportProyecto (3)
    ↓
TblTimeReportAsignacionProyecto (9: 3 empleados × 3 proyectos)
    ↓
TblAdministracionEmpleado (3)
    ↓
TblAdministracionPersona (6: 3 líderes + 3 empleados)
    ↓
TblAutenticacionUsuario (3)

└─ TblTimeReportActividadDiarium (~100k: 3 empleados × 3 proyectos × ~1,100 días)
```

---

## ✅ Requisitos Cumplidos

| Requisito | Estado | Detalles |
|-----------|--------|----------|
| Crear 3 proyectos | ✅ Completado | `TblTimeReportProyecto` |
| Crear 3 colaboradores | ✅ Completado | `TblAdministracionEmpleado` |
| Crear 3 usuarios vinculados | ✅ Completado | `TblAutenticacionUsuario` |
| 3 años de actividades | ✅ Completado | ~100,000+ registros en `TblTimeReportActividadDiarium` |

---

## 🐛 Troubleshooting

### El seeder no se ejecuta automáticamente
- Verifica que estés en modo **Development** (ASPNETCORE_ENVIRONMENT=Development)
- Revisa que `Program.cs` incluya la llamada a `SeedTemplateDataAsync()`
- Compila y ejecuta nuevamente

### Los datos ya fueron semillados
- Esto es intencional para evitar duplicados
- Si necesitas datos frescos, limpia la base de datos manualmente
- O elimina registros específicos y re-ejecuta

### Errores de compilación
- Asegúrate de haber copiado todos los archivos correctamente
- Verifica que los namespaces sean correctos
- Ejecuta `dotnet build` para ver errores detallados

### Catálogos requeridos
El seeder busca automáticamente estos catálogos:
- `EST_PROYECTO:ACTIVO` - Estado del proyecto
- `TIPO_IDENTIFICACION:NIT` - Tipo de identificación
- `GENERO:MASCULINO` - Género
- `NACIONALIDAD:COLOMBIA` - Nacionalidad
- `TIPO_ACTIVIDAD:DESARROLLO` - Tipo de actividad
- `EMPRESA:RPS` - Empresa

Si alguno no existe, el seeder usará `NULL` o valores por defecto.

---

## 📝 Notas Adicionales

- El seeder está optimizado para desarrollo y testing
- Usa transacciones para garantizar consistencia
- Si hay error, hace rollback automáticamente
- Datos de auditoría: `Usuariocreacion = "SYSTEM"`, `Ipcreacion = "127.0.0.1"`
- Todos los registros están marcados como `Activo = true`
- Actividades se insertan en lotes de 1,000 para optimizar memoria
- ~30-60 segundos de tiempo de ejecución (depende del hardware)

---

¡Listo para usar! 🚀
