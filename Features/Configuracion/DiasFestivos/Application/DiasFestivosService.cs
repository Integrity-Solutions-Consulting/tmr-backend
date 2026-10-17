using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Configuracion.DiasFestivos.Domain;
using tmr_backend.Features.Configuracion.DiasFestivos.DTOs;
using tmr_backend.Features.Configuracion.DiasFestivos.Domain.Exceptions;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Configuracion.DiasFestivos.Application;

public interface IDiasFestivosService
{
    Task<SuccessResponse> CrearFeriadoAsync(CreateFeriadoRequest request, string usuarioActual, string ipActual);
    Task<SuccessResponse> ActualizarFeriadoAsync(int id, UpdateFeriadoRequest request, string usuarioActual, string ipActual);
    Task<SuccessResponse> EliminarFeriadoAsync(int id, string usuarioActual, string ipActual);
    Task<FeriadoResponse> ObtenerFeriadoPorIdAsync(int id);
    Task<List<FeriadoResponse>> ObtenerFeriadosAsync();
    Task<ImportarFeriadosResponse> ImportarFeriadosAsync(int anio, string usuarioActual, string ipActual);
}

public class DiasFestivosService : IDiasFestivosService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpClientFactory _httpClientFactory;

    public DiasFestivosService(ApplicationDbContext dbContext, IHttpClientFactory httpClientFactory)
    {
        _dbContext = dbContext;
        _httpClientFactory = httpClientFactory;
    }

    // sm - Fuente pública de feriados de Ecuador (gratuita, sin clave). Trae los feriados oficiales del año, pero NO los
    // traslados por decreto (ej. 24 de mayo en domingo que se pasa al lunes) ni días extra: por eso la importación es una
    // base y se revisa/corrige en la pantalla, donde el registro manual sigue funcionando igual.
    private const string UrlFeriadosEcuador = "https://date.nager.at/api/v3/PublicHolidays/{0}/EC";

    // sm - La fuente devuelve algunos nombres en inglés: se traducen y se asigna el tipo usado en el sistema.
    private static readonly Dictionary<string, (string Nombre, string Tipo)> NombresFeriados = new(StringComparer.OrdinalIgnoreCase)
    {
        ["New Year's Day"] = ("Año Nuevo", "Nacional"),
        ["Carnival"] = ("Carnaval", "Nacional"),
        ["Good Friday"] = ("Viernes Santo", "Religioso"),
        ["International Workers' Day"] = ("Día del Trabajo", "Nacional"),
        ["The Battle of Pichincha"] = ("Batalla de Pichincha", "Nacional"),
        ["Declaration of Independence of Quito"] = ("Primer Grito de Independencia", "Nacional"),
        ["Independence of Guayaquil"] = ("Independencia de Guayaquil", "Nacional"),
        ["All Souls' Day"] = ("Día de los Difuntos", "Nacional"),
        ["Independence of Cuenca"] = ("Independencia de Cuenca", "Nacional"),
        ["Christmas Day"] = ("Navidad", "Religioso"),
    };

    private sealed record FeriadoExterno(DateOnly date, string localName, string name);

    /// <summary>
    /// sm - Importa los feriados nacionales de Ecuador de un año. No duplica: se omite el feriado si ya existe uno activo
    /// en esa fecha o con el mismo nombre en ese año (así se respeta lo corregido a mano, como un feriado trasladado).
    /// </summary>
    public async Task<ImportarFeriadosResponse> ImportarFeriadosAsync(int anio, string usuarioActual, string ipActual)
    {
        if (anio is < 2000 or > 2100)
            throw new DatosInvalidosFeriadoException("Año inválido.");

        List<FeriadoExterno>? externos;
        try
        {
            var cliente = _httpClientFactory.CreateClient();
            cliente.Timeout = TimeSpan.FromSeconds(20);
            externos = await cliente.GetFromJsonAsync<List<FeriadoExterno>>(string.Format(UrlFeriadosEcuador, anio));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            throw new DatosInvalidosFeriadoException("No se pudo consultar la fuente de feriados. Intente más tarde o regístrelos manualmente.");
        }

        var existentes = await _dbContext.TblTimeReportFeriados
            .Where(f => f.Activo && f.Fechaferiado.Year == anio)
            .Select(f => new { f.Fechaferiado, f.Nombreferiado })
            .ToListAsync();
        var fechasExistentes = existentes.Select(f => f.Fechaferiado).ToHashSet();
        var nombresExistentes = existentes.Select(f => Normalizar(f.Nombreferiado)).ToHashSet();

        var creados = new List<string>();
        var omitidos = new List<string>();
        foreach (var externo in externos ?? [])
        {
            var (nombre, tipo) = NombresFeriados.TryGetValue(externo.name, out var traducido)
                ? traducido
                : (externo.localName, "Nacional");
            // sm - Carnaval son dos días con el mismo nombre: se distinguen por el día de la semana.
            if (nombre == "Carnaval")
                nombre = externo.date.DayOfWeek == DayOfWeek.Monday ? "Carnaval - Lunes" : "Carnaval - Martes";

            var etiqueta = $"{externo.date:dd/MM/yyyy} {nombre}";
            if (fechasExistentes.Contains(externo.date) || nombresExistentes.Contains(Normalizar(nombre)))
            {
                omitidos.Add(etiqueta);
                continue;
            }

            _dbContext.TblTimeReportFeriados.Add(new TblTimeReportFeriado
            {
                Nombreferiado = nombre,
                Fechaferiado = externo.date,
                Tipoferiado = tipo,
                Esrecurrente = false,
                Descripcion = "Importado automáticamente (date.nager.at). Revisar traslados por decreto.",
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = ipActual
            });
            fechasExistentes.Add(externo.date);
            creados.Add(etiqueta);
        }

        if (creados.Count > 0) await _dbContext.SaveChangesAsync();
        return new ImportarFeriadosResponse(anio, creados, omitidos);
    }

    private static string Normalizar(string texto)
    {
        var sinTildes = new string(texto.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
        return sinTildes.Trim().ToLowerInvariant();
    }

    public async Task<SuccessResponse> CrearFeriadoAsync(CreateFeriadoRequest request, string usuarioActual, string ipActual)
    {
        var feriadoDominio = Feriado.Crear(
            request.nombreFeriado, 
            request.fechaFeriado, 
            request.tipoFeriado, 
            request.esRecurrente, 
            request.descripcion);

        // Verificamos si existe otro feriado activo con el mismo nombre y fecha
        var existeFeriado = await _dbContext.TblTimeReportFeriados
            .AnyAsync(f => f.Activo && 
                           f.Nombreferiado.ToLower() == feriadoDominio.NombreFeriado.ToLower() &&
                           f.Fechaferiado == feriadoDominio.FechaFeriado);

        if (existeFeriado)
            throw new FeriadoYaExisteException(feriadoDominio.NombreFeriado);

        var nuevoFeriadoEntity = new TblTimeReportFeriado
        {
            Nombreferiado = feriadoDominio.NombreFeriado,
            Fechaferiado = feriadoDominio.FechaFeriado,
            Tipoferiado = feriadoDominio.TipoFeriado,
            Esrecurrente = feriadoDominio.EsRecurrente,
            Descripcion = feriadoDominio.Descripcion,
            Activo = true,
            Usuariocreacion = usuarioActual,
            Fechacreacion = DateTime.UtcNow,
            Ipcreacion = ipActual
        };

        _dbContext.TblTimeReportFeriados.Add(nuevoFeriadoEntity);
        await _dbContext.SaveChangesAsync();

        return new SuccessResponse("Feriado creado exitosamente.");
    }

    public async Task<SuccessResponse> ActualizarFeriadoAsync(int id, UpdateFeriadoRequest request, string usuarioActual, string ipActual)
    {
        var feriadoEntity = await _dbContext.TblTimeReportFeriados.FindAsync(id);

        if (feriadoEntity == null || !feriadoEntity.Activo)
            throw new FeriadoNoEncontradoException(id);

        var feriadoDominio = Feriado.Crear(
            request.nombreFeriado, 
            request.fechaFeriado, 
            request.tipoFeriado, 
            request.esRecurrente, 
            request.descripcion);

        var existeOtroFeriado = await _dbContext.TblTimeReportFeriados
            .AnyAsync(f => f.Activo && 
                           f.Id != id &&
                           f.Nombreferiado.ToLower() == feriadoDominio.NombreFeriado.ToLower() &&
                           f.Fechaferiado == feriadoDominio.FechaFeriado);

        if (existeOtroFeriado)
            throw new FeriadoYaExisteException(feriadoDominio.NombreFeriado);

        feriadoEntity.Nombreferiado = feriadoDominio.NombreFeriado;
        feriadoEntity.Fechaferiado = feriadoDominio.FechaFeriado;
        feriadoEntity.Tipoferiado = feriadoDominio.TipoFeriado;
        feriadoEntity.Esrecurrente = feriadoDominio.EsRecurrente;
        feriadoEntity.Descripcion = feriadoDominio.Descripcion;
        feriadoEntity.Usuariomodificacion = usuarioActual;
        feriadoEntity.Fechamodificacion = DateTime.UtcNow;
        feriadoEntity.Ipmodificacion = ipActual;

        _dbContext.TblTimeReportFeriados.Update(feriadoEntity);
        await _dbContext.SaveChangesAsync();

        return new SuccessResponse("Feriado actualizado exitosamente.");
    }

    public async Task<SuccessResponse> EliminarFeriadoAsync(int id, string usuarioActual, string ipActual)
    {
        var feriadoEntity = await _dbContext.TblTimeReportFeriados.FindAsync(id);

        if (feriadoEntity == null || !feriadoEntity.Activo)
            throw new FeriadoNoEncontradoException(id);

        // Eliminación lógica
        feriadoEntity.Activo = false;
        feriadoEntity.Usuariomodificacion = usuarioActual;
        feriadoEntity.Fechamodificacion = DateTime.UtcNow;
        feriadoEntity.Ipmodificacion = ipActual;

        _dbContext.TblTimeReportFeriados.Update(feriadoEntity);
        await _dbContext.SaveChangesAsync();

        return new SuccessResponse("Feriado eliminado exitosamente.");
    }

    public async Task<FeriadoResponse> ObtenerFeriadoPorIdAsync(int id)
    {
        var feriadoEntity = await _dbContext.TblTimeReportFeriados.FindAsync(id);

        if (feriadoEntity == null || !feriadoEntity.Activo)
            throw new FeriadoNoEncontradoException(id);

        return new FeriadoResponse(
            id: feriadoEntity.Id,
            nombreFeriado: feriadoEntity.Nombreferiado,
            fechaFeriado: feriadoEntity.Fechaferiado,
            tipoFeriado: feriadoEntity.Tipoferiado ?? string.Empty,
            esRecurrente: feriadoEntity.Esrecurrente ?? false,
            descripcion: feriadoEntity.Descripcion,
            activo: feriadoEntity.Activo
        );
    }

    public async Task<List<FeriadoResponse>> ObtenerFeriadosAsync()
    {
        var feriados = await _dbContext.TblTimeReportFeriados
            .Where(f => f.Activo)
            .OrderByDescending(f => f.Fechaferiado)
            .ToListAsync();

        return feriados.Select(f => new FeriadoResponse(
            id: f.Id,
            nombreFeriado: f.Nombreferiado,
            fechaFeriado: f.Fechaferiado,
            tipoFeriado: f.Tipoferiado ?? string.Empty,
            esRecurrente: f.Esrecurrente ?? false,
            descripcion: f.Descripcion,
            activo: f.Activo
        )).ToList();
    }
}
