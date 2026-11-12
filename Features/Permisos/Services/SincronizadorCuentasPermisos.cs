using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Security;

namespace tmr_backend.Features.Permisos.Services;

/// <summary>
/// Sincroniza de forma idempotente las cuentas estándar de prueba del sistema
/// (admin@tmr.com, colaborador@tmr.com, lider@tmr.com, ger1@tmr.com, rec1@tmr.com)
/// garantizando que cuenten con Persona, Ficha de Empleado (Nómina), Contrato vigente,
/// Cargo (incluyendo Líder de Talento Humano para rec1@tmr.com), registro de Líder Interno
/// para lider@tmr.com y externo@tmr.com, asignaciones activas y permisos RBAC.
/// </summary>
public static class SincronizadorCuentasPermisos
{
    private static readonly SemaphoreSlim Mutex = new(1, 1);
    private static DateTime _ultimaVerificacionUtc = DateTime.MinValue;

    private sealed record DefinicionCuentaPrueba(
        string Email,
        string Cedula,
        string Nombres,
        string Apellidos,
        string CodigoEmpleado,
        string[] RolesPreferidos,
        string CargoPreferido,
        bool EsLiderInterno = false,
        bool EsLiderExterno = false);

    private static readonly DefinicionCuentaPrueba[] CuentasEstandar =
    [
        new("admin@tmr.com",       "1790000001", "Administrador", "General TMR",     "EMP-ADM-001", ["Administrador"],                  "Administrador General"),
        new("colaborador@tmr.com", "1790000002", "Carlos Andrés", "Colaborador TMR", "EMP-COL-002", ["Colaborador"],                    "Analista de Desarrollo"),
        new("lider@tmr.com",       "1790000003", "Luis Fernando", "Líder Proyecto",  "EMP-LID-003", ["Lider", "Líder", "Colaborador"],  "Líder de Proyecto", EsLiderInterno: true),
        new("ger1@tmr.com",        "1790000004", "Gabriel",       "Gerencia TMR",    "EMP-GER-004", ["Gerencia", "Gerente"],            "Gerente de Operaciones"),
        new("rec1@tmr.com",        "1790000005", "Ana María",     "Talento Humano",  "EMP-RRHH-005",["Recursos Humanos"],               "Líder de Talento Humano"),
        new("externo@tmr.com",     "1790000006", "Ernesto",       "Líder Externo",   "EMP-EXT-006", ["Colaborador"],                    "Líder Externo de Prueba", EsLiderExterno: true)
    ];

    public static async Task AsegurarCuentasSincronizadasAsync(
        ApplicationDbContext db,
        IMemoryCache? cache = null,
        CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null)
            return;

        if ((DateTime.UtcNow - _ultimaVerificacionUtc).TotalMinutes < 5)
            return;

        if (!await Mutex.WaitAsync(0, ct))
            return;

        try
        {
            if ((DateTime.UtcNow - _ultimaVerificacionUtc).TotalMinutes < 5)
                return;

            var emailsEstandar = CuentasEstandar.Select(c => c.Email).ToArray();
            var usuariosExistentes = await db.TblAutenticacionUsuarios
                .Where(u => emailsEstandar.Contains(u.Email.ToLower()))
                .ToListAsync(ct);

            // Esta cuenta es un fixture público de QA: su contraseña debe ser estable
            // y no depender del orden de lectura de hashes de otras cuentas.
            var hasher = new PasswordHasher();

            var hashBase = usuariosExistentes.FirstOrDefault(u => !string.IsNullOrWhiteSpace(u.Hashpassword))?.Hashpassword
                           ?? await db.TblAutenticacionUsuarios
                               .AsNoTracking()
                               .Where(u => u.Activo && u.Hashpassword != "")
                               .OrderBy(u => u.Id)
                               .Select(u => u.Hashpassword)
                               .FirstOrDefaultAsync(ct)
                           ?? "$2a$12$UFDB3k8YBM7xlG.ZMt6PGe7fetHSiIKf3HxnJpgka1oA5e6brt4Y2";

            var ahora = DateTime.UtcNow;
            const string usuarioSistema = "SYNC_PERMISOS";
            const string ipSistema = "127.0.0.1";

            // 1. Asegurar catálogo de contrato Nómina (Relación de dependencia) y Tipo de Líder Interno (INT)
            var idTipoContratoNomina = await ObtenerIdContratoNominaAsync(db, ct);
            var idTipoLiderInterno = await ObtenerIdTipoLiderInternoAsync(db, ct);
            var idTipoLiderExterno = await ObtenerIdTipoLiderExternoAsync(db, ct);

            // 2. Asegurar cargos base (en especial "Líder de Talento Humano")
            var idCargoTh = await AsegurarCargoAsync(db, "Líder de Talento Humano", ahora, usuarioSistema, ipSistema, ct);
            var idCargoDefault = await db.TblAdministracionCargos
                .AsNoTracking()
                .Where(c => c.Activo && c.Id != idCargoTh)
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct) ?? idCargoTh;

            // 3. Asegurar que los roles requeridos existan y tengan los permisos del módulo Permisos
            await AsegurarPermisosRolesModuloAsync(db, ahora, usuarioSistema, ipSistema, ct);

            var rolesActivos = await db.TblAutenticacionRols
                .Where(r => r.Activo)
                .ToListAsync(ct);

            var empleadosPorEmail = new Dictionary<string, TblAdministracionEmpleado>(StringComparer.OrdinalIgnoreCase);
            int? idLiderPrueba = null;
            int? idLiderExternoPrueba = null;

            foreach (var def in CuentasEstandar)
            {
                var usuario = usuariosExistentes.FirstOrDefault(u => u.Email.Equals(def.Email, StringComparison.OrdinalIgnoreCase));

                // Resolver o crear Persona
                TblAdministracionPersona? persona = null;
                if (usuario?.Idpersona is > 0)
                {
                    persona = await db.TblAdministracionPersonas
                        .FirstOrDefaultAsync(p => p.Id == usuario.Idpersona.Value, ct);
                }

                persona ??= await db.TblAdministracionPersonas
                    .FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == def.Email, ct);

                persona ??= await db.TblAdministracionPersonas
                    .FirstOrDefaultAsync(p => p.Numeroidentificacion == def.Cedula, ct);

                if (persona is null)
                {
                    persona = new TblAdministracionPersona
                    {
                        Numeroidentificacion = def.Cedula,
                        Tipopersona = "NATURAL",
                        Nombres = def.Nombres,
                        Apellidos = def.Apellidos,
                        Email = def.Email,
                        Activo = true,
                        Usuariocreacion = usuarioSistema,
                        Fechacreacion = ahora,
                        Ipcreacion = ipSistema
                    };
                    db.TblAdministracionPersonas.Add(persona);
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    var modificado = false;
                    if (!persona.Activo) { persona.Activo = true; modificado = true; }
                    if (string.IsNullOrWhiteSpace(persona.Email)) { persona.Email = def.Email; modificado = true; }
                    if (string.IsNullOrWhiteSpace(persona.Nombres)) { persona.Nombres = def.Nombres; modificado = true; }
                    if (string.IsNullOrWhiteSpace(persona.Apellidos)) { persona.Apellidos = def.Apellidos; modificado = true; }
                    if (string.IsNullOrWhiteSpace(persona.Numeroidentificacion)) { persona.Numeroidentificacion = def.Cedula; modificado = true; }
                    if (modificado)
                        await db.SaveChangesAsync(ct);
                }

                // Resolver o crear Usuario
                if (usuario is null)
                {
                    usuario = new TblAutenticacionUsuario
                    {
                        Idpersona = persona.Id,
                        Email = def.Email,
                        Hashpassword = def.Email.Equals("externo@tmr.com", StringComparison.OrdinalIgnoreCase)
                            ? hasher.Hash("adminadmin")
                            : hashBase,
                        Emailverificado = true,
                        Intentosfallidos = 0,
                        Bloqueadohasta = null,
                        Debecambiarpassword = false,
                        Activo = true,
                        Usuariocreacion = usuarioSistema,
                        Fechacreacion = ahora,
                        Ipcreacion = ipSistema
                    };
                    db.TblAutenticacionUsuarios.Add(usuario);
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    var modUser = false;
                    if (def.Email.Equals("externo@tmr.com", StringComparison.OrdinalIgnoreCase)
                        && !hasher.Verify("adminadmin", usuario.Hashpassword))
                    {
                        usuario.Hashpassword = hasher.Hash("adminadmin");
                        modUser = true;
                    }
                    if (usuario.Idpersona != persona.Id)
                    {
                        var otroConMismaPersona = await db.TblAutenticacionUsuarios
                            .AnyAsync(u => u.Id != usuario.Id && u.Idpersona == persona.Id, ct);
                        if (!otroConMismaPersona)
                        {
                            usuario.Idpersona = persona.Id;
                            modUser = true;
                        }
                    }
                    if (!usuario.Activo) { usuario.Activo = true; modUser = true; }
                    if (usuario.Bloqueadohasta.HasValue) { usuario.Bloqueadohasta = null; usuario.Intentosfallidos = 0; modUser = true; }
                    if (usuario.Debecambiarpassword) { usuario.Debecambiarpassword = false; modUser = true; }
                    if (modUser)
                        await db.SaveChangesAsync(ct);
                }

                var idPersonaEfectiva = usuario.Idpersona ?? persona.Id;

                // Asegurar Rol(es) del usuario
                foreach (var rolNombre in def.RolesPreferidos)
                {
                    var rol = rolesActivos.FirstOrDefault(r => r.Nombre.Equals(rolNombre, StringComparison.OrdinalIgnoreCase));
                    if (rol is null) continue;

                    var relRol = await db.TblAutenticacionUsuarioRols
                        .FirstOrDefaultAsync(ur => ur.Idusuario == usuario.Id && ur.Idrol == rol.Id, ct);

                    if (relRol is null)
                    {
                        db.TblAutenticacionUsuarioRols.Add(new TblAutenticacionUsuarioRol
                        {
                            Idusuario = usuario.Id,
                            Idrol = rol.Id,
                            Asignadoen = ahora,
                            Activo = true,
                            Usuariocreacion = usuarioSistema,
                            Fechacreacion = ahora,
                            Ipcreacion = ipSistema
                        });
                        await db.SaveChangesAsync(ct);
                    }
                    else if (!relRol.Activo)
                    {
                        relRol.Activo = true;
                        await db.SaveChangesAsync(ct);
                    }
                }

                // Asegurar Ficha de Empleado activa (Nómina)
                var idCargoEmpleado = def.Email.Equals("rec1@tmr.com", StringComparison.OrdinalIgnoreCase)
                    ? idCargoTh
                    : idCargoDefault;

                var empleado = await db.TblAdministracionEmpleados
                    .FirstOrDefaultAsync(e => e.Idpersona == idPersonaEfectiva, ct);

                if (empleado is null)
                {
                    var codigoEmp = def.CodigoEmpleado;
                    if (await db.TblAdministracionEmpleados.AnyAsync(e => e.Codigoempleado == codigoEmp, ct))
                        codigoEmp = $"EMP-{idPersonaEfectiva:D4}";

                    empleado = new TblAdministracionEmpleado
                    {
                        Idpersona = idPersonaEfectiva,
                        Codigoempleado = codigoEmp,
                        Idcargo = idCargoEmpleado,
                        Idtipocontrato = idTipoContratoNomina,
                        Fechaingreso = new DateOnly(2022, 1, 10),
                        Fechaterminacion = null,
                        Emailcorporativo = def.Email,
                        Activo = true,
                        Usuariocreacion = usuarioSistema,
                        Fechacreacion = ahora,
                        Ipcreacion = ipSistema
                    };
                    db.TblAdministracionEmpleados.Add(empleado);
                    await db.SaveChangesAsync(ct);
                }
                else
                {
                    var modEmp = false;
                    if (!empleado.Activo || empleado.Fechaterminacion.HasValue)
                    {
                        empleado.Activo = true;
                        empleado.Fechaterminacion = null;
                        modEmp = true;
                    }
                    if (string.IsNullOrWhiteSpace(empleado.Emailcorporativo))
                    {
                        empleado.Emailcorporativo = def.Email;
                        modEmp = true;
                    }
                    if (!empleado.Idtipocontrato.HasValue && idTipoContratoNomina.HasValue)
                    {
                        empleado.Idtipocontrato = idTipoContratoNomina;
                        modEmp = true;
                    }
                    if (def.Email.Equals("rec1@tmr.com", StringComparison.OrdinalIgnoreCase) && idCargoTh.HasValue && empleado.Idcargo != idCargoTh)
                    {
                        empleado.Idcargo = idCargoTh;
                        modEmp = true;
                    }
                    else if (!empleado.Idcargo.HasValue && idCargoEmpleado.HasValue)
                    {
                        empleado.Idcargo = idCargoEmpleado;
                        modEmp = true;
                    }
                    if (modEmp)
                        await db.SaveChangesAsync(ct);
                }

                // Asegurar Contrato vigente en tbl_administracion_empleado_contrato
                if (idTipoContratoNomina.HasValue)
                {
                    var tieneContratoVigente = await db.TblAdministracionEmpleadoContratos
                        .AnyAsync(c => c.Idempleado == empleado.Id && c.Activo && c.Fechahasta == null, ct);

                    if (!tieneContratoVigente)
                    {
                        db.TblAdministracionEmpleadoContratos.Add(new TblAdministracionEmpleadoContrato
                        {
                            Idempleado = empleado.Id,
                            Idtipocontrato = idTipoContratoNomina.Value,
                            Fechadesde = empleado.Fechaingreso ?? new DateOnly(2022, 1, 10),
                            Fechahasta = null,
                            Activo = true,
                            Usuariocreacion = usuarioSistema,
                            Fechacreacion = ahora,
                            Ipcreacion = ipSistema
                        });
                        await db.SaveChangesAsync(ct);
                    }
                }

                empleadosPorEmail[def.Email] = empleado;

                // Si es lider@tmr.com, asegurar registro activo en tbl_administracion_lider
                if (def.EsLiderInterno)
                {
                    var lider = await db.TblAdministracionLiders
                        .FirstOrDefaultAsync(l => l.Idpersona == idPersonaEfectiva, ct);

                    if (lider is null)
                    {
                        lider = new TblAdministracionLider
                        {
                            Idpersona = idPersonaEfectiva,
                            Idtipo = idTipoLiderInterno,
                            Activo = true,
                            Usuariocreacion = usuarioSistema,
                            Fechacreacion = ahora,
                            Ipcreacion = ipSistema
                        };
                        db.TblAdministracionLiders.Add(lider);
                        await db.SaveChangesAsync(ct);
                    }
                    else if (!lider.Activo || (!lider.Idtipo.HasValue && idTipoLiderInterno.HasValue))
                    {
                        lider.Activo = true;
                        lider.Idtipo ??= idTipoLiderInterno;
                        await db.SaveChangesAsync(ct);
                    }

                    idLiderPrueba = lider.Id;
                }

                if (def.EsLiderExterno && idTipoLiderExterno.HasValue)
                {
                    var lider = await db.TblAdministracionLiders
                        .FirstOrDefaultAsync(l => l.Idpersona == idPersonaEfectiva, ct);
                    if (lider is null)
                    {
                        lider = new TblAdministracionLider
                        {
                            Idpersona = idPersonaEfectiva, Idtipo = idTipoLiderExterno, Activo = true,
                            Usuariocreacion = usuarioSistema, Fechacreacion = ahora, Ipcreacion = ipSistema
                        };
                        db.TblAdministracionLiders.Add(lider);
                        await db.SaveChangesAsync(ct);
                    }
                    else if (!lider.Activo || lider.Idtipo != idTipoLiderExterno)
                    {
                        lider.Activo = true;
                        lider.Idtipo = idTipoLiderExterno;
                        await db.SaveChangesAsync(ct);
                    }
                    idLiderExternoPrueba = lider.Id;
                }

                cache?.Remove($"permissions:{usuario.Id}");
            }

            // 4. Asegurar Proyecto activo y Asignación entre colaborador@tmr.com y lider@tmr.com
            if (idLiderPrueba.HasValue && empleadosPorEmail.TryGetValue("colaborador@tmr.com", out var empColab))
            {
                await AsegurarProyectoYAsignacionAsync(
                    db,
                    empColab,
                    empleadosPorEmail.GetValueOrDefault("lider@tmr.com"),
                    idLiderPrueba.Value,
                    ahora,
                    usuarioSistema,
                    ipSistema,
                    ct);
            }

            if (idLiderExternoPrueba.HasValue && empleadosPorEmail.TryGetValue("colaborador@tmr.com", out var empColabExterno))
                await AsegurarAsignacionLiderExternoAsync(db, empColabExterno, idLiderExternoPrueba.Value, ahora, usuarioSistema, ipSistema, ct);

            _ultimaVerificacionUtc = DateTime.UtcNow;
        }
        catch
        {
            // No interrumpir peticiones si el entorno usa una BD parcial de pruebas
            db.ChangeTracker.Clear();
        }
        finally
        {
            Mutex.Release();
        }
    }

    private static async Task<int?> ObtenerIdContratoNominaAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var detallesTct = await db.TblAdministracionCatalogoDetalles
            .AsNoTracking()
            .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == "TCT")
            .ToListAsync(ct);

        var nomina = detallesTct.FirstOrDefault(d =>
            !string.Equals(d.Codigovalor?.Trim(), "COS", StringComparison.OrdinalIgnoreCase)
            && !(d.Valor?.Contains("Consultor", StringComparison.OrdinalIgnoreCase) ?? false)
            && !(d.Valor?.Contains("Servicios", StringComparison.OrdinalIgnoreCase) ?? false));

        return nomina?.Id ?? detallesTct.FirstOrDefault()?.Id;
    }

    private static async Task<int?> ObtenerIdTipoLiderInternoAsync(ApplicationDbContext db, CancellationToken ct)
    {
        return await db.TblAdministracionCatalogoDetalles
            .AsNoTracking()
            .Where(d => d.Activo
                        && d.IdcatalogoNavigation.Codigo == "TLI"
                        && (d.Codigovalor == "INT" || d.Valor.ToUpper().Contains("INTERN")))
            .Select(d => (int?)d.Id)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<int?> ObtenerIdTipoLiderExternoAsync(ApplicationDbContext db, CancellationToken ct)
    {
        return await db.TblAdministracionCatalogoDetalles.AsNoTracking()
            .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == "TLI"
                && (d.Codigovalor == "EXT" || d.Valor.ToUpper().Contains("EXTERN")))
            .Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
    }

    private static async Task AsegurarAsignacionLiderExternoAsync(
        ApplicationDbContext db, TblAdministracionEmpleado colaborador, int idLiderExterno,
        DateTime ahora, string usuarioSistema, string ipSistema, CancellationToken ct)
    {
        var asignacion = await db.TblTimeReportAsignacionProyectos
            .Where(a => a.Idempleado == colaborador.Id && a.Activo && a.Estadoasignacion)
            .OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
        if (asignacion is null) return;
        var existe = await db.TblTimeReportAsignacionProyectos.AnyAsync(a =>
            a.Idproyecto == asignacion.Idproyecto && a.Idempleado == null && a.Idlider == idLiderExterno && a.Activo, ct);
        if (existe) return;
        db.TblTimeReportAsignacionProyectos.Add(new TblTimeReportAsignacionProyecto
        {
            Idproyecto = asignacion.Idproyecto, Idempleado = null, Idlider = idLiderExterno,
            Rolasignado = "Líder Externo de Prueba", Fechaasignacion = new DateOnly(2025, 1, 1),
            Costoporhora = 0m, Horasasignadas = 160m, Estadoasignacion = true, Activo = true,
            Usuariocreacion = usuarioSistema, Fechacreacion = ahora, Ipcreacion = ipSistema
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task<int?> AsegurarCargoAsync(
        ApplicationDbContext db,
        string nombreCargo,
        DateTime ahora,
        string usuarioSistema,
        string ipSistema,
        CancellationToken ct)
    {
        var cargo = await db.TblAdministracionCargos
            .FirstOrDefaultAsync(c => c.Nombrecargo.ToUpper() == nombreCargo.ToUpper(), ct);

        if (cargo is not null)
        {
            if (!cargo.Activo)
            {
                cargo.Activo = true;
                await db.SaveChangesAsync(ct);
            }
            return cargo.Id;
        }

        var idDepto = await db.TblAdministracionCatalogoDetalles
            .AsNoTracking()
            .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == "DEP")
            .OrderBy(d => d.Id)
            .Select(d => (int?)d.Id)
            .FirstOrDefaultAsync(ct);

        var nuevo = new TblAdministracionCargo
        {
            Nombrecargo = nombreCargo,
            Descripcion = nombreCargo,
            Iddepartamento = idDepto,
            Activo = true,
            Usuariocreacion = usuarioSistema,
            Fechacreacion = ahora,
            Ipcreacion = ipSistema
        };
        db.TblAdministracionCargos.Add(nuevo);
        await db.SaveChangesAsync(ct);
        return nuevo.Id;
    }

    private static async Task AsegurarPermisosRolesModuloAsync(
        ApplicationDbContext db,
        DateTime ahora,
        string usuarioSistema,
        string ipSistema,
        CancellationToken ct)
    {
        var roles = await db.TblAutenticacionRols.Where(r => r.Activo).ToListAsync(ct);
        if (!roles.Any(r => r.Nombre.Equals("Gerencia", StringComparison.OrdinalIgnoreCase) || r.Nombre.Equals("Gerente", StringComparison.OrdinalIgnoreCase)))
        {
            db.TblAutenticacionRols.Add(new TblAutenticacionRol
            {
                Nombre = "Gerencia",
                Descripcion = "Rol de Gerencia con acceso a reportes e historial general",
                Essistema = true,
                Activo = true,
                Usuariocreacion = usuarioSistema,
                Fechacreacion = ahora,
                Ipcreacion = ipSistema
            });
            await db.SaveChangesAsync(ct);
            roles = await db.TblAutenticacionRols.Where(r => r.Activo).ToListAsync(ct);
        }

        if (!roles.Any(r => r.Nombre.Equals("Recursos Humanos", StringComparison.OrdinalIgnoreCase) || r.Nombre.Equals("RECURSOS_HUMANOS", StringComparison.OrdinalIgnoreCase)))
        {
            db.TblAutenticacionRols.Add(new TblAutenticacionRol
            {
                Nombre = "Recursos Humanos",
                Descripcion = "Gestión de Talento Humano y validación de permisos",
                Essistema = true,
                Activo = true,
                Usuariocreacion = usuarioSistema,
                Fechacreacion = ahora,
                Ipcreacion = ipSistema
            });
            await db.SaveChangesAsync(ct);
            roles = await db.TblAutenticacionRols.Where(r => r.Activo).ToListAsync(ct);
        }

        var permisosModulo = await db.TblAutenticacionPermisos
            .Where(p => p.Activo && (
                p.Codigo == "PERMISOS_VIEW" ||
                p.Codigo == "MIS_PERMISOS_READ" ||
                p.Codigo == "MIS_PERMISOS_CREATE" ||
                p.Codigo == "PERMISOS_LIDER_READ" ||
                p.Codigo == "PERMISOS_LIDER_APPROVE" ||
                p.Codigo == "PERMISOS_RRHH_READ" ||
                p.Codigo == "PERMISOS_RRHH_CONFIRM" ||
                p.Codigo == "PERMISOS_GERENCIA_READ" ||
                p.Codigo == "SEGUIMIENTO_READ"))
            .ToListAsync(ct);

        if (permisosModulo.Count == 0)
            return;

        var asignacionesActuales = await db.TblAutenticacionRolPermisos
            .Where(rp => permisosModulo.Select(p => p.Id).Contains(rp.Idpermiso))
            .ToListAsync(ct);

        var cambios = false;
        foreach (var rol in roles)
        {
            var nombreUpper = rol.Nombre.Trim().ToUpperInvariant();
            var esAdmin = nombreUpper is "ADMINISTRADOR" or "ADMIN";
            var esLider = nombreUpper is "LIDER" or "LÍDER";
            var esRrhh = nombreUpper is "RECURSOS HUMANOS" or "RECURSOS_HUMANOS";
            var esGerencia = nombreUpper is "GERENCIA" or "GERENTE";

            foreach (var perm in permisosModulo)
            {
                var aplica = perm.Codigo switch
                {
                    "PERMISOS_VIEW" or "MIS_PERMISOS_READ" or "MIS_PERMISOS_CREATE" => true,
                    "PERMISOS_LIDER_READ" or "PERMISOS_LIDER_APPROVE" => esAdmin || esLider,
                    "PERMISOS_RRHH_READ" or "PERMISOS_RRHH_CONFIRM" => esAdmin || esRrhh,
                    "PERMISOS_GERENCIA_READ" or "SEGUIMIENTO_READ" => esAdmin || esGerencia || esRrhh || esLider,
                    _ => false
                };

                if (!aplica) continue;

                var existente = asignacionesActuales.FirstOrDefault(rp => rp.Idrol == rol.Id && rp.Idpermiso == perm.Id);
                if (existente is null)
                {
                    db.TblAutenticacionRolPermisos.Add(new TblAutenticacionRolPermiso
                    {
                        Idrol = rol.Id,
                        Idpermiso = perm.Id,
                        Activo = true,
                        Usuariocreacion = usuarioSistema,
                        Fechacreacion = ahora,
                        Ipcreacion = ipSistema
                    });
                    cambios = true;
                }
                else if (!existente.Activo)
                {
                    existente.Activo = true;
                    cambios = true;
                }
            }
        }

        if (cambios)
            await db.SaveChangesAsync(ct);
    }

    private static async Task AsegurarProyectoYAsignacionAsync(
        ApplicationDbContext db,
        TblAdministracionEmpleado empColab,
        TblAdministracionEmpleado? empLider,
        int idLider,
        DateTime ahora,
        string usuarioSistema,
        string ipSistema,
        CancellationToken ct)
    {
        var asignacionesColab = await db.TblTimeReportAsignacionProyectos
            .Where(a => a.Idempleado == empColab.Id && a.Activo)
            .ToListAsync(ct);

        if (asignacionesColab.Any(a => a.Idlider == idLider && a.Estadoasignacion))
            return;

        var asignacionExistente = asignacionesColab.FirstOrDefault();
        if (asignacionExistente is not null)
        {
            asignacionExistente.Idlider = idLider;
            asignacionExistente.Estadoasignacion = true;
            await db.SaveChangesAsync(ct);
            return;
        }

        var idProyecto = await db.TblTimeReportAsignacionProyectos
            .AsNoTracking()
            .Where(a => a.Activo && a.Idlider == idLider && a.IdproyectoNavigation.Activo)
            .Select(a => (int?)a.Idproyecto)
            .FirstOrDefaultAsync(ct)
            ?? await db.TblTimeReportProyectos
                .AsNoTracking()
                .Where(p => p.Activo)
                .OrderBy(p => p.Id)
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(ct);

        if (!idProyecto.HasValue)
        {
            var idCliente = await db.TblAdministracionClientes
                .AsNoTracking()
                .Where(c => c.Activo)
                .OrderBy(c => c.Id)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);

            if (!idCliente.HasValue)
            {
                var cliente = new TblAdministracionCliente
                {
                    Numeroidentificacion = "1799999999001",
                    Razonsocial = "INTEGRITY SOLUTIONS CONSULTING S.A.",
                    Nombrecomercial = "Integrity Solutions (ISC)",
                    Nombres = "Integrity Solutions",
                    Apellidos = "Consulting",
                    Email = "contacto@integritysolutions.com.ec",
                    Activo = true,
                    Usuariocreacion = usuarioSistema,
                    Fechacreacion = ahora,
                    Ipcreacion = ipSistema
                };
                db.TblAdministracionClientes.Add(cliente);
                await db.SaveChangesAsync(ct);
                idCliente = cliente.Id;
            }

            var idEstadoProy = await db.TblAdministracionCatalogoDetalles
                .AsNoTracking()
                .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == "EPR")
                .OrderBy(d => d.Id)
                .Select(d => (int?)d.Id)
                .FirstOrDefaultAsync(ct)
                ?? await db.TblAdministracionCatalogoDetalles
                    .AsNoTracking()
                    .Where(d => d.Activo)
                    .OrderBy(d => d.Id)
                    .Select(d => d.Id)
                    .FirstAsync(ct);

            var proyecto = new TblTimeReportProyecto
            {
                Codigo = "PRY-ISC-001",
                Nombre = "Operaciones y Desarrollo TMR",
                Descripcion = "Proyecto corporativo para gestión de Time Report y Permisos",
                Idcliente = idCliente.Value,
                Idestadoproyecto = idEstadoProy,
                Fechainicioplaneada = new DateOnly(2025, 1, 1),
                Activo = true,
                Usuariocreacion = usuarioSistema,
                Fechacreacion = ahora,
                Ipcreacion = ipSistema
            };
            db.TblTimeReportProyectos.Add(proyecto);
            await db.SaveChangesAsync(ct);
            idProyecto = proyecto.Id;
        }

        db.TblTimeReportAsignacionProyectos.Add(new TblTimeReportAsignacionProyecto
        {
            Idproyecto = idProyecto.Value,
            Idempleado = empColab.Id,
            Idlider = idLider,
            Rolasignado = "Analista / Desarrollador",
            Fechaasignacion = new DateOnly(2025, 1, 1),
            Costoporhora = 0m,
            Horasasignadas = 160m,
            Estadoasignacion = true,
            Activo = true,
            Usuariocreacion = usuarioSistema,
            Fechacreacion = ahora,
            Ipcreacion = ipSistema
        });

        if (empLider is not null)
        {
            var tieneAsigLider = await db.TblTimeReportAsignacionProyectos
                .AnyAsync(a => a.Idproyecto == idProyecto.Value && a.Idempleado == empLider.Id && a.Activo, ct);

            if (!tieneAsigLider)
            {
                db.TblTimeReportAsignacionProyectos.Add(new TblTimeReportAsignacionProyecto
                {
                    Idproyecto = idProyecto.Value,
                    Idempleado = empLider.Id,
                    Idlider = idLider,
                    Rolasignado = "Líder de Proyecto",
                    Fechaasignacion = new DateOnly(2025, 1, 1),
                    Costoporhora = 0m,
                    Horasasignadas = 160m,
                    Estadoasignacion = true,
                    Activo = true,
                    Usuariocreacion = usuarioSistema,
                    Fechacreacion = ahora,
                    Ipcreacion = ipSistema
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
