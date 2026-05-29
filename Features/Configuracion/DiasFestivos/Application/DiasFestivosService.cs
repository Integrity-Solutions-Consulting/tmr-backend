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
}

public class DiasFestivosService : IDiasFestivosService
{
    private readonly ApplicationDbContext _dbContext;

    public DiasFestivosService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
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
