using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Permisos.DTOs;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Features.Permisos.Endpoints;

/// <summary>
/// Listados de solicitudes con filtros, resumen y paginación en servidor para
/// Mis permisos, Aprobaciones del líder e Historial de gerencia.
/// </summary>
public static partial class PermisosEndpoints
{
    private enum VistaListadoPermisos { Colaborador, Lider, Gerencia }

    private sealed record FiltrosListadoPermisos(
        string? Buscar,
        string? Tipo,
        string? Estado,
        string? Desde,
        string? Hasta,
        string? Proyecto)
    {
        public bool FiltranEnBaseDeDatos =>
            !string.IsNullOrWhiteSpace(Buscar)
            || EsFiltroActivo(Tipo)
            || !string.IsNullOrWhiteSpace(Desde)
            || !string.IsNullOrWhiteSpace(Hasta);
    }

    private sealed record SolicitudClasificada(
        int Id,
        string EstadoClase,
        string EstadoLider,
        DateOnly FechaInicio,
        DateOnly FechaFin,
        string Proyecto);

    private static bool EsFiltroActivo(string? valor) =>
        !string.IsNullOrWhiteSpace(valor) && !string.Equals(valor.Trim(), "TODOS", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Responde el listado de una vista. Con <paramref name="page"/> devuelve una página con su resumen;
    /// sin ella, la lista completa ya filtrada (sin filtros equivale al comportamiento anterior).
    /// </summary>
    private static async Task<IResult> ResponderListadoPermisosAsync(
        ApplicationDbContext db,
        IQueryable<TblTimeReportSolicitud> alcance,
        VistaListadoPermisos vista,
        FiltrosListadoPermisos filtros,
        int? page,
        int? pageSize,
        CancellationToken ct)
    {
        var filtrado = AplicarBusquedaAmplia(alcance, filtros.Buscar);
        if (EsFiltroActivo(filtros.Tipo))
        {
            var codigoTipo = filtros.Tipo!.Trim().ToUpperInvariant();
            filtrado = filtrado.Where(s => s.TipoPermiso.Codigo == codigoTipo);
        }
        var fechaDesde = ParsearFecha(filtros.Desde);
        var fechaHasta = ParsearFecha(filtros.Hasta);
        if (fechaDesde.HasValue)
            filtrado = filtrado.Where(s => s.FechaFin >= fechaDesde.Value);
        if (fechaHasta.HasValue)
            filtrado = filtrado.Where(s => s.FechaInicio <= fechaHasta.Value);

        var clasificadas = await ClasificarSolicitudesAsync(filtrado, vista, ct);
        var resultado = clasificadas
            .Where(c => CumpleFiltroEstado(c, vista, filtros.Estado))
            .Where(c => !EsFiltroActivo(filtros.Proyecto) || c.Proyecto == filtros.Proyecto!.Trim())
            .ToList();

        var ids = resultado.Select(c => c.Id).ToList();
        if (page is null)
            return Results.Ok(ApiResponse<IReadOnlyList<PermisoItemDto>>.Ok(
                await CargarListadoAsync(db, alcance, vista, ids, ct)));

        // Mis permisos y Aprobaciones muestran indicadores globales; Gerencia los del resultado filtrado.
        IReadOnlyList<SolicitudClasificada> paraResumen = vista == VistaListadoPermisos.Gerencia
            ? resultado
            : filtros.FiltranEnBaseDeDatos ? await ClasificarSolicitudesAsync(alcance, vista, ct) : clasificadas;

        var (pagina, tamano) = NormalizarPaginacion(page, pageSize);
        var idsPagina = ids.Skip((pagina - 1) * tamano).Take(tamano).ToList();
        var items = await CargarListadoAsync(db, alcance, vista, idsPagina, ct);
        var meta = new PaginationMeta(pagina, tamano, ids.Count);
        var dto = new PaginaPermisosDto(items, pagina, tamano, ids.Count, meta.TotalPages, CalcularResumenListado(paraResumen));
        return Results.Ok(ApiResponse<PaginaPermisosDto>.Ok(dto, meta: meta));
    }

    private static bool CumpleFiltroEstado(SolicitudClasificada c, VistaListadoPermisos vista, string? estado)
    {
        if (!EsFiltroActivo(estado)) return true;
        var enProceso = string.Equals(estado!.Trim(), "proceso", StringComparison.OrdinalIgnoreCase);
        var finalizado = string.Equals(estado.Trim(), "finalizado", StringComparison.OrdinalIgnoreCase);
        if (!enProceso && !finalizado) return true;

        return vista switch
        {
            VistaListadoPermisos.Lider => enProceso == (c.EstadoLider == "Pendiente"),
            VistaListadoPermisos.Colaborador => enProceso
                ? c.EstadoClase is "pendiente" or "rrhh"
                : c.EstadoClase is "confirmado" or "observado" or "rechazado",
            _ => enProceso
                ? c.EstadoClase is "pendiente" or "rrhh" or "observado"
                : c.EstadoClase is "confirmado" or "rechazado",
        };
    }

    private static ResumenListadoPermisosDto CalcularResumenListado(IReadOnlyList<SolicitudClasificada> lista)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        return new ResumenListadoPermisosDto(
            Total: lista.Count,
            EnProceso: lista.Count(c => c.EstadoClase is "pendiente" or "rrhh" or "observado"),
            Confirmados: lista.Count(c => c.EstadoClase == "confirmado"),
            Rechazados: lista.Count(c => c.EstadoClase == "rechazado"),
            PendientesLider: lista.Count(c => c.EstadoLider == "Pendiente"),
            AprobadosLider: lista.Count(c => c.EstadoLider == "Aprobado"),
            AusentesHoy: lista.Count(c => c.EstadoLider == "Aprobado" && c.FechaInicio <= hoy && c.FechaFin >= hoy),
            Proyectos: lista.Select(c => c.Proyecto)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .Order(StringComparer.CurrentCultureIgnoreCase)
                .ToList());
    }

    /// <summary>Consulta ligera (estado, revisiones, líder y proyecto) en el orden de cada vista.</summary>
    private static async Task<List<SolicitudClasificada>> ClasificarSolicitudesAsync(
        IQueryable<TblTimeReportSolicitud> query,
        VistaListadoPermisos vista,
        CancellationToken ct)
    {
        var ordenada = vista == VistaListadoPermisos.Gerencia
            ? query.OrderByDescending(s => s.FechaInicio).ThenByDescending(s => s.FechaCreacion).ThenByDescending(s => s.Id)
            : query.OrderByDescending(s => s.FechaCreacion).ThenByDescending(s => s.Id);

        var entidades = await ConsultarClasificacionBandeja(ordenada)
            .Include(s => s.Proyecto)
            .Include(s => s.AsignacionProyecto)
                .ThenInclude(a => a!.IdproyectoNavigation)
            .ToListAsync(ct);

        return entidades.Select(s =>
        {
            var liderEfectivo = s.Lider ?? s.AsignacionProyecto?.IdliderNavigation;
            var (revLider, revTh) = ResolverRevisionesVigentes(s);
            var aprobadoLider = revLider?.Decision == "APROBADO" || s.Estado.Codigo is "PENDIENTE_TH" or "APROBADA";
            var confirmadoRrhh = revTh?.Decision == "APROBADO" || s.Estado.Codigo == "APROBADA";
            var estados = ResolverEstados(
                s.Estado.Codigo,
                aprobadoLider,
                confirmadoRrhh,
                EsLiderExterno(liderEfectivo),
                revLider?.Decision,
                revTh?.Decision);
            var proyecto = (s.Proyecto ?? s.AsignacionProyecto?.IdproyectoNavigation)?.Nombre ?? "General ISC";
            return new SolicitudClasificada(s.Id, estados.EstadoClase, estados.EstadoLider, s.FechaInicio, s.FechaFin, proyecto);
        }).ToList();
    }

    /// <summary>Detalle completo de los IDs indicados; en la bandeja del líder incluye la alerta de traslape.</summary>
    private static async Task<IReadOnlyList<PermisoItemDto>> CargarListadoAsync(
        ApplicationDbContext db,
        IQueryable<TblTimeReportSolicitud> alcance,
        VistaListadoPermisos vista,
        IReadOnlyList<int> ids,
        CancellationToken ct)
    {
        if (vista != VistaListadoPermisos.Lider || ids.Count == 0)
            return await CargarSolicitudesPorIdsAsync(db, ids, ct);

        var entidades = await ConsultarSolicitudesBase(db)
            .Where(s => ids.Contains(s.Id))
            .AsSplitQuery()
            .ToListAsync(ct);

        // El traslape se busca en todo el equipo visible, no solo en la página: se cargan únicamente
        // las solicitudes no rechazadas de los mismos proyectos que se cruzan con el rango de fechas.
        var proyectos = entidades
            .Select(s => s.IdProyecto ?? s.AsignacionProyecto?.Idproyecto)
            .OfType<int>()
            .Distinct()
            .ToList();
        List<TblTimeReportSolicitud> candidatos = [];
        if (proyectos.Count > 0)
        {
            var minInicio = entidades.Min(s => s.FechaInicio);
            var maxFin = entidades.Max(s => s.FechaFin);
            candidatos = await alcance
                .Where(s => s.Estado.Codigo != "RECHAZADA" && s.FechaInicio <= maxFin && s.FechaFin >= minInicio)
                .Where(s => (s.IdProyecto != null && proyectos.Contains(s.IdProyecto.Value))
                            || (s.IdProyecto == null && s.AsignacionProyecto != null && proyectos.Contains(s.AsignacionProyecto.Idproyecto)))
                .Include(s => s.Empleado)
                    .ThenInclude(e => e.IdpersonaNavigation)
                .Include(s => s.Estado)
                .Include(s => s.AsignacionProyecto)
                .OrderByDescending(s => s.FechaCreacion)
                .ToListAsync(ct);
        }

        var porId = entidades.ToDictionary(s => s.Id);
        return ids.Where(porId.ContainsKey)
            .Select(id => MapearSolicitudDto(porId[id], CalcularAlertaTraslape(porId[id], candidatos)))
            .ToList();
    }

    /// <summary>
    /// Búsqueda por palabras (todas deben coincidir, sin distinguir tildes) en colaborador, cédula,
    /// códigos, motivo, tipo, proyecto, cliente y líder.
    /// </summary>
    private static IQueryable<TblTimeReportSolicitud> AplicarBusquedaAmplia(
        IQueryable<TblTimeReportSolicitud> query,
        string? buscar)
    {
        const RegexOptions ci = RegexOptions.IgnoreCase;
        foreach (var patron in PatronesBusqueda(buscar))
        {
            query = query.Where(s =>
                Regex.IsMatch(s.Empleado.IdpersonaNavigation.Apellidos + " " + s.Empleado.IdpersonaNavigation.Nombres, patron, ci)
                || Regex.IsMatch(s.Empleado.IdpersonaNavigation.Nombres + " " + s.Empleado.IdpersonaNavigation.Apellidos, patron, ci)
                || Regex.IsMatch(s.Empleado.IdpersonaNavigation.Numeroidentificacion, patron, ci)
                || Regex.IsMatch(s.Codigo, patron, ci)
                || Regex.IsMatch(s.Motivo, patron, ci)
                || Regex.IsMatch(s.TipoPermiso.Nombre, patron, ci)
                || Regex.IsMatch(s.Estado.Nombre, patron, ci)
                || (s.Proyecto != null && Regex.IsMatch(s.Proyecto.Nombre, patron, ci))
                || (s.AsignacionProyecto != null && Regex.IsMatch(s.AsignacionProyecto.IdproyectoNavigation.Nombre, patron, ci))
                || (s.Cliente != null && s.Cliente.Nombrecomercial != null && Regex.IsMatch(s.Cliente.Nombrecomercial, patron, ci))
                || (s.Cliente != null && s.Cliente.Razonsocial != null && Regex.IsMatch(s.Cliente.Razonsocial, patron, ci))
                || (s.Lider != null && s.Lider.IdpersonaNavigation != null
                    && Regex.IsMatch(s.Lider.IdpersonaNavigation.Nombres + " " + s.Lider.IdpersonaNavigation.Apellidos, patron, ci)));
        }
        return query;
    }
}
