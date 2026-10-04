using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Dashboard.DTOs;
using tmr_backend.Features.Dashboard.Services;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Dashboard;

/// <summary>
/// sm - Dashboard ejecutivo (Requerimiento_Funcional_Dashboard_Time_Report).
/// Todos los indicadores, gráficos y detalles usan la misma selección de filtros (RF 01) y la misma fecha de corte:
/// fin del mes elegido, o hoy (hora de Ecuador) si es el mes en curso.
/// </summary>
public static class DashboardEjecutivoEndpoints
{
    private const string CatalogoEstadosProyecto = "EPR";

    // sm - Selección de filtros globales (RF 01). Año y mes obligatorios; el resto opcional.
    private sealed record Filtros(int? IdCliente, int? IdProyecto, int? IdEstado, int? IdEmpleado)
    {
        public bool FiltraProyectos => IdCliente.HasValue || IdProyecto.HasValue || IdEstado.HasValue;
    }

    // sm - Datos base cargados una sola vez por petición.
    private sealed class DatosBase
    {
        public required List<TblTimeReportProyecto> Proyectos { get; init; }
        public required List<TblTimeReportAsignacionProyecto> Asignaciones { get; init; }
        public required List<TblAdministracionEmpleado> Empleados { get; init; }
        public required Dictionary<int, TblTimeReportProyecto> ProyectosPorId { get; init; }
        public required Dictionary<int, TblAdministracionEmpleado> EmpleadosPorId { get; init; }

        // sm - Asignaciones de personas que ya fueron retiradas de un proyecto (reconstruidas del historial).
        public required List<AsignacionCalculo> AsignacionesRetiradas { get; init; }

        // sm - Historial de tipo de contrato por empleado, ya convertido a jornada (8 h / 6 h pasante).
        public required ILookup<int, PeriodoJornada> JornadasPorEmpleado { get; init; }

        // sm - Optimización: antes estas listas se volvían a armar cada vez que se usaban y varias búsquedas recorrían
        // todas las asignaciones por cada fila. Ahora se arman una sola vez por petición y se indexan (mismo resultado).
        private List<EmpleadoCalculo>? empleadosCalculo;
        private List<AsignacionCalculo>? asignacionesCalculo;
        private ILookup<int, AsignacionCalculo>? asignacionesCalculoPorEmpleado;
        private ILookup<(int IdEmpleado, int IdProyecto), AsignacionCalculo>? vigenciasPorPar;
        private ILookup<int, TblTimeReportAsignacionProyecto>? asignacionesPorEmpleado;
        private ILookup<int, TblTimeReportAsignacionProyecto>? asignacionesPorProyecto;

        public List<EmpleadoCalculo> EmpleadosCalculo => empleadosCalculo ??= Empleados
            .Select(e => new EmpleadoCalculo(e.Id, e.Fechaingreso, e.Fechaterminacion, CalculoHorasPeriodo.HorasJornada(e),
                JornadasPorEmpleado[e.Id].ToList()))
            .ToList();

        // sm - Vigencias para el cálculo de horas: asignaciones actuales + retiradas (para meses pasados).
        public List<AsignacionCalculo> AsignacionesCalculo => asignacionesCalculo ??= Asignaciones
            .Where(a => a.Idempleado.HasValue && ProyectosPorId.ContainsKey(a.Idproyecto))
            .Select(a => new AsignacionCalculo(a.Idempleado!.Value, a.Idproyecto, a.Fechaasignacion, a.Fechafinasignacion))
            .Concat(AsignacionesRetiradas.Where(a => ProyectosPorId.ContainsKey(a.IdProyecto)))
            .ToList();

        public ILookup<int, AsignacionCalculo> AsignacionesCalculoPorEmpleado =>
            asignacionesCalculoPorEmpleado ??= AsignacionesCalculo.ToLookup(a => a.IdEmpleado);

        public ILookup<(int IdEmpleado, int IdProyecto), AsignacionCalculo> VigenciasPorPar =>
            vigenciasPorPar ??= AsignacionesCalculo.ToLookup(a => (a.IdEmpleado, a.IdProyecto));

        // sm - Asignaciones activas (entidades) por empleado y por proyecto.
        public ILookup<int, TblTimeReportAsignacionProyecto> AsignacionesPorEmpleado =>
            asignacionesPorEmpleado ??= Asignaciones.Where(a => a.Idempleado.HasValue).ToLookup(a => a.Idempleado!.Value);

        public ILookup<int, TblTimeReportAsignacionProyecto> AsignacionesPorProyecto =>
            asignacionesPorProyecto ??= Asignaciones.ToLookup(a => a.Idproyecto);
    }

    public static void MapDashboardEjecutivoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireAuthorization(); // sm - JWT obligatorio en todos los endpoints del dashboard ejecutivo

        group.MapGet("/ejecutivo/filtros", ObtenerFiltros);
        group.MapGet("/ejecutivo", ObtenerDashboard);
        group.MapGet("/ejecutivo/historico", ObtenerHistorico);
    }

    // =====================================================================
    // Filtros
    // =====================================================================
    private static async Task<IResult> ObtenerFiltros(ApplicationDbContext db)
    {
        var clientes = await db.TblAdministracionClientes
            .AsNoTracking()
            .Where(c => c.Activo)
            .Select(c => new OpcionFiltroDto(c.Id, c.Nombrecomercial ?? c.Razonsocial ?? (c.Nombres + " " + c.Apellidos)))
            .ToListAsync();

        var proyectos = await db.TblTimeReportProyectos
            .AsNoTracking()
            .Where(p => p.Activo)
            .Select(p => new OpcionProyectoFiltroDto(p.Id, p.Nombre, p.Codigo ?? "", p.Idcliente, p.Idestadoproyecto))
            .ToListAsync();

        var estados = await db.Set<TblAdministracionCatalogoDetalle>()
            .AsNoTracking()
            .Where(d => d.Activo && d.IdcatalogoNavigation.Codigo == CatalogoEstadosProyecto)
            .OrderBy(d => d.Orden)
            .Select(d => new OpcionFiltroDto(d.Id, d.Valor))
            .ToListAsync();

        var colaboradores = await db.TblAdministracionEmpleados
            .AsNoTracking()
            .Where(e => e.Activo || e.Fechaterminacion != null)
            .Select(e => new OpcionFiltroDto(e.Id, e.IdpersonaNavigation.Nombres + " " + e.IdpersonaNavigation.Apellidos))
            .ToListAsync();

        return Results.Ok(new DashboardFiltrosResponse(
            clientes.OrderBy(c => c.Nombre).ToList(),
            proyectos.OrderBy(p => p.Nombre).ToList(),
            estados,
            colaboradores.OrderBy(c => c.Nombre).ToList()));
    }

    // =====================================================================
    // Dashboard del periodo
    // =====================================================================
    private static async Task<IResult> ObtenerDashboard(
        int? anio, int? mes, int? idCliente, int? idProyecto, int? idEstado, int? idEmpleado, int? horizonte,
        ApplicationDbContext db)
    {
        var hoy = CalculoHorasPeriodo.HoyEcuador();
        var anioSel = anio ?? hoy.Year;
        var mesSel = mes ?? hoy.Month;
        if (mesSel is < 1 or > 12 || anioSel is < 2000 or > 2100)
            return Results.BadRequest(new { Mensaje = "Periodo inválido" });

        var horizonteDias = Math.Clamp(horizonte ?? ParametrosDashboard.HorizontePorDefecto, 1, 365);
        var filtros = new Filtros(idCliente, idProyecto, idEstado, idEmpleado);

        var inicioMes = new DateOnly(anioSel, mesSel, 1);
        var finMes = inicioMes.AddMonths(1).AddDays(-1);
        // sm - Fecha de corte (confirmado 2026-10-02): fin del mes elegido o, si es el mes en curso, AYER (el día de hoy
        // aún no termina y no debe aparecer como pendiente). CA 01: la misma para todo el dashboard.
        // El primer día del mes el corte queda en ese día, pero no se esperan horas (horasHasta < inicioMes).
        var ayer = hoy.AddDays(-1);
        var corte = finMes <= ayer ? finMes : (inicioMes > ayer ? inicioMes : ayer);
        var horasHasta = corte < ayer ? corte : ayer;

        var datos = await CargarDatosBaseAsync(db);
        var proyectoCumpleFiltro = CrearFiltroProyecto(filtros, datos);

        // ── Proyectos y sus categorías (RF 04 a RF 07, RF 10, RF 11) ──
        var proyectosDto = new List<ProyectoDashboardDto>();
        foreach (var p in datos.Proyectos.Where(p => proyectoCumpleFiltro(p.Id)))
        {
            var codigoEstado = CodigoEstado(p);
            var cerrado = ParametrosDashboard.EstadosCerrados.Contains(codigoEstado);
            var inicio = p.Fechainicioreal ?? p.Fechainicioplaneada;
            var termino = p.Fechafinplaneada;

            var categorias = new List<string>();
            if (ParametrosDashboard.EstadosActivos.Contains(codigoEstado)
                && (inicio == null || inicio <= corte)
                && (termino == null || termino >= corte))
                categorias.Add("Activo");
            // sm - RF 04 / CA 08: no cerrado y con término anterior a la fecha de corte.
            if (!cerrado && termino.HasValue && termino.Value < corte) categorias.Add("Vencido");
            // sm - RF 05 / CA 09: termina entre la fecha de corte y el horizonte; nunca a la vez que Vencido.
            if (!cerrado && termino.HasValue && termino.Value >= corte && termino.Value <= corte.AddDays(horizonteDias))
                categorias.Add("Proximo");
            // sm - RF 07: inicio (real o planeado) dentro del periodo.
            if (inicio.HasValue && inicio.Value >= inicioMes && inicio.Value <= finMes) categorias.Add("Nuevo");
            // sm - RF 06: cierre efectivo (fecha fin real) dentro del periodo. No se usa la fecha de actualización.
            if (cerrado && p.Fechafinreal.HasValue && p.Fechafinreal.Value >= inicioMes && p.Fechafinreal.Value <= finMes)
                categorias.Add("Cerrado");

            proyectosDto.Add(new ProyectoDashboardDto(
                p.Id,
                p.Codigo ?? "",
                p.Nombre,
                p.Idcliente,
                NombreCliente(p),
                ResponsablesProyecto(p.Id, datos),
                p.IdestadoproyectoNavigation?.Valor ?? "",
                inicio,
                termino,
                p.Fechafinreal,
                categorias.Contains("Vencido") ? corte.DayNumber - termino!.Value.DayNumber : null,
                categorias.Contains("Proximo") ? termino!.Value.DayNumber - corte.DayNumber : null,
                categorias));
        }

        // ── Colaboradores ──
        var empleadoCumpleFiltro = CrearFiltroEmpleado(filtros, datos, proyectoCumpleFiltro);

        // sm - RF 02: activos a la fecha de corte sin asignación vigente a un proyecto no cerrado.
        // Solo aplica el filtro de colaborador (por definición no tienen proyecto, cliente ni estado).
        var sinProyecto = new List<ColaboradorSinProyectoDto>();
        var asignacionesCalculo = datos.AsignacionesCalculo;
        foreach (var e in datos.Empleados.Where(e => EstaActivoEn(e, corte)
                                                    && (!filtros.IdEmpleado.HasValue || e.Id == filtros.IdEmpleado)))
        {
            var asignacionesEmpleado = datos.AsignacionesPorEmpleado[e.Id].ToList();
            // sm - Incluye las asignaciones retiradas: en un mes pasado la persona sí pudo estar asignada.
            var tieneVigente = datos.AsignacionesCalculoPorEmpleado[e.Id].Any(a =>
                (a.Desde ?? DateOnly.MinValue) <= corte
                && (a.Hasta ?? DateOnly.MaxValue) >= corte
                && datos.ProyectosPorId.TryGetValue(a.IdProyecto, out var p)
                && !ParametrosDashboard.EstadosCerrados.Contains(CodigoEstado(p)));
            if (tieneVigente) continue;

            var ultima = asignacionesEmpleado
                .Where(a => (a.Fechaasignacion ?? DateOnly.MinValue) <= corte)
                .OrderByDescending(a => FinEfectivoAsignacion(a, datos) ?? DateOnly.MaxValue)
                .FirstOrDefault();
            var finUltima = ultima is null ? null : FinEfectivoAsignacion(ultima, datos);
            var referencia = finUltima.HasValue && finUltima.Value <= corte ? finUltima : e.Fechaingreso;

            sinProyecto.Add(new ColaboradorSinProyectoDto(
                e.Id,
                NombreEmpleado(e),
                e.IdpersonaNavigation?.Numeroidentificacion ?? "",
                e.IdcargoNavigation?.Nombrecargo ?? "",
                ultima?.IdliderNavigation is { } lider ? NombreLider(lider) : "",
                e.Fechaingreso,
                ultima is not null && datos.ProyectosPorId.TryGetValue(ultima.Idproyecto, out var pu) ? pu.Nombre : "",
                referencia.HasValue ? Math.Max(0, corte.DayNumber - referencia.Value.DayNumber) : null));
        }

        // sm - RF 03: ingresos y salidas con fecha efectiva dentro del periodo.
        var ingresos = datos.Empleados
            .Where(e => e.Fechaingreso >= inicioMes && e.Fechaingreso <= finMes && empleadoCumpleFiltro(e.Id))
            .Select(e => Movimiento(e, e.Fechaingreso!.Value, datos))
            .OrderBy(m => m.Fecha).ToList();
        var salidas = datos.Empleados
            .Where(e => e.Fechaterminacion >= inicioMes && e.Fechaterminacion <= finMes && empleadoCumpleFiltro(e.Id))
            .Select(e => Movimiento(e, e.Fechaterminacion!.Value, datos))
            .OrderBy(m => m.Fecha).ToList();

        // sm - RF 08 / CA 10: asignaciones activas de personas cuya salida ya ocurrió, en proyectos no cerrados.
        var desvinculados = new List<AsignacionDesvinculadaDto>();
        foreach (var a in datos.Asignaciones.Where(a => a.Idempleado.HasValue))
        {
            if (!datos.EmpleadosPorId.TryGetValue(a.Idempleado!.Value, out var e) || !e.Fechaterminacion.HasValue) continue;
            if (e.Fechaterminacion.Value >= corte) continue;
            if (a.Fechafinasignacion.HasValue && a.Fechafinasignacion.Value <= e.Fechaterminacion.Value) continue;
            if (!datos.ProyectosPorId.TryGetValue(a.Idproyecto, out var p)) continue;
            if (ParametrosDashboard.EstadosCerrados.Contains(CodigoEstado(p))) continue;
            if (!proyectoCumpleFiltro(p.Id) || (filtros.IdEmpleado.HasValue && e.Id != filtros.IdEmpleado)) continue;

            desvinculados.Add(new AsignacionDesvinculadaDto(
                p.Id, p.Codigo ?? "", p.Nombre, NombreCliente(p), e.Id, NombreEmpleado(e),
                e.Fechaterminacion.Value, a.Fechaasignacion, a.Fechafinasignacion, a.Rolasignado ?? ""));
        }

        // ── Cumplimiento (RF 12, RF 13, sección 5) ──
        var feriados = await CargarFeriadosAsync(db, inicioMes, finMes);
        var actividades = await CargarActividadesAsync(db, inicioMes, corte);
        var filas = CalculoCumplimiento
            .Calcular(datos.EmpleadosCalculo, asignacionesCalculo, actividades, feriados, inicioMes, horasHasta)
            .Where(f => proyectoCumpleFiltro(f.IdProyecto) && (!filtros.IdEmpleado.HasValue || f.IdEmpleado == filtros.IdEmpleado))
            .ToList();

        // sm - Horas registradas que NO entran al cumplimiento: en proyectos donde la persona no tiene asignación
        // vigente en el periodo, o sin proyecto. Se muestran para explicar diferencias con Seguimiento.
        var paresConAsignacion = filas.Select(f => (f.IdEmpleado, f.IdProyecto)).ToHashSet();
        // sm - Sin la restricción "proyectos donde el colaborador está asignado": justamente se buscan los que no lo están.
        var proyectoSinFiltroEmpleado = CrearFiltroProyecto(filtros with { IdEmpleado = null }, datos);
        var fueraDeAsignacion = actividades
            .Where(a => !a.EsNovedad
                        && datos.EmpleadosPorId.ContainsKey(a.IdEmpleado)
                        && (!filtros.IdEmpleado.HasValue || a.IdEmpleado == filtros.IdEmpleado)
                        && (a.IdProyecto.HasValue
                            ? proyectoSinFiltroEmpleado(a.IdProyecto.Value) && !paresConAsignacion.Contains((a.IdEmpleado, a.IdProyecto.Value))
                            : !filtros.FiltraProyectos))
            .GroupBy(a => (a.IdEmpleado, a.IdProyecto))
            .Select(g =>
            {
                var e = datos.EmpleadosPorId[g.Key.IdEmpleado];
                var p = g.Key.IdProyecto.HasValue && datos.ProyectosPorId.TryGetValue(g.Key.IdProyecto.Value, out var pr) ? pr : null;
                return new RegistroFueraDeAsignacionDto(
                    e.Id, NombreEmpleado(e), g.Key.IdProyecto,
                    p?.Nombre ?? "Sin proyecto", p is null ? "" : NombreCliente(p),
                    g.Sum(a => a.Horas), g.Max(a => a.Fecha));
            })
            .OrderByDescending(r => r.Horas)
            .ToList();

        var ultimosRegistros = await db.TblTimeReportActividadDiaria
            .AsNoTracking()
            .Where(a => a.Activo && a.Idproyecto != null && a.Fechaactividad <= corte)
            .GroupBy(a => new { a.Idempleado, Idproyecto = a.Idproyecto!.Value })
            .Select(g => new { g.Key.Idempleado, g.Key.Idproyecto, Fecha = g.Max(a => a.Fechaactividad) })
            .ToListAsync();
        var ultimoRegistroPorFila = ultimosRegistros.ToDictionary(u => (u.Idempleado, u.Idproyecto), u => u.Fecha);

        var detalle = filas.Select(f =>
        {
            var e = datos.EmpleadosPorId[f.IdEmpleado];
            var p = datos.ProyectosPorId[f.IdProyecto];
            // sm - Se redondea cada fila antes de sumar para que el detalle cuadre exacto con el total del cliente (CA 06).
            var esperadas = Math.Round(f.Esperadas, 2);
            var registradas = Math.Round(f.Registradas, 2);
            var reportadas = Math.Min(registradas, esperadas);
            var pendientes = Math.Max(0m, esperadas - registradas);
            var porcentaje = Porcentaje(reportadas, esperadas);
            var vigencia = datos.VigenciasPorPar[(f.IdEmpleado, f.IdProyecto)].ToList();
            ultimoRegistroPorFila.TryGetValue((f.IdEmpleado, f.IdProyecto), out var ultimo);

            return new CumplimientoDetalleDto(
                e.Id, NombreEmpleado(e), e.Emailcorporativo ?? e.IdpersonaNavigation?.Email ?? "",
                p.Id, p.Codigo ?? "", p.Nombre, p.Idcliente ?? 0, NombreCliente(p),
                vigencia.Min(a => a.Desde),
                vigencia.Any(a => a.Hasta == null) ? null : vigencia.Max(a => a.Hasta),
                esperadas, registradas, reportadas, pendientes, porcentaje,
                ParametrosDashboard.Semaforo(porcentaje),
                ultimo == default ? null : ultimo,
                pendientes > 0 ? "Incompleto" : "Completo");
        }).ToList();

        var cumplimientoClientes = detalle
            .GroupBy(d => new { d.IdCliente, d.Cliente })
            .Select(g =>
            {
                var esperadas = g.Sum(d => d.Esperadas);
                var reportadas = g.Sum(d => d.Reportadas);
                var porcentaje = Porcentaje(reportadas, esperadas);
                return new CumplimientoClienteDto(
                    g.Key.IdCliente, g.Key.Cliente, esperadas, reportadas, esperadas - reportadas, porcentaje,
                    ParametrosDashboard.Semaforo(porcentaje),
                    g.Select(d => d.IdEmpleado).Distinct().Count(),
                    g.Where(d => d.Pendientes > 0).Select(d => d.IdEmpleado).Distinct().Count());
            })
            // sm - Orden por criticidad: menor cumplimiento primero y, a igual %, mayor brecha.
            .OrderBy(c => c.Porcentaje).ThenByDescending(c => c.Pendientes)
            .ToList();

        var totalEsperadas = detalle.Sum(d => d.Esperadas);
        var totalReportadas = detalle.Sum(d => d.Reportadas);
        var porcentajeTotal = Porcentaje(totalReportadas, totalEsperadas);
        var cumplimientoTotal = new CumplimientoTotalesDto(
            totalEsperadas, totalReportadas, totalEsperadas - totalReportadas, porcentajeTotal,
            ParametrosDashboard.Semaforo(porcentajeTotal));

        // ── Tarjetas ──
        int Contar(string categoria) => proyectosDto.Count(p => p.Categorias.Contains(categoria));
        var tarjetas = new DashboardTarjetasDto(
            sinProyecto.Count,
            ingresos.Count,
            salidas.Count,
            // sm - RF 09: cada cliente cuenta una sola vez si tiene al menos un proyecto activo.
            proyectosDto.Where(p => p.Categorias.Contains("Activo") && p.IdCliente.HasValue)
                .Select(p => p.IdCliente).Distinct().Count(),
            Contar("Activo"),
            Contar("Vencido"),
            Contar("Proximo"),
            Contar("Nuevo"),
            Contar("Cerrado"),
            desvinculados.Select(d => d.IdProyecto).Distinct().Count());

        // ── Trazabilidad (RF 16) ──
        var ultimoRegistro = await db.TblTimeReportActividadDiaria
            .AsNoTracking()
            .MaxAsync(a => (DateTime?)(a.Fechamodificacion ?? a.Fechacreacion));

        var periodo = new DashboardPeriodoDto(
            anioSel, mesSel, inicioMes, finMes, corte,
            AHoraEcuador(DateTime.UtcNow),
            ultimoRegistro.HasValue ? AHoraEcuador(ultimoRegistro.Value) : null);

        return Results.Ok(new DashboardEjecutivoResponse(
            periodo,
            await ParametrosAsync(db, horizonteDias),
            tarjetas,
            sinProyecto.OrderByDescending(s => s.DiasSinAsignacion ?? 0).ToList(),
            ingresos,
            salidas,
            proyectosDto.OrderBy(p => p.FechaTermino ?? DateOnly.MaxValue).ToList(),
            desvinculados.OrderBy(d => d.Proyecto).ThenBy(d => d.Colaborador).ToList(),
            cumplimientoTotal,
            cumplimientoClientes,
            detalle.OrderByDescending(d => d.Pendientes).ThenBy(d => d.Colaborador).ToList(),
            fueraDeAsignacion));
    }

    // =====================================================================
    // Histórico y recurrencia (RF 15)
    // =====================================================================
    private static async Task<IResult> ObtenerHistorico(
        int? anio, int? mes, int? meses, int? umbralRecurrencia,
        int? idCliente, int? idProyecto, int? idEstado, int? idEmpleado,
        ApplicationDbContext db)
    {
        var hoy = CalculoHorasPeriodo.HoyEcuador();
        var anioSel = anio ?? hoy.Year;
        var mesSel = mes ?? hoy.Month;
        if (mesSel is < 1 or > 12 || anioSel is < 2000 or > 2100)
            return Results.BadRequest(new { Mensaje = "Periodo inválido" });

        // sm - CA 11: al menos 6 y 12 meses.
        var mesesVentana = meses == 12 ? 12 : 6;
        // sm - El umbral es de ocasiones (cortes) con atraso: hay dos cortes por mes en la ventana.
        var umbral = Math.Clamp(umbralRecurrencia ?? ParametrosDashboard.UmbralRecurrenciaPorDefecto, 1, mesesVentana * 2);
        var filtros = new Filtros(idCliente, idProyecto, idEstado, idEmpleado);

        var ultimoMes = new DateOnly(anioSel, mesSel, 1);
        var primerMes = ultimoMes.AddMonths(-(mesesVentana - 1));
        var finVentana = ultimoMes.AddMonths(1).AddDays(-1);
        var finDatos = finVentana < hoy ? finVentana : hoy;

        var datos = await CargarDatosBaseAsync(db);
        var proyectoCumpleFiltro = CrearFiltroProyecto(filtros, datos);
        var feriados = await CargarFeriadosAsync(db, primerMes, finVentana);
        var actividades = await CargarActividadesAsync(db, primerMes, finDatos);
        var empleadosCalculo = datos.EmpleadosCalculo;
        var asignacionesCalculo = datos.AsignacionesCalculo;
        var empleadoCalculoPorId = empleadosCalculo.ToDictionary(e => e.Id);
        var asignacionesPorEmpleado = datos.AsignacionesCalculoPorEmpleado;
        var actividadesPorEmpleado = actividades.ToLookup(a => a.IdEmpleado);

        var resumenMeses = new List<HistoricoMesDto>();
        var estadosPorEmpleado = new Dictionary<int, List<HistoricoEstadoMesDto>>();
        var cortesPorEmpleado = new Dictionary<int, List<HistoricoCorteDto>>();

        // sm - Las horas esperadas se cuentan hasta ayer (el día de hoy aún no termina).
        var ayer = hoy.AddDays(-1);
        for (var inicio = primerMes; inicio <= ultimoMes; inicio = inicio.AddMonths(1))
        {
            if (inicio > ayer) break;
            var fin = inicio.AddMonths(1).AddDays(-1);
            var hasta = fin < ayer ? fin : ayer;

            // sm - Cierre del mes: regla fija, último día hábil del mes (confirmado por la usuaria, sin tabla de cierres).
            var fechaCierre = ParametrosDashboard.UltimoDiaHabil(inicio.Year, inicio.Month, feriados);
            var mesAbierto = hoy <= fechaCierre;

            // sm - Cortes de quincena del mes: (quincena, desde, hasta, fecha de corte). Solo se evalúan los cortes ya
            // pasados (hoy > fecha de corte); lo registrado después del corte no lo cubre.
            var corteQuincena = ParametrosDashboard.CorteQuincena(inicio.Year, inicio.Month, feriados);
            var cortesMes = new[]
                {
                    (Quincena: 1, Desde: inicio, Hasta: new DateOnly(inicio.Year, inicio.Month, 15), Fecha: corteQuincena),
                    (Quincena: 2, Desde: new DateOnly(inicio.Year, inicio.Month, 16), Hasta: fin, Fecha: fechaCierre)
                }
                .Where(c => hoy > c.Fecha)
                .ToList();

            bool CumpleFiltro(FilaCumplimiento f) =>
                proyectoCumpleFiltro(f.IdProyecto) && (!filtros.IdEmpleado.HasValue || f.IdEmpleado == filtros.IdEmpleado);

            var filasActual = CalculoCumplimiento
                .Calcular(empleadosCalculo, asignacionesCalculo, actividades, feriados, inicio, hasta)
                .Where(CumpleFiltro).ToList();
            // sm - Lo registrado hasta el cierre: si faltaba algo al cierre y hoy está completo, es regularización.
            var filasAlCierre = mesAbierto
                ? filasActual
                : CalculoCumplimiento
                    .Calcular(empleadosCalculo, asignacionesCalculo, actividades, feriados, inicio, hasta, fechaCierre)
                    .Where(CumpleFiltro).ToList();

            var pendientesAlCierre = filasAlCierre
                .GroupBy(f => f.IdEmpleado)
                .ToDictionary(g => g.Key, g => g.Sum(f => Math.Round(f.Pendientes, 2)));

            int cumplidos = 0, atrasosCarga = 0, incumplidos = 0, regularizados = 0;
            foreach (var grupo in filasActual.GroupBy(f => f.IdEmpleado))
            {
                var esperadas = grupo.Sum(f => Math.Round(f.Esperadas, 2));
                if (esperadas == 0m) continue;
                var pendientes = grupo.Sum(f => Math.Round(f.Pendientes, 2));
                var reportadas = esperadas - pendientes;
                pendientesAlCierre.TryGetValue(grupo.Key, out var pendienteCierre);

                var estado = pendientes > 0 ? (mesAbierto ? "AtrasoCarga" : "Incumplido")
                    : pendienteCierre > 0 ? "Regularizado"
                    : "Cumplido";
                switch (estado)
                {
                    case "Cumplido": cumplidos++; break;
                    case "AtrasoCarga": atrasosCarga++; break;
                    case "Incumplido": incumplidos++; break;
                    default: regularizados++; break;
                }

                if (!estadosPorEmpleado.TryGetValue(grupo.Key, out var lista))
                    estadosPorEmpleado[grupo.Key] = lista = [];
                lista.Add(new HistoricoEstadoMesDto(
                    inicio.Year, inicio.Month, esperadas, pendientes, pendienteCierre,
                    Porcentaje(reportadas, esperadas), estado));

                if (!empleadoCalculoPorId.TryGetValue(grupo.Key, out var empleadoCalculo)) continue;
                if (!cortesPorEmpleado.TryGetValue(grupo.Key, out var cortes))
                    cortesPorEmpleado[grupo.Key] = cortes = [];
                foreach (var c in cortesMes)
                {
                    var diasIncompletos = CalculoCumplimiento.DiasIncompletos(
                        empleadoCalculo, asignacionesPorEmpleado[grupo.Key], actividadesPorEmpleado[grupo.Key],
                        feriados, c.Desde, c.Hasta, c.Fecha);
                    cortes.Add(new HistoricoCorteDto(
                        inicio.Year, inicio.Month, c.Quincena, c.Fecha, diasIncompletos,
                        diasIncompletos > ParametrosDashboard.MaxDiasIncompletosPorCorte));
                }
            }

            var totalEsperadas = filasActual.Sum(f => Math.Round(f.Esperadas, 2));
            var totalPendientes = filasActual.Sum(f => Math.Round(f.Pendientes, 2));
            var totalReportadas = totalEsperadas - totalPendientes;
            var porcentaje = Porcentaje(totalReportadas, totalEsperadas);

            resumenMeses.Add(new HistoricoMesDto(
                inicio.Year, inicio.Month, fechaCierre, mesAbierto,
                totalEsperadas, totalReportadas, totalPendientes, porcentaje, ParametrosDashboard.Semaforo(porcentaje),
                cumplidos, atrasosCarga, incumplidos, regularizados));
        }

        // sm - Recurrencia (confirmada 2026-10-02): ocasiones = cortes (15 y fin de mes) con más de
        // MaxDiasIncompletosPorCorte días hábiles incompletos. Recurrente con umbral o más ocasiones en la ventana.
        // sm - Regla anterior (meses Incumplido/Regularizado) reemplazada:
        // var mesesConAtraso = kv.Value.Count(m => m.Estado is "Incumplido" or "Regularizado");
        var colaboradores = estadosPorEmpleado
            .Where(kv => datos.EmpleadosPorId.ContainsKey(kv.Key))
            .Select(kv =>
            {
                var cortes = cortesPorEmpleado.GetValueOrDefault(kv.Key) ?? [];
                var ocasiones = cortes.Count(c => c.ConAtraso);
                return new HistoricoColaboradorDto(
                    kv.Key, NombreEmpleado(datos.EmpleadosPorId[kv.Key]), kv.Value,
                    cortes, ocasiones, ocasiones >= umbral);
            })
            .OrderByDescending(c => c.OcasionesConAtraso)
            .ThenByDescending(c => c.Meses.LastOrDefault()?.Pendientes ?? 0)
            .ThenBy(c => c.Colaborador)
            .ToList();

        return Results.Ok(new DashboardHistoricoResponse(
            mesesVentana, umbral, ParametrosDashboard.MaxDiasIncompletosPorCorte, ParametrosDashboard.ReglaRecurrencia,
            ParametrosDashboard.ReglaCierre, resumenMeses, colaboradores));
    }

    // =====================================================================
    // Carga de datos y utilidades
    // =====================================================================
    private static async Task<DatosBase> CargarDatosBaseAsync(ApplicationDbContext db)
    {
        var proyectos = await db.TblTimeReportProyectos
            .AsNoTracking()
            .Include(p => p.IdclienteNavigation)
            .Include(p => p.IdestadoproyectoNavigation)
            .Where(p => p.Activo)
            .ToListAsync();

        // sm - Asignaciones actuales (activas). Al guardar un proyecto, GuardarAsignaciones desactiva TODAS las filas
        // del proyecto y crea las nuevas: las filas inactivas son versiones anteriores, no se borran.
        var asignaciones = await db.TblTimeReportAsignacionProyectos
            .AsNoTracking()
            .Include(a => a.IdliderNavigation!).ThenInclude(l => l.IdpersonaNavigation)
            .Where(a => a.Activo)
            .ToListAsync();

        // sm - Historial: si un colaborador tiene filas inactivas en un proyecto pero ninguna activa, fue retirado.
        // Su vigencia termina en la fecha fin de la asignación o, si no la tenía, en la fecha en que se guardó el
        // proyecto sin él (fechamodificacion de la última fila desactivada).
        var paresActivos = asignaciones
            .Where(a => a.Idempleado.HasValue)
            .Select(a => (a.Idempleado!.Value, a.Idproyecto))
            .ToHashSet();
        var inactivas = await db.TblTimeReportAsignacionProyectos
            .AsNoTracking()
            .Where(a => !a.Activo && a.Idempleado != null)
            .Select(a => new
            {
                Idempleado = a.Idempleado!.Value,
                a.Idproyecto,
                a.Fechaasignacion,
                a.Fechafinasignacion,
                Retiro = a.Fechamodificacion ?? a.Fechacreacion
            })
            .ToListAsync();
        var retiradas = inactivas
            .Where(a => !paresActivos.Contains((a.Idempleado, a.Idproyecto)))
            .GroupBy(a => (a.Idempleado, a.Idproyecto))
            .Select(g =>
            {
                var ultima = g.OrderByDescending(a => a.Retiro).First();
                var fechaRetiro = DateOnly.FromDateTime(AHoraEcuador(ultima.Retiro));
                var hasta = ultima.Fechafinasignacion.HasValue && ultima.Fechafinasignacion.Value < fechaRetiro
                    ? ultima.Fechafinasignacion.Value
                    : fechaRetiro;
                return new AsignacionCalculo(g.Key.Idempleado, g.Key.Idproyecto, ultima.Fechaasignacion, hasta);
            })
            .ToList();

        // sm - Activos y también los que ya salieron (cuentan para salidas y para las horas del mes de salida).
        var empleados = await db.TblAdministracionEmpleados
            .AsNoTracking()
            .Include(e => e.IdpersonaNavigation)
            .Include(e => e.IdcargoNavigation)
            .Include(e => e.IdtipocontratoNavigation)
            .Where(e => e.Activo || e.Fechaterminacion != null)
            .ToListAsync();

        return new DatosBase
        {
            // sm - Historial de contratos (script 11): la jornada de cada día sale del contrato vigente ese día.
            JornadasPorEmpleado = await CalculoHorasPeriodo.CargarJornadasAsync(db),
            Proyectos = proyectos,
            Asignaciones = asignaciones,
            Empleados = empleados,
            ProyectosPorId = proyectos.ToDictionary(p => p.Id),
            EmpleadosPorId = empleados.ToDictionary(e => e.Id),
            AsignacionesRetiradas = retiradas
        };
    }

    private static async Task<List<DateOnly>> CargarFeriadosAsync(ApplicationDbContext db, DateOnly desde, DateOnly hasta) =>
        await db.TblTimeReportFeriados
            .AsNoTracking()
            .Where(f => f.Activo && f.Fechaferiado >= desde && f.Fechaferiado <= hasta)
            .Select(f => f.Fechaferiado)
            .ToListAsync();

    private static async Task<List<ActividadCalculo>> CargarActividadesAsync(ApplicationDbContext db, DateOnly desde, DateOnly hasta)
    {
        var filas = await db.TblTimeReportActividadDiaria
            .AsNoTracking()
            .Where(a => a.Activo && a.Fechaactividad >= desde && a.Fechaactividad <= hasta)
            .Select(a => new
            {
                a.Id,
                a.Idempleado,
                a.Idproyecto,
                a.Fechaactividad,
                a.Cantidadhoras,
                a.Fechacreacion,
                TipoActividad = a.IdtipoactividadNavigation.Nombretipo
            })
            .ToListAsync();

        var cambios = await CargarCambiosHorasAsync(db, desde);

        return filas.Select(a => new ActividadCalculo(
            a.Idempleado,
            a.Idproyecto,
            a.Fechaactividad,
            a.Cantidadhoras,
            ParametrosDashboard.TiposActividadNovedad.Contains(a.TipoActividad.Trim().ToUpper()),
            DateOnly.FromDateTime(AHoraEcuador(a.Fechacreacion)),
            cambios.TryGetValue(a.Id, out var c) ? c : null)).ToList();
    }

    // sm - Cambios de horas de actividades desde la auditoría (AuditInterceptor guarda los valores antes y después de
    // cada UPDATE). Solo importan los cambios desde el inicio del periodo: los cortes nunca son anteriores a esa fecha.
    // Las ediciones que no tocaron las horas (descripción, proyecto, etc.) se ignoran.
    private static async Task<Dictionary<int, IReadOnlyList<CambioHoras>>> CargarCambiosHorasAsync(ApplicationDbContext db, DateOnly desde)
    {
        var desdeUtc = desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var auditoria = await db.TblAuditoriaHistoricoGenerals
            .AsNoTracking()
            .Where(h => h.Nombretabla == "tbl_time_report_actividad_diaria" && h.Tipooperacion == "UPDATE" && h.Fechacambio >= desdeUtc)
            .Select(h => new { h.Idregistro, h.Fechacambio, h.Datosanteriores, h.Datosnuevos })
            .ToListAsync();

        var resultado = new Dictionary<int, List<CambioHoras>>();
        foreach (var h in auditoria)
        {
            if (!int.TryParse(h.Idregistro, out var id)) continue;
            var antes = LeerHoras(h.Datosanteriores);
            var despues = LeerHoras(h.Datosnuevos);
            if (antes is null || antes == despues) continue;
            if (!resultado.TryGetValue(id, out var lista)) resultado[id] = lista = [];
            lista.Add(new CambioHoras(DateOnly.FromDateTime(AHoraEcuador(h.Fechacambio)), antes.Value));
        }
        return resultado.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<CambioHoras>)kv.Value);
    }

    private static decimal? LeerHoras(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (!prop.Name.Equals("Cantidadhoras", StringComparison.OrdinalIgnoreCase)) continue;
                return prop.Value.ValueKind switch
                {
                    System.Text.Json.JsonValueKind.Number => prop.Value.GetDecimal(),
                    System.Text.Json.JsonValueKind.String when decimal.TryParse(prop.Value.GetString(),
                        System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) => v,
                    _ => null
                };
            }
        }
        catch (System.Text.Json.JsonException) { }
        return null;
    }

    private static async Task<DashboardParametrosDto> ParametrosAsync(ApplicationDbContext db, int horizonteDias)
    {
        var estados = await db.Set<TblAdministracionCatalogoDetalle>()
            .AsNoTracking()
            .Where(d => d.IdcatalogoNavigation.Codigo == CatalogoEstadosProyecto)
            .Select(d => new { d.Codigovalor, d.Valor })
            .ToListAsync();
        string Nombres(string[] codigos) => string.Join(", ",
            codigos.Select(c => estados.FirstOrDefault(e => e.Codigovalor == c)?.Valor ?? c));

        return new DashboardParametrosDto(
            ParametrosDashboard.UmbralVerde,
            ParametrosDashboard.UmbralAmarillo,
            horizonteDias,
            Nombres(ParametrosDashboard.EstadosActivos),
            Nombres(ParametrosDashboard.EstadosCerrados),
            "8 h por día laborable (6 h pasantes), lunes a viernes sin feriados, según ingreso/salida y vigencia de la asignación",
            ParametrosDashboard.ReglaReparto,
            "Actividades de tipo Vacaciones y Permiso: se restan de las horas esperadas y no cuentan como reportadas");
    }

    // sm - Filtro de proyecto: cliente, proyecto, estado y, si hay colaborador, proyectos donde está asignado.
    private static Func<int, bool> CrearFiltroProyecto(Filtros filtros, DatosBase datos)
    {
        var proyectosDelEmpleado = filtros.IdEmpleado.HasValue
            ? datos.AsignacionesCalculoPorEmpleado[filtros.IdEmpleado.Value].Select(a => a.IdProyecto).ToHashSet()
            : null;

        return idProyecto =>
        {
            if (!datos.ProyectosPorId.TryGetValue(idProyecto, out var p)) return false;
            if (filtros.IdCliente.HasValue && p.Idcliente != filtros.IdCliente) return false;
            if (filtros.IdProyecto.HasValue && p.Id != filtros.IdProyecto) return false;
            if (filtros.IdEstado.HasValue && p.Idestadoproyecto != filtros.IdEstado) return false;
            if (proyectosDelEmpleado is not null && !proyectosDelEmpleado.Contains(p.Id)) return false;
            return true;
        };
    }

    // sm - Filtro de colaborador: el colaborador elegido y, si hay filtro de cliente/proyecto/estado,
    // solo quienes tienen una asignación en esos proyectos.
    private static Func<int, bool> CrearFiltroEmpleado(Filtros filtros, DatosBase datos, Func<int, bool> proyectoCumpleFiltro)
    {
        var empleadosDeProyectos = filtros.FiltraProyectos
            ? datos.Asignaciones
                .Where(a => a.Idempleado.HasValue && proyectoCumpleFiltro(a.Idproyecto))
                .Select(a => a.Idempleado!.Value)
                .ToHashSet()
            : null;

        return idEmpleado =>
            (!filtros.IdEmpleado.HasValue || idEmpleado == filtros.IdEmpleado)
            && (empleadosDeProyectos is null || empleadosDeProyectos.Contains(idEmpleado));
    }

    private static bool EstaActivoEn(TblAdministracionEmpleado e, DateOnly fecha) =>
        (e.Activo || e.Fechaterminacion.HasValue)
        && (!e.Fechaingreso.HasValue || e.Fechaingreso.Value <= fecha)
        && (!e.Fechaterminacion.HasValue || e.Fechaterminacion.Value >= fecha);

    private static DateOnly? FinEfectivoAsignacion(TblTimeReportAsignacionProyecto a, DatosBase datos) =>
        a.Fechafinasignacion
        ?? (datos.ProyectosPorId.TryGetValue(a.Idproyecto, out var p) ? p.Fechafinreal ?? p.Fechafinplaneada : null);

    private static MovimientoColaboradorDto Movimiento(TblAdministracionEmpleado e, DateOnly fecha, DatosBase datos) =>
        new(e.Id, NombreEmpleado(e), e.IdpersonaNavigation?.Numeroidentificacion ?? "", e.IdcargoNavigation?.Nombrecargo ?? "",
            fecha,
            string.Join(", ", datos.AsignacionesPorEmpleado[e.Id]
                .Where(a => datos.ProyectosPorId.ContainsKey(a.Idproyecto))
                .Select(a => datos.ProyectosPorId[a.Idproyecto].Nombre)
                .Distinct()));

    private static string ResponsablesProyecto(int idProyecto, DatosBase datos) =>
        string.Join(", ", datos.AsignacionesPorProyecto[idProyecto]
            .Where(a => a.IdliderNavigation != null)
            .Select(a => NombreLider(a.IdliderNavigation!))
            .Distinct());

    private static string CodigoEstado(TblTimeReportProyecto p) =>
        p.IdestadoproyectoNavigation?.Codigovalor?.Trim().ToUpper() ?? "";

    private static string NombreCliente(TblTimeReportProyecto p) =>
        p.IdclienteNavigation?.Nombrecomercial ?? p.IdclienteNavigation?.Razonsocial ?? "Sin cliente";

    private static string NombreEmpleado(TblAdministracionEmpleado e) =>
        $"{e.IdpersonaNavigation?.Nombres} {e.IdpersonaNavigation?.Apellidos}".Trim();

    private static string NombreLider(TblAdministracionLider l) =>
        $"{l.IdpersonaNavigation?.Nombres} {l.IdpersonaNavigation?.Apellidos}".Trim();

    // sm - Cumplimiento = reportadas válidas ÷ esperadas × 100. Sin horas esperadas no hay brecha (100 %).
    // Se trunca (no se redondea) para que 99,996 % no se muestre como 100 % en verde.
    private static decimal Porcentaje(decimal reportadas, decimal esperadas) =>
        esperadas > 0 ? Math.Round(reportadas / esperadas * 100m, 2, MidpointRounding.ToZero) : 100m;

    // sm - Fechas guardadas en UTC mostradas en hora de Ecuador (UTC−5).
    private static DateTime AHoraEcuador(DateTime fechaUtc) =>
        DateTime.SpecifyKind(fechaUtc.ToUniversalTime().AddHours(-5), DateTimeKind.Unspecified);
}
