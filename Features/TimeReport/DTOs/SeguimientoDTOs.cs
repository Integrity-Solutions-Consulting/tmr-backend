using System;
using System.Collections.Generic;

namespace tmr_backend.Features.TimeReport.DTOs;

public record FiltroSeguimientoDto(
    // sm - Se quita "Busqueda": la búsqueda por colaborador/proyecto se hace en el frontend sobre los datos cargados.
    string? ClienteSeleccionado,
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    // El frontend manda "quincena"/"mes-completo" para saber qué preset de fechas mostrar,
    // pero el filtrado real ya llega resuelto en FechaDesde/FechaHasta, así que este endpoint
    // no lo usa. Se deja el parámetro (no se borra) por si a futuro se necesita aplicar una
    // regla distinta según el período en vez de solo el rango de fechas.
    string? Periodo
);

// sm - Un proyecto asignado al colaborador dentro del rango consultado, con sus horas registradas en ese mismo
// rango. Se usa para el desglose del modal "Ver detalle" y para generar un archivo de reporte por proyecto al
// descargar (la jornada/estado es del colaborador completo, no se recalcula por proyecto: ver SeguimientoColaboradorDto).
public record SeguimientoProyectoDto(
    int IdProyecto,
    string Nombre,
    string Cliente,
    string LiderTecnico,
    decimal HorasRegistradas
);

public record SeguimientoColaboradorDto(
    int Id,
    string Nombre,
    string Proyecto,
    string Cliente,
    string LiderTecnico,
    decimal NroHoras,
    string Estado,
    int DiasConReporte,
    int DiasACompletar,
    // sm - Campos nuevos para calcular bien "Horas por registrar" en el frontend. Se agregan al final y con valor
    // por defecto para no romper a quien construya el DTO con los parámetros anteriores.
    // HorasJornada: 8 h por día, o 6 h si el tipo de contrato es Pasantía (catálogo TCT, código PAS).
    decimal HorasJornada = 8m,
    // HorasEsperadas: días laborables (lun-vie sin feriados) dentro del rango y del periodo trabajado × HorasJornada.
    decimal HorasEsperadas = 0m,
    // HorasPorRegistrar: max(0, HorasEsperadas − horas registradas en ese mismo periodo).
    decimal HorasPorRegistrar = 0m,
    // sm - Para "Promedio por día": días laborables del periodo (hasta hoy, sin fines de semana ni feriados)
    // y horas registradas en esos días. Promedio = HorasDiasLaborables ÷ DiasLaborables.
    int DiasLaborables = 0,
    decimal HorasDiasLaborables = 0m,
    // sm - Proyectos del colaborador en el rango, con sus horas propias. El frontend los usa para generar un
    // archivo de reporte por proyecto al descargar y para el desglose del modal "Ver detalle".
    List<SeguimientoProyectoDto>? Proyectos = null
);

// sm - Se comenta: la funcionalidad de aprobar horas se retira de Seguimiento (endpoint /aprobar comentado).
// public record AprobarHorasRequest(
//     List<int> Ids
// );
