using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.CargaActividades
{
    public interface ICargarActividadesExcelHandler
    {
        Task<CargaActividadesResponse> HandleAsync(CargarActividadesExcelCommand command, CancellationToken cancellationToken = default);
    }

    public class CargarActividadesExcelHandler : ICargarActividadesExcelHandler
    {
        private readonly ApplicationDbContext _context;

        public CargarActividadesExcelHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CargaActividadesResponse> HandleAsync(CargarActividadesExcelCommand command, CancellationToken cancellationToken = default)
        {
            var erroresValidacion = new List<string>();
            var actividadesParaInsertar = new List<TblTimeReportActividadDiarium>();
            var listaParaFrontend = new List<object>();
            var firmasFilasProcesadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var streamArchivo = command.File.OpenReadStream();
                var fileName = command.File.FileName?.Trim() ?? string.Empty;
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(extension))
                    return CargaActividadesResponse.Failure("El archivo debe tener extensión .xlsx o .csv.");

                if (extension != ".xlsx" && extension != ".csv")
                {
                    if (extension == ".xls")
                        return CargaActividadesResponse.Failure("El formato .xls no es compatible. Use un archivo .xlsx o .csv.");

                    return CargaActividadesResponse.Failure("El archivo debe ser .xlsx o .csv.");
                }

                var tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + extension);
                await using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await streamArchivo.CopyToAsync(fs);
                }

                List<object> datosCrudosExcel;
                try
                {
                    datosCrudosExcel = MiniExcel.Query(tempFilePath, useHeaderRow: true).ToList();
                }
                catch (Exception ex)
                {
                    if (File.Exists(tempFilePath))
                        try { File.Delete(tempFilePath); } catch { }

                    return CargaActividadesResponse.Failure("El archivo no es un Excel válido o está dañado.", new List<string> { ex.Message });
                }

                if (File.Exists(tempFilePath))
                    try { File.Delete(tempFilePath); } catch { }

                var filaIndex = 1;

                foreach (dynamic fila in datosCrudosExcel)
                {
                    filaIndex++;
                    var filaDict = (IDictionary<string, object>)fila;
                    var filaNormalizada = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var kvp in filaDict)
                    {
                        var key = (kvp.Key ?? string.Empty).Trim();
                        var value = kvp.Value switch
                        {
                            DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            DateOnly d  => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            _           => kvp.Value?.ToString()?.Trim() ?? string.Empty
                        };
                        filaNormalizada[key] = value;
                    }

                    if (filaNormalizada.Values.All(string.IsNullOrWhiteSpace))
                        continue;

                    var codigoEmpleado     = GetFirstValue(filaNormalizada, "CodigoEmpleado", "CódigoEmpleado", "Codigo Empleado", "Codigo");
                    var cedula             = GetFirstValue(filaNormalizada, "Cedula", "Cédula", "NumeroIdentificacion", "Numeroidentificacion");
                    var emailCorporativo   = GetFirstValue(filaNormalizada, "EmailCorporativo", "Email", "Email Corporativo");
                    var colaborador        = GetFirstValue(filaNormalizada, "Colaborador", "Nombre", "NombreCompleto");
                    var tipoActividad      = GetFirstValue(filaNormalizada, "TipoActividad", "Tipo Actividad");
                    var proyecto           = GetFirstValue(filaNormalizada, "Proyecto", "ProyectoNombre", "CodigoProyecto", "CódigoProyecto");
                    var descripcionActividad = GetFirstValue(filaNormalizada, "DescripcionActividad", "Descripcion Actividad", "Descripcion", "DescripciónActividad");
                    var notas              = GetFirstValue(filaNormalizada, "Notas", "Nota");
                    var fechaActividadTexto = GetFirstValue(filaNormalizada, "FechaActividad", "Fecha Actividad", "Fecha");
                    var horasTexto         = GetFirstValue(filaNormalizada, "CantidadHoras", "Cantidad Horas", "Horas", "NroHoras", "HorasTrabajadas");
                    var codigoRequerimiento = GetFirstValue(filaNormalizada, "CodigoRequerimiento", "CódigoRequerimiento", "Código Requerimiento", "Requerimiento");
                    var cliente            = GetFirstValue(filaNormalizada, "Cliente", "ClienteNombre", "NombreCliente");
                    var lider              = GetFirstValue(filaNormalizada, "LiderTecnico", "Lider", "Lider Técnico", "Lider Tecnico");

                    if (string.IsNullOrWhiteSpace(descripcionActividad))
                        descripcionActividad = "Actividad registrada desde carga de Excel.";

                    if (!TryParseDateOnly(fechaActividadTexto, out var fechaActividad))
                    {
                        erroresValidacion.Add($"Fila {filaIndex}: Fecha de actividad inválida ('{fechaActividadTexto}').");
                        continue;
                    }

                    if (!TryParseDecimal(horasTexto, out var cantidadHoras))
                    {
                        erroresValidacion.Add($"Fila {filaIndex}: Cantidad de horas inválida ('{horasTexto}').");
                        continue;
                    }

                    // ── VALIDACIÓN EXPLÍCITA DEL CHECK CONSTRAINT ──────────────────
                    if (cantidadHoras <= 0 || cantidadHoras > 24)
                    {
                        erroresValidacion.Add($"Fila {filaIndex}: CantidadHoras ({cantidadHoras}) debe ser mayor a 0 y menor o igual a 24.");
                        continue;
                    }

                    var empleado = await FindEmpleadoAsync(codigoEmpleado, emailCorporativo, cancellationToken)
                        ?? await GetOrCreateFallbackEmpleadoAsync(codigoEmpleado, emailCorporativo, cedula, colaborador, cancellationToken);

                    var tipoActividadId = await GetOrCreateTipoActividadIdAsync(tipoActividad, cancellationToken);
                    if (tipoActividadId is null)
                    {
                        erroresValidacion.Add($"Fila {filaIndex}: no se pudo determinar el tipo de actividad.");
                        continue;
                    }

                    var proyectoId = await FindProyectoIdAsync(proyecto, cancellationToken);
                    var codigoRequerimientoNormalizado = string.IsNullOrWhiteSpace(codigoRequerimiento) ? null : codigoRequerimiento.Trim();
                    var descripcionNormalizada = descripcionActividad.Trim();

                    var firmaFila = BuildRowSignature(
                        empleado.Id,
                        proyectoId,
                        tipoActividadId.Value,
                        fechaActividad,
                        cantidadHoras,
                        codigoRequerimientoNormalizado,
                        descripcionNormalizada);

                    if (!firmasFilasProcesadas.Add(firmaFila))
                    {
                        erroresValidacion.Add($"Fila {filaIndex}: registro duplicado dentro del archivo.");
                        continue;
                    }

                    var existeEnBase = await _context.TblTimeReportActividadDiaria.AnyAsync(a =>
                        a.Idempleado == empleado.Id &&
                        a.Idproyecto == proyectoId &&
                        a.Idtipoactividad == tipoActividadId.Value &&
                        a.Fechaactividad == fechaActividad &&
                        a.Cantidadhoras == cantidadHoras &&
                        a.Codigorequerimiento == codigoRequerimientoNormalizado &&
                        a.Descripcionactividad == descripcionNormalizada &&
                        a.Activo, cancellationToken);

                    if (existeEnBase)
                    {
                        erroresValidacion.Add($"Fila {filaIndex}: registro duplicado en la base de datos. No se volverá a insertar.");
                        continue;
                    }

                    actividadesParaInsertar.Add(new TblTimeReportActividadDiarium
                    {
                        Idempleado           = empleado.Id,
                        Idproyecto           = proyectoId,
                        Idtipoactividad      = tipoActividadId.Value,
                        Codigorequerimiento  = codigoRequerimientoNormalizado,
                        Cantidadhoras        = cantidadHoras,
                        Fechaactividad       = fechaActividad,
                        Descripcionactividad = descripcionNormalizada,
                        Notas                = string.IsNullOrWhiteSpace(notas) ? null : notas,
                        Esbillable           = null,
                        Activo               = true,
                        Usuariocreacion      = string.IsNullOrWhiteSpace(command.ColaboradorId) ? "carga_excel" : command.ColaboradorId,
                        Fechacreacion        = DateTime.UtcNow,
                        Ipcreacion           = "127.0.0.1"
                    });

                    listaParaFrontend.Add(new
                    {
                        id           = (string?)null,
                        colaborador  = colaborador,
                        proyecto     = proyecto,
                        cliente      = cliente,
                        liderTecnico = lider,
                         fecha        = fechaActividad.ToString("yyyy-MM-dd"),
                        nroHoras     = cantidadHoras,
                        estado       = "Cargado"
                    });
                }

                if (actividadesParaInsertar.Count == 0)
                {
                    return erroresValidacion.Count > 0
                        ? CargaActividadesResponse.Failure("No se procesaron filas válidas.", erroresValidacion)
                        : CargaActividadesResponse.Failure("No se encontró ninguna fila con datos.");
                }

                await _context.TblTimeReportActividadDiaria.AddRangeAsync(actividadesParaInsertar, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                var response = CargaActividadesResponse.Success(
                    actividadesParaInsertar.Count,
                    "La planilla de actividades se ha procesado y sincronizado con éxito.",
                    listaParaFrontend);

                if (erroresValidacion.Any())
                    response.ErroresValidacion = erroresValidacion;

                return response;
            }
            catch (Exception ex)
            {
                var baseMessage = ex.GetBaseException()?.Message;
                var inner       = ex.InnerException?.Message;
                var detalle     = baseMessage;
                if (!string.IsNullOrWhiteSpace(inner) && inner != baseMessage)
                    detalle += " - " + inner;

                return CargaActividadesResponse.Failure(
                    $"Error crítico en el lote de carga: {ex.Message}" +
                    (string.IsNullOrWhiteSpace(detalle) ? string.Empty : " - " + detalle));
            }
        }

        // ── HELPERS ────────────────────────────────────────────────────────────────

        private static string GetFirstValue(IDictionary<string, string> fila, params string[] keys)
        {
            foreach (var key in keys)
                if (fila.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value.Trim();

            return string.Empty;
        }

        private static bool TryParseDecimal(string value, out decimal result)
        {
            result = 0m;
            if (string.IsNullOrWhiteSpace(value)) return false;

            return decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out result)
                || decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out result);
        }

        private static string BuildRowSignature(
            int empleadoId,
            int? proyectoId,
            int tipoActividadId,
            DateOnly fechaActividad,
            decimal cantidadHoras,
            string? codigoRequerimiento,
            string descripcionActividad)
        {
            return string.Join("|",
                empleadoId,
                proyectoId?.ToString() ?? "NULL",
                tipoActividadId,
                fechaActividad.ToString("yyyy-MM-dd"),
                cantidadHoras.ToString(CultureInfo.InvariantCulture),
                codigoRequerimiento ?? "",
                descripcionActividad.Trim());
        }

        private static bool TryParseDateOnly(string value, out DateOnly result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value)) return false;

            if (DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) return true;
            if (DateOnly.TryParse(value.Trim(), CultureInfo.CurrentCulture,   DateTimeStyles.None, out result)) return true;
            if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) { result = DateOnly.FromDateTime(dt); return true; }
            if (DateTime.TryParse(value.Trim(), CultureInfo.CurrentCulture,   DateTimeStyles.None, out dt))     { result = DateOnly.FromDateTime(dt); return true; }

            return false;
        }

        private async Task<TblAdministracionEmpleado?> FindEmpleadoAsync(string codigoEmpleado, string email, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(codigoEmpleado))
            {
                var emp = await _context.TblAdministracionEmpleados
                    .FirstOrDefaultAsync(e => e.Codigoempleado == codigoEmpleado.Trim(), ct);
                if (emp != null) return emp;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var emp = await _context.TblAdministracionEmpleados
                    .FirstOrDefaultAsync(e => e.Emailcorporativo != null && e.Emailcorporativo.ToLower() == email.Trim().ToLower(), ct);
                if (emp != null) return emp;
            }

            return null;
        }

        private async Task<TblAdministracionEmpleado> GetOrCreateFallbackEmpleadoAsync(
            string codigoEmpleado, string email, string cedula, string colaborador, CancellationToken ct)
        {
            const string fallbackCodigo = "CARGA_EXCEL";
            const string fallbackEmail  = "carga-excel@local";

            var empleadoExistente = await _context.TblAdministracionEmpleados
                .FirstOrDefaultAsync(e => e.Codigoempleado == fallbackCodigo || e.Emailcorporativo == fallbackEmail, ct);
            if (empleadoExistente != null) return empleadoExistente;

            TblAdministracionPersona? persona = null;

            if (!string.IsNullOrWhiteSpace(cedula))
                persona = await _context.TblAdministracionPersonas
                    .FirstOrDefaultAsync(p => p.Numeroidentificacion == cedula.Trim(), ct);

            if (persona == null && !string.IsNullOrWhiteSpace(email))
                persona = await _context.TblAdministracionPersonas
                    .FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == email.Trim().ToLower(), ct);

            if (persona == null)
            {
                var identificacionFallback = !string.IsNullOrWhiteSpace(cedula)
                    ? cedula.Trim()
                    : $"CARGAEXCEL-{Guid.NewGuid():N}".Substring(0, 20);

                // ── FIX: usar Nombres/Apellidos que mapean a primernombre/apellidopaterno via [Column] ──
                persona = new TblAdministracionPersona
                {
                    Numeroidentificacion = identificacionFallback,
                    Tipopersona          = "NATURAL",
                    Nombres              = string.IsNullOrWhiteSpace(colaborador) ? "Carga Excel" : colaborador.Trim(),
                    Apellidos            = "Usuario",
                    Email                = string.IsNullOrWhiteSpace(email) ? fallbackEmail : email.Trim(),
                    Activo               = true,
                    Usuariocreacion      = "carga_excel",
                    Fechacreacion        = DateTime.UtcNow,
                    Ipcreacion           = "127.0.0.1"
                };

                _context.TblAdministracionPersonas.Add(persona);
                await _context.SaveChangesAsync(ct);
            }

            var empleadoPorPersona = await _context.TblAdministracionEmpleados
                .FirstOrDefaultAsync(e => e.Idpersona == persona.Id, ct);
            if (empleadoPorPersona != null) return empleadoPorPersona;

            var empleado = new TblAdministracionEmpleado
            {
                Idpersona        = persona.Id,
                Codigoempleado   = string.IsNullOrWhiteSpace(codigoEmpleado) ? fallbackCodigo : codigoEmpleado.Trim(),
                Emailcorporativo = string.IsNullOrWhiteSpace(email) ? fallbackEmail : email.Trim(),
                Activo           = true,
                Usuariocreacion  = "carga_excel",
                Fechacreacion    = DateTime.UtcNow,
                Ipcreacion       = "127.0.0.1"
            };

            _context.TblAdministracionEmpleados.Add(empleado);
            await _context.SaveChangesAsync(ct);

            return empleado;
        }

        private async Task<int?> GetOrCreateTipoActividadIdAsync(string tipoActividad, CancellationToken ct)
        {
            const string fallbackTipo = "Carga Excel";

            if (string.IsNullOrWhiteSpace(tipoActividad))
            {
                var tipoExistente = await _context.TblTimeReportTipoActividads
                    .FirstOrDefaultAsync(t => t.Nombretipo == fallbackTipo, ct);
                if (tipoExistente != null) return tipoExistente.Id;

                var nuevoTipo = new TblTimeReportTipoActividad
                {
                    Nombretipo      = fallbackTipo,
                    Descripcion     = "Tipo de actividad generado por carga de prueba",
                    Activo          = true,
                    Usuariocreacion = "carga_excel",
                    Fechacreacion   = DateTime.UtcNow,
                    Ipcreacion      = "127.0.0.1"
                };
                _context.TblTimeReportTipoActividads.Add(nuevoTipo);
                await _context.SaveChangesAsync(ct);
                return nuevoTipo.Id;
            }

            var normalizedTipo = tipoActividad.Trim().ToLower();

            var tipo = await _context.TblTimeReportTipoActividads
                .FirstOrDefaultAsync(t => t.Nombretipo != null && t.Nombretipo.ToLower() == normalizedTipo, ct);
            if (tipo != null) return tipo.Id;

            var tipoParcial = await _context.TblTimeReportTipoActividads
                .FirstOrDefaultAsync(t => t.Nombretipo != null && t.Nombretipo.ToLower().Contains(normalizedTipo), ct);
            if (tipoParcial != null) return tipoParcial.Id;

            var nuevo = new TblTimeReportTipoActividad
            {
                Nombretipo      = tipoActividad.Trim(),
                Descripcion     = $"Tipo creado desde carga: {tipoActividad.Trim()}",
                Activo          = true,
                Usuariocreacion = "carga_excel",
                Fechacreacion   = DateTime.UtcNow,
                Ipcreacion      = "127.0.0.1"
            };
            _context.TblTimeReportTipoActividads.Add(nuevo);
            await _context.SaveChangesAsync(ct);
            return nuevo.Id;
        }

        private async Task<int?> FindProyectoIdAsync(string proyecto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(proyecto)) return null;

            var norm = proyecto.Trim().ToLower();

            var proyectoEntidad = await _context.TblTimeReportProyectos
                .FirstOrDefaultAsync(p =>
                    (p.Codigo != null && p.Codigo.ToLower() == norm) ||
                    (p.Nombre != null && p.Nombre.ToLower() == norm), ct);

            if (proyectoEntidad != null) return proyectoEntidad.Id;

            proyectoEntidad = await _context.TblTimeReportProyectos
                .FirstOrDefaultAsync(p => p.Nombre != null && p.Nombre.ToLower().Contains(norm), ct);

            return proyectoEntidad?.Id;
        }
    }
}