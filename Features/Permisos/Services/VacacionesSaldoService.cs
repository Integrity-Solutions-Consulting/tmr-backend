using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.TimeReport.Services;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Permisos.Services;

public sealed record ResumenSaldoVacaciones(
    string Regimen,                      // "AFILIADO" | "MIXTO" | "FACTURA"
    string TipoContratoNombre,
    string? EmpresaNombre,
    bool TieneDerechoVacaciones,
    bool ExcepcionFacturaActiva,
    DateOnly? FechaIngresoOriginal,
    DateOnly? FechaInicioVacaciones,
    int AniosAntiguedad,
    decimal DiasGanadosAcumulados,
    decimal DiasProporcionalesCicloActual,
    decimal DiasConsumidos,
    decimal DiasAjuste,
    decimal SaldoDisponible);

public static class VacacionesSaldoService
{
    public const decimal MaximoAnticipoDias = 15m;
    public const decimal MaximoDiasPorSolicitudVacaciones = 30m;

    /// <summary>
    /// Calcula el saldo de vacaciones de un colaborador según las reglas de negocio ISC y Art. 69 Código del Trabajo:
    /// - Régimen derivado sin nuevas tablas en administracion:
    ///   * TCT = FJ + EMP = RISC ('RPS E ISC') -> MIXTO (con derecho)
    ///   * TCT = FJ/MT + otra empresa -> AFILIADO (con derecho)
    ///   * TCT = CON/COS/PAS -> FACTURA (sin derecho por defecto; habilitable vía EXCEPCION_FACTURA)
    /// - Fecha base: 1) ajuste FECHA_AFILIACION (fecha real de afiliación IESS), 2) fechadesde del tramo
    ///   afiliado FJ/MT en tbl_administracion_empleado_contrato, 3) fallback fechaingreso.
    /// - Ciclos por aniversario: 0 días antes del 1.er año; 15 días base por año cumplido + 1 día adicional
    ///   por cada año desde el 6.º (tope +15 => máximo 30 días/año), acumulables progresivamente.
    /// - Descuenta todas las solicitudes activas (no rechazadas ni observadas) con descuenta_vacaciones = true
    ///   (o tipo VACACIONES) y aplica ajustes manuales. Puede ser negativo cuando se toman vacaciones anticipadas.
    /// </summary>
    public static ResumenSaldoVacaciones CalcularSaldo(
        TblAdministracionEmpleado? empleado,
        IReadOnlyCollection<TblAdministracionEmpleadoContrato> contratosEmpleado,
        IReadOnlyDictionary<int, TblAdministracionCatalogoDetalle> catalogosPorId,
        IReadOnlyCollection<TblTimeReportSolicitud> solicitudesEmpleado,
        IReadOnlyCollection<TblTimeReportAjusteVacaciones> ajustesEmpleado,
        DateOnly? fechaCorteOverride = null)
    {
        var hoy = fechaCorteOverride ?? CalculoHorasPeriodo.HoyEcuador();

        if (empleado is null)
        {
            return new ResumenSaldoVacaciones(
                Regimen: "AFILIADO",
                TipoContratoNombre: "Relación de dependencia (Nómina)",
                EmpresaNombre: null,
                TieneDerechoVacaciones: false,
                ExcepcionFacturaActiva: false,
                FechaIngresoOriginal: null,
                FechaInicioVacaciones: null,
                AniosAntiguedad: 0,
                DiasGanadosAcumulados: 0m,
                DiasProporcionalesCicloActual: 0m,
                DiasConsumidos: 0m,
                DiasAjuste: 0m,
                SaldoDisponible: 0m);
        }

        // 1. Contrato vigente (historial -> fallback campo empleado)
        var contratosActivos = contratosEmpleado
            .Where(c => c.Activo)
            .OrderByDescending(c => c.Fechahasta == null)
            .ThenByDescending(c => c.Fechadesde)
            .ToList();

        var contratoVigente = contratosActivos.FirstOrDefault(c => c.Fechahasta == null)
                              ?? contratosActivos.FirstOrDefault();

        var idTipoContratoActual = contratoVigente?.Idtipocontrato ?? empleado.Idtipocontrato;
        TblAdministracionCatalogoDetalle? detalleContrato = null;
        if (idTipoContratoActual.HasValue)
        {
            if (empleado.IdtipocontratoNavigation?.Id == idTipoContratoActual.Value)
                detalleContrato = empleado.IdtipocontratoNavigation;
            else
                catalogosPorId.TryGetValue(idTipoContratoActual.Value, out detalleContrato);
        }

        var codContrato = detalleContrato?.Codigovalor?.Trim().ToUpperInvariant() ?? "";
        var valContrato = detalleContrato?.Valor?.Trim() ?? "Relación de dependencia (Nómina)";

        TblAdministracionCatalogoDetalle? detalleEmpresa = empleado.IdempresacatalogoNavigation;
        if (detalleEmpresa is null && empleado.Idempresacatalogo.HasValue)
            catalogosPorId.TryGetValue(empleado.Idempresacatalogo.Value, out detalleEmpresa);

        var codEmpresa = detalleEmpresa?.Codigovalor?.Trim().ToUpperInvariant() ?? "";
        var valEmpresa = detalleEmpresa?.Valor?.Trim();

        var esFactura = EsContratoFactura(codContrato, valContrato);
        var esMixto = !esFactura && (
            codEmpresa == "RISC"
            || (!string.IsNullOrWhiteSpace(valEmpresa)
                && (valEmpresa.Contains("RPS E ISC", StringComparison.OrdinalIgnoreCase)
                    || valEmpresa.Contains("RPS / ISC", StringComparison.OrdinalIgnoreCase))));

        var regimen = esFactura ? "FACTURA" : (esMixto ? "MIXTO" : "AFILIADO");

        // 2. Excepción de factura y ajustes manuales
        var ajustesActivos = ajustesEmpleado.Where(a => a.Activo).ToList();
        var ultimaExcepcion = ajustesActivos
            .Where(a => a.TipoAjuste == "EXCEPCION_FACTURA" && a.PermiteVacaciones.HasValue)
            .OrderByDescending(a => a.FechaCreacion)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

        var excepcionFacturaActiva = esFactura && (ultimaExcepcion?.PermiteVacaciones == true);
        var tieneDerecho = !esFactura || excepcionFacturaActiva;

        // 3. Fecha base de afiliación (fechadesde del contrato afiliado FJ/MT en historial -> fallback fechaingreso)
        DateOnly? fechaBase = null;
        if (!esFactura)
        {
            var contratosAfiliados = contratosActivos
                .Where(c =>
                {
                    catalogosPorId.TryGetValue(c.Idtipocontrato, out var det);
                    var cod = det?.Codigovalor?.Trim().ToUpperInvariant() ?? "";
                    var val = det?.Valor?.Trim() ?? "";
                    return !EsContratoFactura(cod, val);
                })
                .OrderByDescending(c => c.Fechadesde)
                .ToList();

            // Si tiene un contrato afiliado vigente, tomamos la fecha de inicio de ese tramo afiliado continuo
            var vigenteAfil = contratosAfiliados.FirstOrDefault(c => c.Fechahasta == null)
                              ?? contratosAfiliados.FirstOrDefault();

            if (vigenteAfil is not null)
            {
                // Si hubiera varios registros afiliados consecutivos sin hueco de factura, retrocedemos al inicio del tramo continuo
                var inicioTramo = vigenteAfil.Fechadesde;
                foreach (var prev in contratosAfiliados.Where(c => c.Id != vigenteAfil.Id && c.Fechahasta.HasValue).OrderByDescending(c => c.Fechahasta))
                {
                    if (prev.Fechahasta!.Value.AddDays(1) >= inicioTramo)
                        inicioTramo = prev.Fechadesde;
                    else
                        break;
                }
                fechaBase = inicioTramo;
            }
            else
            {
                fechaBase = empleado.Fechaingreso;
            }
        }
        else if (excepcionFacturaActiva)
        {
            fechaBase = empleado.Fechaingreso;
        }

        // 3b. Fecha de AFILIACIÓN IESS registrada por Talento Humano / HC 2026 (prevalece sobre
        //     fechaingreso y sobre el historial de contratos, que suele arrancar en el ingreso general
        //     aunque el colaborador haya iniciado por Factura o Pasantía).
        var ultimaFechaAfiliacion = ajustesActivos
            .Where(a => a.TipoAjuste == "FECHA_AFILIACION" && a.FechaAfiliacionBase.HasValue)
            .OrderByDescending(a => a.FechaCreacion)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();
        if (tieneDerecho && ultimaFechaAfiliacion is not null)
            fechaBase = ultimaFechaAfiliacion.FechaAfiliacionBase;

        // 4. Cálculo de años cumplidos, ciclos acumulados y proporcional del ciclo en curso
        var aniosCompletos = 0;
        var diasGanadosAcumulados = 0m;
        var diasProporcionales = 0m;

        if (tieneDerecho && fechaBase.HasValue && fechaBase.Value <= hoy)
        {
            aniosCompletos = hoy.Year - fechaBase.Value.Year;
            if (fechaBase.Value.AddYears(aniosCompletos) > hoy)
                aniosCompletos--;
            aniosCompletos = Math.Max(0, aniosCompletos);

            for (var anioCiclo = 1; anioCiclo <= aniosCompletos; anioCiclo++)
            {
                diasGanadosAcumulados += CalcularDiasPorCiclo(anioCiclo);
            }

            var inicioCicloActual = fechaBase.Value.AddYears(aniosCompletos);
            var finCicloActual = fechaBase.Value.AddYears(aniosCompletos + 1);
            var diasTranscurridos = Math.Max(0, hoy.DayNumber - inicioCicloActual.DayNumber);
            var diasTotalesCiclo = Math.Max(1, finCicloActual.DayNumber - inicioCicloActual.DayNumber);
            var diasObjetivoCiclo = CalcularDiasPorCiclo(aniosCompletos + 1);
            diasProporcionales = Math.Round((decimal)diasTranscurridos / diasTotalesCiclo * diasObjetivoCiclo, 2);
        }

        // 5. Días consumidos y ajustes manuales
        var diasConsumidos = solicitudesEmpleado
            .Where(s => s.Activo
                        && s.Estado?.Codigo is not ("RECHAZADA" or "OBSERVADA")
                        && (s.DescuentaVacaciones || string.Equals(s.TipoPermiso?.Codigo, "VACACIONES", StringComparison.OrdinalIgnoreCase)))
            .Sum(s => s.DiasSolicitados);

        var diasAjuste = ajustesActivos
            .Where(a => a.TipoAjuste == "AJUSTE_DIAS")
            .Sum(a => a.DiasAjuste);

        var saldoDisponible = tieneDerecho || diasAjuste != 0m || diasConsumidos > 0m
            ? Math.Round(diasGanadosAcumulados + diasAjuste - diasConsumidos, 2)
            : 0m;

        return new ResumenSaldoVacaciones(
            Regimen: regimen,
            TipoContratoNombre: valContrato,
            EmpresaNombre: valEmpresa,
            TieneDerechoVacaciones: tieneDerecho,
            ExcepcionFacturaActiva: excepcionFacturaActiva,
            FechaIngresoOriginal: empleado.Fechaingreso,
            FechaInicioVacaciones: fechaBase,
            AniosAntiguedad: aniosCompletos,
            DiasGanadosAcumulados: diasGanadosAcumulados,
            DiasProporcionalesCicloActual: diasProporcionales,
            DiasConsumidos: Math.Round(diasConsumidos, 2),
            DiasAjuste: Math.Round(diasAjuste, 2),
            SaldoDisponible: saldoDisponible);
    }

    public static decimal CalcularDiasPorCiclo(int numeroAnioCumplido)
    {
        if (numeroAnioCumplido <= 0) return 0m;
        if (numeroAnioCumplido <= 5) return 15m;
        return 15m + Math.Min(15m, numeroAnioCumplido - 5m);
    }

    public static bool EsContratoFactura(string? codigoContrato, string? valorContrato)
    {
        var cod = codigoContrato?.Trim().ToUpperInvariant() ?? "";
        if (cod is "CON" or "COS" or "PAS")
            return true;
        if (cod is "FJ" or "MT")
            return false;

        var val = valorContrato ?? "";
        return val.Contains("Consultor", StringComparison.OrdinalIgnoreCase)
               || val.Contains("Servicios", StringComparison.OrdinalIgnoreCase)
               || val.Contains("Honorarios", StringComparison.OrdinalIgnoreCase)
               || val.Contains("Factura", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<ResumenSaldoVacaciones> CalcularSaldoEmpleadoAsync(
        ApplicationDbContext db,
        TblAdministracionEmpleado? empleado,
        IReadOnlyCollection<TblTimeReportSolicitud>? solicitudesPrecargadas,
        CancellationToken ct)
    {
        if (empleado is null)
            return CalcularSaldo(null, [], new Dictionary<int, TblAdministracionCatalogoDetalle>(), [], []);

        var contratos = await db.TblAdministracionEmpleadoContratos
            .AsNoTracking()
            .Where(c => c.Idempleado == empleado.Id && c.Activo)
            .ToListAsync(ct);

        var idsCatalogo = new HashSet<int>();
        if (empleado.Idtipocontrato.HasValue) idsCatalogo.Add(empleado.Idtipocontrato.Value);
        if (empleado.Idempresacatalogo.HasValue) idsCatalogo.Add(empleado.Idempresacatalogo.Value);
        foreach (var c in contratos) idsCatalogo.Add(c.Idtipocontrato);

        var catalogos = idsCatalogo.Count == 0
            ? new Dictionary<int, TblAdministracionCatalogoDetalle>()
            : await db.TblAdministracionCatalogoDetalles
                .AsNoTracking()
                .Where(d => idsCatalogo.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, ct);

        var solicitudes = solicitudesPrecargadas is not null
            ? solicitudesPrecargadas
            : await db.TblTimeReportSolicitudes
                .AsNoTracking()
                .Where(s => s.IdEmpleado == empleado.Id && s.Activo)
                .Include(s => s.Estado)
                .Include(s => s.TipoPermiso)
                .ToListAsync(ct);

        var ajustes = await db.TblTimeReportAjustesVacaciones
            .AsNoTracking()
            .Where(a => a.IdEmpleado == empleado.Id && a.Activo)
            .ToListAsync(ct);

        return CalcularSaldo(empleado, contratos, catalogos, solicitudes, ajustes);
    }
}
