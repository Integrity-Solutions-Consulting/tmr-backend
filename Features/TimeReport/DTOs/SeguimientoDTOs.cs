using System;
using System.Collections.Generic;

namespace tmr_backend.Features.TimeReport.DTOs;

public record FiltroSeguimientoDto(
    string? Busqueda,
    string? ClienteSeleccionado,
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    string? Periodo
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
    decimal HorasDiasLaborables = 0m
);

public record AprobarHorasRequest(
    List<int> Ids
);

public record DescargarSeguimientoMultipleRequest(
    List<int> Ids,
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    string Formato = "xlsx"
);
