using System;
using System.Collections.Generic;

namespace tmr_backend.Features.TimeReport.DTOs;

public record FiltroSeguimientoDto(
    string? Busqueda,
    string? ClienteSeleccionado,
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    string Periodo
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
    int DiasACompletar
);

public record AprobarHorasRequest(
    List<int> Ids
);
