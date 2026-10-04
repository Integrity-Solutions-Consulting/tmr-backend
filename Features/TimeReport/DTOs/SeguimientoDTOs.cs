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

// sm - El proyecto de ESTA fila (colaborador+proyecto+cliente), con sus horas registradas. Se usa para
// generar el archivo de reporte de ese proyecto al descargar (ver SeguimientoColaboradorDto.Proyectos).
public record SeguimientoProyectoDto(
    int IdProyecto,
    string Nombre,
    string Cliente,
    string LiderTecnico,
    decimal HorasRegistradas
);

// sm - Una fila = un colaborador en UN proyecto (con su cliente y líder técnico) dentro del rango consultado.
// Un colaborador con 2 proyectos activos genera 2 filas, una por proyecto, cada una con sus propias horas/días
// calculados contra su propia jornada completa (8 h, o 6 h si el contrato es Pasantía): ver CalculoHorasPeriodo.
// Un colaborador sin proyecto asignado en el rango genera una única fila "Sin Proyecto"/"Sin Cliente"/"Sin Líder".
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
    // sm - El proyecto de esta fila, como lista de un solo elemento (null si es la fila "Sin Proyecto"). Se
    // mantiene como lista por compatibilidad con el frontend (descarga de reportes por proyecto).
    List<SeguimientoProyectoDto>? Proyectos = null
);

// sm - Se comenta: la funcionalidad de aprobar horas se retira de Seguimiento (endpoint /aprobar comentado).
// public record AprobarHorasRequest(
//     List<int> Ids
// );
