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

            try
            {
                using var streamArchivo = command.File.OpenReadStream();
                var fileName = command.File.FileName?.Trim() ?? string.Empty;
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(extension))
                {
                    return CargaActividadesResponse.Failure("El archivo debe tener extensión .xlsx o .csv.");
                }

                if (extension != ".xlsx" && extension != ".csv")
                {
                    if (extension == ".xls")
                    {
                        return CargaActividadesResponse.Failure("El formato .xls no es compatible. Use un archivo .xlsx o .csv.");
                    }

                    return CargaActividadesResponse.Failure("El archivo debe ser .xlsx o .csv.");
                }

                // Guardar temporalmente el archivo con la extensión original para que MiniExcel pueda detectar el tipo por la extensión
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
                    {
                        try { File.Delete(tempFilePath); } catch { /* ignore */ }
                    }

                    return CargaActividadesResponse.Failure("El archivo no es un Excel válido o está dañado. Use un .xlsx o .csv correcto.", new List<string> { ex.Message });
                }

                if (File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); } catch { /* ignore */ }
                }

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
                            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                            _ => kvp.Value?.ToString()?.Trim() ?? string.Empty
                        };
                        filaNormalizada[key] = value;
                    }

                    if (filaNormalizada.Values.All(string.IsNullOrWhiteSpace))
                    {
                        continue;
                    }

                    var codigoEmpleado = GetFirstValue(filaNormalizada, "CodigoEmpleado", "CódigoEmpleado", "Codigo Empleado", "Codigo");
                    var cedula = GetFirstValue(filaNormalizada, "Cedula", "Cédula", "NumeroIdentificacion", "Numeroidentificacion");
                    var emailCorporativo = GetFirstValue(filaNormalizada, "EmailCorporativo", "Email", "Email Corporativo");
                    var colaborador = GetFirstValue(filaNormalizada, "Colaborador", "Nombre", "NombreCompleto");
                    var tipoActividad = GetFirstValue(filaNormalizada, "TipoActividad", "Tipo Actividad", "TipoActividad");
                    var proyecto = GetFirstValue(filaNormalizada, "Proyecto", "ProyectoNombre", "CodigoProyecto", "CódigoProyecto");
                    var descripcionActividad = GetFirstValue(filaNormalizada, "DescripcionActividad", "Descripcion Actividad", "Descripcion", "DescripciónActividad");
                    var notas = GetFirstValue(filaNormalizada, "Notas", "Nota");
                    var fechaActividadTexto = GetFirstValue(filaNormalizada, "FechaActividad", "Fecha Actividad", "Fecha");
                    var horasTexto = GetFirstValue(filaNormalizada, "CantidadHoras", "Cantidad Horas", "Horas", "NroHoras", "HorasTrabajadas");
                    var codigoRequerimiento = GetFirstValue(filaNormalizada, "CodigoRequerimiento", "CódigoRequerimiento", "Código Requerimiento", "Requerimiento");
                    var cliente = GetFirstValue(filaNormalizada, "Cliente", "ClienteNombre", "NombreCliente");
                    var lider = GetFirstValue(filaNormalizada, "LiderTecnico", "Lider", "Lider Técnico", "Lider Tecnico");

                    if (string.IsNullOrWhiteSpace(descripcionActividad))
                    {
                        descripcionActividad = "Actividad registrada desde carga de Excel.";
                    }

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

                    var empleado = await FindEmpleadoAsync(codigoEmpleado, emailCorporativo, cancellationToken)
                        ?? await GetOrCreateFallbackEmpleadoAsync(codigoEmpleado, emailCorporativo, cedula, colaborador, cancellationToken);

                    var tipoActividadId = await GetOrCreateTipoActividadIdAsync(tipoActividad, cancellationToken);
                    if (tipoActividadId is null)
                    {
                        erroresValidacion.Add($"Fila {filaIndex}: no se pudo determinar el tipo de actividad.");
                        continue;
                    }

                    var proyectoId = await FindProyectoIdAsync(proyecto, cancellationToken);

                    actividadesParaInsertar.Add(new TblTimeReportActividadDiarium
                    {
                        Idempleado = empleado.Id,
                        Idproyecto = proyectoId,
                        Idtipoactividad = tipoActividadId.Value,
                        Codigorequerimiento = string.IsNullOrWhiteSpace(codigoRequerimiento) ? null : codigoRequerimiento,
                        Cantidadhoras = cantidadHoras,
                        Fechaactividad = fechaActividad,
                        Descripcionactividad = descripcionActividad,
                        Notas = string.IsNullOrWhiteSpace(notas) ? null : notas,
                        Esbillable = null,
                        Activo = true,
                        Usuariocreacion = string.IsNullOrWhiteSpace(command.ColaboradorId) ? "carga_excel" : command.ColaboradorId,
                        Fechacreacion = DateTime.UtcNow,
                        Ipcreacion = "127.0.0.1"
                    });

                    listaParaFrontend.Add(new
                    {
                        id = (string?)null,
                        colaborador = colaborador,
                        proyecto = proyecto,
                        cliente = cliente,
                        liderTecnico = lider,
                        nroHoras = cantidadHoras,
                        estado = "Cargado"
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

                listaParaFrontend.Clear();

                foreach (var actividad in actividadesParaInsertar)
                {
                    listaParaFrontend.Add(new
                    {
                        id = actividad.Id.ToString(),
                        colaborador = string.Empty,
                        proyecto = string.Empty,
                        cliente = string.Empty,
                        liderTecnico = string.Empty,
                        nroHoras = actividad.Cantidadhoras,
                        estado = "Cargado"
                    });
                }

                var response = CargaActividadesResponse.Success(
                    actividadesParaInsertar.Count,
                    "La planilla de actividades se ha procesado y sincronizado con éxito.",
                    listaParaFrontend);

                if (erroresValidacion.Any())
                {
                    response.ErroresValidacion = erroresValidacion;
                }

                return response;
            }
            catch (Exception ex)
            {
                var baseMessage = ex.GetBaseException()?.Message;
                var inner = ex.InnerException?.Message;
                var detalle = baseMessage;
                if (!string.IsNullOrWhiteSpace(inner) && inner != baseMessage)
                {
                    detalle += " - " + inner;
                }

                return CargaActividadesResponse.Failure($"Error crítico en el lote de carga: {ex.Message}{(string.IsNullOrWhiteSpace(detalle) ? string.Empty : " - " + detalle)}");
            }
        }

        private static string GetFirstValue(IDictionary<string, string> fila, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (fila.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static bool TryParseDecimal(string value, out decimal result)
        {
            result = 0m;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out result)
                || decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out result);
        }

        private static bool TryParseDateOnly(string value, out DateOnly result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (DateOnly.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return true;
            }

            if (DateOnly.TryParse(value.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out result))
            {
                return true;
            }

            if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                result = DateOnly.FromDateTime(dt);
                return true;
            }

            if (DateTime.TryParse(value.Trim(), CultureInfo.CurrentCulture, DateTimeStyles.None, out dt))
            {
                result = DateOnly.FromDateTime(dt);
                return true;
            }

            return false;
        }

        private async Task<TblAdministracionEmpleado?> FindEmpleadoAsync(string codigoEmpleado, string email, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(codigoEmpleado))
            {
                var empleado = await _context.TblAdministracionEmpleados
                    .FirstOrDefaultAsync(e => e.Codigoempleado == codigoEmpleado.Trim(), cancellationToken);
                if (empleado != null) return empleado;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var empleado = await _context.TblAdministracionEmpleados
                    .FirstOrDefaultAsync(e => e.Emailcorporativo != null && e.Emailcorporativo.ToLower() == email.Trim().ToLower(), cancellationToken);
                if (empleado != null) return empleado;
            }

            return null;
        }

        private async Task<TblAdministracionEmpleado> GetOrCreateFallbackEmpleadoAsync(string codigoEmpleado, string email, string cedula, string colaborador, CancellationToken cancellationToken)
        {
            const string fallbackCodigo = "CARGA_EXCEL";
            const string fallbackEmail = "carga-excel@local";
            const string fallbackPersonaName = "Carga Excel";

            var empleadoExistente = await _context.TblAdministracionEmpleados
                .FirstOrDefaultAsync(e => e.Codigoempleado == fallbackCodigo || e.Emailcorporativo == fallbackEmail, cancellationToken);
            if (empleadoExistente != null)
            {
                return empleadoExistente;
            }

            TblAdministracionPersona persona = null;

            if (!string.IsNullOrWhiteSpace(cedula))
            {
                var cedulaNormalizada = cedula.Trim();
                persona = await _context.TblAdministracionPersonas
                    .FirstOrDefaultAsync(p => p.Numeroidentificacion == cedulaNormalizada, cancellationToken);
            }

            if (persona == null && !string.IsNullOrWhiteSpace(email))
            {
                var emailNormalizado = email.Trim().ToLowerInvariant();
                persona = await _context.TblAdministracionPersonas
                    .FirstOrDefaultAsync(p => p.Email != null && p.Email.ToLower() == emailNormalizado, cancellationToken);
            }

            if (persona == null)
            {
                var identificacionFallback = !string.IsNullOrWhiteSpace(cedula)
                    ? cedula.Trim()
                    : $"CARGAEXCEL-{Guid.NewGuid():N}".Substring(0, 20);

                persona = new TblAdministracionPersona
                {
                    Numeroidentificacion = identificacionFallback,
                    Tipopersona = "NATURAL",
                    Nombres = string.IsNullOrWhiteSpace(colaborador) ? fallbackPersonaName : colaborador.Trim(),
                    Apellidos = "Usuario",
                    Email = string.IsNullOrWhiteSpace(email) ? fallbackEmail : email.Trim(),
                    Activo = true,
                    Usuariocreacion = "carga_excel",
                    Fechacreacion = DateTime.UtcNow,
                    Ipcreacion = "127.0.0.1"
                };

                _context.TblAdministracionPersonas.Add(persona);
                await _context.SaveChangesAsync(cancellationToken);
            }

            var empleadoPorPersona = await _context.TblAdministracionEmpleados
                .FirstOrDefaultAsync(e => e.Idpersona == persona.Id, cancellationToken);
            if (empleadoPorPersona != null)
            {
                return empleadoPorPersona;
            }

            var empleado = new TblAdministracionEmpleado
            {
                Idpersona = persona.Id,
                Codigoempleado = string.IsNullOrWhiteSpace(codigoEmpleado) ? fallbackCodigo : codigoEmpleado.Trim(),
                Emailcorporativo = string.IsNullOrWhiteSpace(email) ? fallbackEmail : email.Trim(),
                Activo = true,
                Usuariocreacion = "carga_excel",
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = "127.0.0.1"
            };

            _context.TblAdministracionEmpleados.Add(empleado);
            await _context.SaveChangesAsync(cancellationToken);

            return empleado;
        }

        private async Task<int?> GetOrCreateTipoActividadIdAsync(string tipoActividad, CancellationToken cancellationToken)
        {
            const string fallbackTipo = "Carga Excel";

            if (string.IsNullOrWhiteSpace(tipoActividad))
            {
                var tipoExistente = await _context.TblTimeReportTipoActividads
                    .FirstOrDefaultAsync(t => t.Nombretipo == fallbackTipo, cancellationToken);

                if (tipoExistente != null)
                    return tipoExistente.Id;

                var nuevoTipo = new TblTimeReportTipoActividad
                {
                    Nombretipo = fallbackTipo,
                    Descripcion = "Tipo de actividad generado por carga de prueba",
                    Activo = true,
                    Usuariocreacion = "carga_excel",
                    Fechacreacion = DateTime.UtcNow,
                    Ipcreacion = "127.0.0.1"
                };

                _context.TblTimeReportTipoActividads.Add(nuevoTipo);
                await _context.SaveChangesAsync(cancellationToken);
                return nuevoTipo.Id;
            }

            var normalizedTipo = tipoActividad.Trim().ToLower();

            var tipo = await _context.TblTimeReportTipoActividads
                .FirstOrDefaultAsync(t => t.Nombretipo != null && t.Nombretipo.ToLower() == normalizedTipo, cancellationToken);

            if (tipo != null)
                return tipo.Id;

            var tipoParcial = await _context.TblTimeReportTipoActividads
                .FirstOrDefaultAsync(t => t.Nombretipo != null && t.Nombretipo.ToLower().Contains(normalizedTipo), cancellationToken);

            if (tipoParcial != null)
                return tipoParcial.Id;

            var nuevo = new TblTimeReportTipoActividad
            {
                Nombretipo = tipoActividad.Trim(),
                Descripcion = $"Tipo creado desde carga: {tipoActividad.Trim()}",
                Activo = true,
                Usuariocreacion = "carga_excel",
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = "127.0.0.1"
            };

            _context.TblTimeReportTipoActividads.Add(nuevo);
            await _context.SaveChangesAsync(cancellationToken);
            return nuevo.Id;
        }

        private async Task<int?> FindProyectoIdAsync(string proyecto, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(proyecto))
                return null;

            var proyectoNormalizado = proyecto.Trim().ToLower();
            var proyectoEntidad = await _context.TblTimeReportProyectos
                .FirstOrDefaultAsync(p =>
                    (p.Codigo != null && p.Codigo.ToLower() == proyectoNormalizado)
                    || (p.Nombre != null && p.Nombre.ToLower() == proyectoNormalizado),
                    cancellationToken);

            if (proyectoEntidad != null)
                return proyectoEntidad.Id;

            proyectoEntidad = await _context.TblTimeReportProyectos
                .FirstOrDefaultAsync(p => p.Nombre != null && p.Nombre.ToLower().Contains(proyectoNormalizado), cancellationToken);

            return proyectoEntidad?.Id;
        }
    }
}