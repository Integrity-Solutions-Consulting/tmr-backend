namespace tmr_backend.Features.Dashboard.DTOs;

// sm - DTOs del Dashboard ejecutivo (Requerimiento_Funcional_Dashboard_Time_Report).

// ── Filtros ──
public record OpcionFiltroDto(int Id, string Nombre);
public record OpcionProyectoFiltroDto(int Id, string Nombre, string Codigo, int? IdCliente, int IdEstado);
public record DashboardFiltrosResponse(
    List<OpcionFiltroDto> Clientes,
    List<OpcionProyectoFiltroDto> Proyectos,
    List<OpcionFiltroDto> Estados,
    List<OpcionFiltroDto> Colaboradores);

// ── Parámetros y trazabilidad (RF 16) ──
public record DashboardParametrosDto(
    decimal UmbralVerde,
    decimal UmbralAmarillo,
    int HorizonteDias,
    string EstadosActivos,
    string EstadosCerrados,
    string ReglaJornada,
    string ReglaReparto,
    string FuenteNovedades);

public record DashboardPeriodoDto(
    int Anio,
    int Mes,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    DateOnly FechaCorte,
    DateTime FechaGeneracion,
    DateTime? UltimoRegistroActividad);

// ── Tarjetas (sección 6.1) ──
public record DashboardTarjetasDto(
    int ColaboradoresSinProyecto,
    int Ingresos,
    int Salidas,
    int ClientesConProyectosActivos,
    int ProyectosActivos,
    int ProyectosVencidos,
    int ProyectosProximos,
    int ProyectosNuevos,
    int ProyectosCerrados,
    int ProyectosConDesvinculados);

// ── Detalles de las tarjetas ──
public record ColaboradorSinProyectoDto(
    int IdEmpleado,
    string Colaborador,
    string Identificacion,
    string Cargo,
    string Responsable,
    DateOnly? FechaIngreso,
    string UltimoProyecto,
    int? DiasSinAsignacion);

public record MovimientoColaboradorDto(
    int IdEmpleado,
    string Colaborador,
    string Identificacion,
    string Cargo,
    DateOnly Fecha,
    string Proyectos);

// sm - Un proyecto con las categorías a las que pertenece en el periodo:
// "Activo", "Vencido", "Proximo", "Nuevo", "Cerrado". Cada tarjeta / segmento filtra por su categoría.
public record ProyectoDashboardDto(
    int IdProyecto,
    string Codigo,
    string Nombre,
    int? IdCliente,
    string Cliente,
    string Responsable,
    string Estado,
    DateOnly? FechaInicio,
    DateOnly? FechaTermino,
    DateOnly? FechaCierre,
    int? DiasAtraso,
    int? DiasParaTerminar,
    List<string> Categorias);

public record AsignacionDesvinculadaDto(
    int IdProyecto,
    string CodigoProyecto,
    string Proyecto,
    string Cliente,
    int IdEmpleado,
    string Colaborador,
    DateOnly FechaSalida,
    DateOnly? FechaAsignacion,
    DateOnly? FechaFinAsignacion,
    string Rol);

// ── Cumplimiento (RF 12 y RF 13) ──
public record CumplimientoTotalesDto(
    decimal Esperadas,
    decimal Reportadas,
    decimal Pendientes,
    decimal Porcentaje,
    string Semaforo);

public record CumplimientoClienteDto(
    int IdCliente,
    string Cliente,
    decimal Esperadas,
    decimal Reportadas,
    decimal Pendientes,
    decimal Porcentaje,
    string Semaforo,
    int Colaboradores,
    int ColaboradoresIncompletos);

public record CumplimientoDetalleDto(
    int IdEmpleado,
    string Colaborador,
    string Correo,
    int IdProyecto,
    string CodigoProyecto,
    string Proyecto,
    int IdCliente,
    string Cliente,
    DateOnly? VigenciaDesde,
    DateOnly? VigenciaHasta,
    decimal Esperadas,
    decimal Registradas,
    decimal Reportadas,
    decimal Pendientes,
    decimal Porcentaje,
    string Semaforo,
    DateOnly? UltimoRegistro,
    string Estado);

public record DashboardEjecutivoResponse(
    DashboardPeriodoDto Periodo,
    DashboardParametrosDto Parametros,
    DashboardTarjetasDto Tarjetas,
    List<ColaboradorSinProyectoDto> ColaboradoresSinProyecto,
    List<MovimientoColaboradorDto> Ingresos,
    List<MovimientoColaboradorDto> Salidas,
    List<ProyectoDashboardDto> Proyectos,
    List<AsignacionDesvinculadaDto> Desvinculados,
    CumplimientoTotalesDto CumplimientoTotal,
    List<CumplimientoClienteDto> CumplimientoClientes,
    List<CumplimientoDetalleDto> CumplimientoDetalle,
    List<RegistroFueraDeAsignacionDto> FueraDeAsignacion);

// sm - Horas registradas que no entran al cumplimiento (proyecto sin asignación vigente, o sin proyecto).
public record RegistroFueraDeAsignacionDto(
    int IdEmpleado,
    string Colaborador,
    int? IdProyecto,
    string Proyecto,
    string Cliente,
    decimal Horas,
    DateOnly UltimoRegistro);

// ── Histórico y recurrencia (RF 15) ──
// sm - Estado de un colaborador en un mes:
// "Cumplido", "AtrasoCarga" (mes aún abierto con horas pendientes), "Incumplido" (cerrado y sigue pendiente),
// "Regularizado" (tenía pendientes al cierre y completó después), "SinAsignacion".
public record HistoricoMesDto(
    int Anio,
    int Mes,
    DateOnly FechaCierre,
    bool MesAbierto,
    decimal Esperadas,
    decimal Reportadas,
    decimal Pendientes,
    decimal Porcentaje,
    string Semaforo,
    int Cumplidos,
    int AtrasosCarga,
    int Incumplidos,
    int Regularizados);

public record HistoricoEstadoMesDto(
    int Anio,
    int Mes,
    decimal Esperadas,
    decimal Pendientes,
    decimal PendientesAlCierre,
    decimal Porcentaje,
    string Estado);

public record HistoricoColaboradorDto(
    int IdEmpleado,
    string Colaborador,
    List<HistoricoEstadoMesDto> Meses,
    int MesesConAtraso,
    bool Recurrente);

public record DashboardHistoricoResponse(
    int MesesVentana,
    int UmbralRecurrencia,
    string ReglaCierre,
    List<HistoricoMesDto> Meses,
    List<HistoricoColaboradorDto> Colaboradores);
