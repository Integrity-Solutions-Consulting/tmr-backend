using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Infrastructure.Database.Entities;
using System.Security.Cryptography;
using System.Text;

namespace tmr_backend.Infrastructure.Database.Seeders;

/// <summary>
/// Seeder para crear datos de plantilla:
/// - 3 Clientes
/// - 3 Líderes (Personas + TblAdministracionLider)
/// - 3 Colaboradores/Empleados (Personas + TblAdministracionEmpleado)
/// - 3 Usuarios de Autenticación
/// - 3 Proyectos
/// - Asignaciones de empleados a proyectos
/// - Actividades diarias para 3 años en cada proyecto
/// </summary>
public class SeedTemplateSeeder
{
    private readonly ApplicationDbContext _db;
    private const string UsuarioSistema = "SYSTEM";
    private const string IpSistema = "127.0.0.1";

    public SeedTemplateSeeder(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task ExecuteAsync()
    {
        // Verificar si ya existen datos semillados para evitar duplicados
        if (await _db.TblAdministracionClientes.AnyAsync(c => c.Nombrecomercial != null && c.Nombrecomercial.Contains("Seeder")))
        {
            Console.WriteLine("⚠️ Los datos ya fueron semillados. Omitiendo...");
            return;
        }

        try
        {
            Console.WriteLine("🌱 Iniciando seeder de plantilla...");

            // Obtener IDs de catálogos necesarios
            var idEstadoProyectoActivo = await GetIdCatalogoAsync("EST_PROYECTO", "ACTIVO");
            var idTipoIdentificacionNit = await GetIdCatalogoAsync("TIPO_IDENTIFICACION", "NIT");
            var idGeneroMasculino = await GetIdCatalogoAsync("GENERO", "MASCULINO");
            var idNacionalidadColombia = await GetIdCatalogoAsync("NACIONALIDAD", "COLOMBIA");
            var idTipoActividadDevelopment = await GetIdCatalogoAsync("TIPO_ACTIVIDAD", "DESARROLLO");
            var idEmpresaRPS = await GetIdCatalogoAsync("EMPRESA", "RPS");

            // Usar transacción para garantizar consistencia
            using var transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                // 1. Crear 3 Clientes
                Console.WriteLine("📦 Creando 3 clientes...");
                var clientes = await CrearClientesAsync(idTipoIdentificacionNit);

                // 2. Crear 3 Líderes (Persona + TblAdministracionLider)
                Console.WriteLine("👨‍💼 Creando 3 líderes...");
                var lideres = await CrearLideresAsync(idGeneroMasculino, idNacionalidadColombia);

                // 3. Crear 3 Empleados/Colaboradores (Persona + TblAdministracionEmpleado)
                Console.WriteLine("👥 Creando 3 colaboradores...");
                var empleados = await CrearEmpleadosAsync(idGeneroMasculino, idNacionalidadColombia, idEmpresaRPS);

                // 4. Crear 3 Usuarios de Autenticación
                Console.WriteLine("🔐 Creando 3 usuarios de autenticación...");
                await CrearUsuariosAutenticacionAsync(empleados);

                // 5. Crear 3 Proyectos
                Console.WriteLine("📋 Creando 3 proyectos...");
                var proyectos = await CrearProyectosAsync(clientes, lideres, idEstadoProyectoActivo);

                // 6. Asignar empleados a proyectos
                Console.WriteLine("🔗 Asignando empleados a proyectos...");
                await AsignarEmpleadosAProyectosAsync(empleados, proyectos);

                // 7. Crear actividades para 3 años
                Console.WriteLine("📅 Creando actividades para 3 años...");
                await CrearActividadesAsync(empleados, proyectos, idTipoActividadDevelopment);

                await transaction.CommitAsync();
                Console.WriteLine("✅ Seeder completado exitosamente");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ Error durante seeder: {ex.Message}");
                throw;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error fatal en seeder: {ex}");
            throw;
        }
    }

    private async Task<int?> GetIdCatalogoAsync(string catalogo, string valor)
    {
        var detalle = await _db.TblAdministracionCatalogoDetalles
            .Include(cd => cd.IdcatalogoNavigation)
            .FirstOrDefaultAsync(cd =>
                cd.IdcatalogoNavigation.Tipocatalogo == catalogo &&
                cd.Valor == valor);

        return detalle?.Id;
    }

    private async Task<List<TblAdministracionCliente>> CrearClientesAsync(int? idTipoIdentificacion)
    {
        var clientes = new List<TblAdministracionCliente>();

        for (int i = 1; i <= 3; i++)
        {
            var cliente = new TblAdministracionCliente
            {
                Numeroidentificacion = $"900123456{i}",
                Idtipoidentificacion = idTipoIdentificacion,
                Nombrecomercial = $"Cliente Seeder {i}",
                Razonsocial = $"Razón Social Cliente Seeder {i}",
                Nombres = $"Cliente {i}",
                Apellidos = $"Seeder",
                Email = $"cliente{i}@seeder.local",
                Telefono = $"601234567{i}",
                Direccion = $"Carrera {i}, Calle 1, Bogotá",
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            _db.TblAdministracionClientes.Add(cliente);
            clientes.Add(cliente);
        }

        await _db.SaveChangesAsync();
        return clientes;
    }

    private async Task<List<TblAdministracionLider>> CrearLideresAsync(int? idGenero, int? idNacionalidad)
    {
        var lideres = new List<TblAdministracionLider>();

        for (int i = 1; i <= 3; i++)
        {
            // Crear Persona
            var persona = new TblAdministracionPersona
            {
                Numeroidentificacion = $"100123456{i}",
                Idtipoidentificacion = 1, // CC - Cédula Colombiana
                Idgenero = idGenero,
                Idnacionalidad = idNacionalidad,
                Tipopersona = "NATURAL",
                Nombres = $"Líder Seeder {i}",
                Apellidos = $"Apellido {i}",
                Fechanacimiento = new DateOnly(1985, 1, i),
                Email = $"lider{i}@seeder.local",
                Telefono = $"3101234567{i}",
                Direccion = $"Avenida {i}, Bogotá",
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            _db.TblAdministracionPersonas.Add(persona);
            await _db.SaveChangesAsync();

            // Crear Lider
            var lider = new TblAdministracionLider
            {
                Idpersona = persona.Id,
                Idtipo = 1, // Tipo Lider genérico
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            _db.TblAdministracionLiders.Add(lider);
            lideres.Add(lider);
        }

        await _db.SaveChangesAsync();
        return lideres;
    }

    private async Task<List<TblAdministracionEmpleado>> CrearEmpleadosAsync(int? idGenero, int? idNacionalidad, int? idEmpresa)
    {
        var empleados = new List<TblAdministracionEmpleado>();

        for (int i = 1; i <= 3; i++)
        {
            // Crear Persona
            var persona = new TblAdministracionPersona
            {
                Numeroidentificacion = $"110456789{i}",
                Idtipoidentificacion = 1, // CC
                Idgenero = idGenero,
                Idnacionalidad = idNacionalidad,
                Tipopersona = "NATURAL",
                Nombres = $"Colaborador Seeder {i}",
                Apellidos = $"Cognomen {i}",
                Fechanacimiento = new DateOnly(1990, 6, i),
                Email = $"colaborador{i}@seeder.local",
                Telefono = $"3201234567{i}",
                Direccion = $"Diagonal {i}, Medellín",
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            _db.TblAdministracionPersonas.Add(persona);
            await _db.SaveChangesAsync();

            // Crear Empleado
            var empleado = new TblAdministracionEmpleado
            {
                Idpersona = persona.Id,
                Idcargo = 1, // Cargo genérico (debe existir en BD)
                Idmodotrabajo = 1, // Modo trabajo genérico
                Idcategoriaempleado = 1, // Categoría genérico
                Idempresacatalogo = idEmpresa,
                Codigoempleado = $"EMP-SEEDER-{i:000}",
                Fechaingreso = new DateOnly(DateTime.UtcNow.AddYears(-3).Year, 1, 1),
                Emailcorporativo = $"colaborador{i}@empresa.local",
                Salario = 3000000 + (i * 100000),
                Aniosexperiencia = 3 + i,
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            _db.TblAdministracionEmpleados.Add(empleado);
            empleados.Add(empleado);
        }

        await _db.SaveChangesAsync();
        return empleados;
    }

    private async Task CrearUsuariosAutenticacionAsync(List<TblAdministracionEmpleado> empleados)
    {
        foreach (var empleado in empleados)
        {
            var persona = await _db.TblAdministracionPersonas.FindAsync(empleado.Idpersona);

            if (persona == null) continue;

            // Verificar si el usuario ya existe
            var usuarioExistente = await _db.TblAutenticacionUsuarios
                .AnyAsync(u => u.IdpersonaNavigation.Numeroidentificacion == persona.Numeroidentificacion);

            if (usuarioExistente) continue;

            var password = $"Seeder123!@{empleado.Id}"; // Contraseña de prueba
            var hashPassword = HashPassword(password);

            var usuario = new TblAutenticacionUsuario
            {
                Idpersona = persona.Id,
                Email = persona.Email ?? $"user{empleado.Id}@seeder.local",
                Hashpassword = hashPassword,
                Emailverificado = true,
                Intentosfallidos = 0,
                Debecambiarpassword = true,
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            _db.TblAutenticacionUsuarios.Add(usuario);
        }

        await _db.SaveChangesAsync();
    }

    private async Task<List<TblTimeReportProyecto>> CrearProyectosAsync(
        List<TblAdministracionCliente> clientes,
        List<TblAdministracionLider> lideres,
        int? idEstadoProyecto)
    {
        var proyectos = new List<TblTimeReportProyecto>();

        for (int i = 0; i < 3; i++)
        {
            var proyecto = new TblTimeReportProyecto
            {
                Idcliente = clientes[i].Id,
                Idestadoproyecto = idEstadoProyecto ?? 1,
                Codigo = $"PROJ-SEED-{i + 1:000}",
                Nombre = $"Proyecto Seeder {i + 1}",
                Descripcion = $"Proyecto de prueba generado por seeder #{i + 1}",
                Fechainicioplaneada = new DateOnly(DateTime.UtcNow.Year - 3, 1, 1),
                Fechafinplaneada = new DateOnly(DateTime.UtcNow.Year, 12, 31),
                Fechainicioreal = new DateOnly(DateTime.UtcNow.Year - 3, 1, 15),
                Presupuesto = 100000000 + (i * 50000000),
                Horasasignadas = 2000 + (i * 500),
                Activo = true,
                Usuariocreacion = UsuarioSistema,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = IpSistema
            };

            _db.TblTimeReportProyectos.Add(proyecto);
            proyectos.Add(proyecto);
        }

        await _db.SaveChangesAsync();
        return proyectos;
    }

    private async Task AsignarEmpleadosAProyectosAsync(
        List<TblAdministracionEmpleado> empleados,
        List<TblTimeReportProyecto> proyectos)
    {
        // Asignar cada empleado a cada proyecto
        for (int i = 0; i < empleados.Count; i++)
        {
            for (int j = 0; j < proyectos.Count; j++)
            {
                var asignacion = new TblTimeReportAsignacionProyecto
                {
                    Idempleado = empleados[i].Id,
                    Idproyecto = proyectos[j].Id,
                    Idlider = null, // No asignamos líder en este paso
                    Fechaasignacion = new DateOnly(DateTime.UtcNow.Year - 3, 1, 1),
                    Rolasignado = $"Developer {i + 1}",
                    Costoporhora = 50000 + (i * 10000),
                    Horasasignadas = 160 * 12, // ~160 horas/mes
                    Activo = true,
                    Usuariocreacion = UsuarioSistema,
                    Fechacreacion = DateTime.UtcNow,
                    Ipcreacion = IpSistema
                };

                _db.TblTimeReportAsignacionProyectos.Add(asignacion);
            }
        }

        await _db.SaveChangesAsync();
    }

    private async Task CrearActividadesAsync(
        List<TblAdministracionEmpleado> empleados,
        List<TblTimeReportProyecto> proyectos,
        int? idTipoActividad)
    {
        var actividades = new List<TblTimeReportActividadDiarium>();
        var fechaInicio = new DateOnly(DateTime.UtcNow.Year - 3, 1, 1);
        var fechaFin = new DateOnly(DateTime.UtcNow.Year, 12, 31);

        var random = new Random(42); // Seed para reproducibilidad

        // Generar actividades para cada empleado, proyecto y día en los últimos 3 años
        for (int e = 0; e < empleados.Count; e++)
        {
            for (int p = 0; p < proyectos.Count; p++)
            {
                var fechaActual = fechaInicio;

                while (fechaActual <= fechaFin)
                {
                    // Skip weekends
                    if (fechaActual.DayOfWeek == DayOfWeek.Saturday ||
                        fechaActual.DayOfWeek == DayOfWeek.Sunday)
                    {
                        fechaActual = fechaActual.AddDays(1);
                        continue;
                    }

                    // Crear 1-2 actividades por día (80% de probabilidad de 1 actividad, 20% de 2)
                    var numActividades = random.NextDouble() < 0.8 ? 1 : 2;

                    for (int a = 0; a < numActividades; a++)
                    {
                        var actividad = new TblTimeReportActividadDiarium
                        {
                            Idempleado = empleados[e].Id,
                            Idproyecto = proyectos[p].Id,
                            Idtipoactividad = idTipoActividad ?? 1,
                            Codigorequerimiento = $"REQ-{random.Next(1000, 9999)}",
                            Cantidadhoras = (decimal)(4 + random.NextDouble() * 4), // 4-8 horas
                            Fechaactividad = fechaActual,
                            Descripcionactividad = GenerarDescripcionActividad(a + 1),
                            Notas = random.NextDouble() > 0.7 ? "Completada exitosamente" : null,
                            Esbillable = true,
                            Activo = true,
                            Usuariocreacion = UsuarioSistema,
                            Fechacreacion = DateTime.UtcNow,
                            Ipcreacion = IpSistema
                        };

                        actividades.Add(actividad);

                        // Insertar en lotes para evitar problemas de memoria
                        if (actividades.Count >= 1000)
                        {
                            await _db.TblTimeReportActividadDiaria.AddRangeAsync(actividades);
                            await _db.SaveChangesAsync();
                            actividades.Clear();
                            Console.WriteLine($"✓ Insertadas {1000} actividades...");
                        }
                    }

                    fechaActual = fechaActual.AddDays(1);
                }
            }
        }

        // Insertar las actividades restantes
        if (actividades.Count > 0)
        {
            await _db.TblTimeReportActividadDiaria.AddRangeAsync(actividades);
            await _db.SaveChangesAsync();
            Console.WriteLine($"✓ Insertadas {actividades.Count} actividades finales");
        }
    }

    private string GenerarDescripcionActividad(int index)
    {
        var descripciones = new[]
        {
            "Desarrollo de funcionalidades",
            "Corrección de bugs",
            "Revisión de código",
            "Pruebas unitarias",
            "Documentación técnica",
            "Refactorización de código",
            "Implementación de API REST",
            "Diseño de base de datos",
            "Optimización de rendimiento",
            "Integración continua"
        };

        return descripciones[index % descripciones.Length];
    }

    private string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }
}
