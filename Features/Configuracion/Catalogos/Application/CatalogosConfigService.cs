using Microsoft.EntityFrameworkCore;
using tmr_backend.Features.Configuracion.Catalogos.DTOs;
using tmr_backend.Features.Configuracion.Catalogos.Domain.Exceptions;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Configuracion.Catalogos.Application;

public class CatalogosConfigService : ICatalogosConfigService
{
    private readonly ApplicationDbContext _dbContext;

    public CatalogosConfigService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SuccessResponse> CrearDetalleAsync(CreateCatalogoDetalleRequest request, string usuarioActual, string ipActual)
    {
        if (request.idCatalogo == -101)
        {
            if (string.IsNullOrWhiteSpace(request.valor))
                throw new DatosInvalidosDetalleException("El nombre es requerido.");

            var nuevoTipoProyecto = new TblTimeReportTipoProyecto
            {
                Nombretipo = request.valor.Trim(),
                Essubtipo = false,
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = ipActual
            };
            _dbContext.TblTimeReportTipoProyectos.Add(nuevoTipoProyecto);
            await _dbContext.SaveChangesAsync();
            return new SuccessResponse("Tipo de proyecto creado exitosamente.");
        }

        if (request.idCatalogo == -102)
        {
            if (string.IsNullOrWhiteSpace(request.valor))
                throw new DatosInvalidosDetalleException("El nombre es requerido.");

            var nuevoTipoActividad = new TblTimeReportTipoActividad
            {
                Nombretipo = request.valor.Trim(),
                Descripcion = request.descripcion?.Trim(),
                Codigocolor = "#163572", // Default color
                Activo = true,
                Usuariocreacion = usuarioActual,
                Fechacreacion = DateTime.UtcNow,
                Ipcreacion = ipActual
            };
            _dbContext.TblTimeReportTipoActividads.Add(nuevoTipoActividad);
            await _dbContext.SaveChangesAsync();
            return new SuccessResponse("Tipo de actividad creado exitosamente.");
        }

        // 1. Validaciones de Entrada (Input Validation)
        if (request.idCatalogo <= 0)
            throw new DatosInvalidosDetalleException("El ID del catálogo es requerido y debe ser mayor a 0.");

        if (string.IsNullOrWhiteSpace(request.codigoValor))
            throw new DatosInvalidosDetalleException("El código del valor es requerido.");

        if (request.codigoValor.Length > 5)
            throw new DatosInvalidosDetalleException("El código del valor no puede exceder los 5 caracteres.");

        if (string.IsNullOrWhiteSpace(request.valor))
            throw new DatosInvalidosDetalleException("El valor es requerido.");

        if (request.valor.Length > 100)
            throw new DatosInvalidosDetalleException("El valor no puede exceder los 100 caracteres.");

        if (request.descripcion != null && request.descripcion.Length > 255)
            throw new DatosInvalidosDetalleException("La descripción no puede exceder los 255 caracteres.");

        if (request.valorExtra != null && request.valorExtra.Length > 100)
            throw new DatosInvalidosDetalleException("El valor extra no puede exceder los 100 caracteres.");

        if (request.orden.HasValue && request.orden.Value < 0)
            throw new DatosInvalidosDetalleException("El orden debe ser un número positivo.");

        // 2. Reglas de Negocio (Business Rules)
        // Verificar que el catálogo padre exista
        var catalogoExiste = await _dbContext.TblAdministracionCatalogos
            .AnyAsync(c => c.Id == request.idCatalogo);

        if (!catalogoExiste)
            throw new CatalogoNoEncontradoException(request.idCatalogo);

        // Verificar duplicados de codigovalor dentro del mismo catálogo padre
        var codigoDuplicado = await _dbContext.TblAdministracionCatalogoDetalles
            .AnyAsync(d => d.Idcatalogo == request.idCatalogo 
                           && d.Codigovalor.ToLower() == request.codigoValor.ToLower()
                           && d.Activo);

        if (codigoDuplicado)
            throw new DetalleCodigoDuplicadoException(request.codigoValor, request.idCatalogo);

        // 3. Crear entidad y persistir
        var nuevoDetalle = new TblAdministracionCatalogoDetalle
        {
            Idcatalogo = request.idCatalogo,
            Codigovalor = request.codigoValor.ToUpper().Trim(),
            Valor = request.valor.Trim(),
            Descripcion = request.descripcion?.Trim(),
            Orden = request.orden,
            Valorextra = request.valorExtra?.Trim(),
            Activo = true,
            Usuariocreacion = usuarioActual,
            Fechacreacion = DateTime.UtcNow,
            Ipcreacion = ipActual
        };

        _dbContext.TblAdministracionCatalogoDetalles.Add(nuevoDetalle);
        await _dbContext.SaveChangesAsync();

        return new SuccessResponse("Detalle de catálogo creado exitosamente.");
    }

    public async Task<SuccessResponse> ActualizarDetalleAsync(int id, UpdateCatalogoDetalleRequest request, string usuarioActual, string ipActual)
    {
        if (request.idCatalogo == -101)
        {
            if (string.IsNullOrWhiteSpace(request.valor))
                throw new DatosInvalidosDetalleException("El nombre es requerido.");

            var tipoProyecto = await _dbContext.TblTimeReportTipoProyectos.FirstOrDefaultAsync(tp => tp.Id == id);
            if (tipoProyecto == null) throw new DetalleNoEncontradoException(id);

            tipoProyecto.Nombretipo = request.valor.Trim();
            if (request.activo.HasValue) tipoProyecto.Activo = request.activo.Value;
            tipoProyecto.Usuariomodificacion = usuarioActual;
            tipoProyecto.Fechamodificacion = DateTime.UtcNow;
            tipoProyecto.Ipmodificacion = ipActual;

            _dbContext.TblTimeReportTipoProyectos.Update(tipoProyecto);
            await _dbContext.SaveChangesAsync();
            return new SuccessResponse("Tipo de proyecto actualizado exitosamente.");
        }

        if (request.idCatalogo == -102)
        {
            if (string.IsNullOrWhiteSpace(request.valor))
                throw new DatosInvalidosDetalleException("El nombre es requerido.");

            var tipoActividad = await _dbContext.TblTimeReportTipoActividads.FirstOrDefaultAsync(ta => ta.Id == id);
            if (tipoActividad == null) throw new DetalleNoEncontradoException(id);

            tipoActividad.Nombretipo = request.valor.Trim();
            tipoActividad.Descripcion = request.descripcion?.Trim();
            if (request.activo.HasValue) tipoActividad.Activo = request.activo.Value;
            tipoActividad.Usuariomodificacion = usuarioActual;
            tipoActividad.Fechamodificacion = DateTime.UtcNow;
            tipoActividad.Ipmodificacion = ipActual;

            _dbContext.TblTimeReportTipoActividads.Update(tipoActividad);
            await _dbContext.SaveChangesAsync();
            return new SuccessResponse("Tipo de actividad actualizado exitosamente.");
        }

        // 1. Validaciones de Entrada
        if (string.IsNullOrWhiteSpace(request.valor))
            throw new DatosInvalidosDetalleException("El valor es requerido.");

        if (request.valor.Length > 100)
            throw new DatosInvalidosDetalleException("El valor no puede exceder los 100 caracteres.");

        if (request.descripcion != null && request.descripcion.Length > 255)
            throw new DatosInvalidosDetalleException("La descripción no puede exceder los 255 caracteres.");

        if (request.valorExtra != null && request.valorExtra.Length > 100)
            throw new DatosInvalidosDetalleException("El valor extra no puede exceder los 100 caracteres.");

        if (request.orden.HasValue && request.orden.Value < 0)
            throw new DatosInvalidosDetalleException("El orden debe ser un número positivo.");

        // 2. Buscar entidad
        var detalle = await _dbContext.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(d => d.Id == id);

        if (detalle == null)
            throw new DetalleNoEncontradoException(id);

        // 3. Actualizar valores y auditoría
        detalle.Valor = request.valor.Trim();
        detalle.Descripcion = request.descripcion?.Trim();
        detalle.Orden = request.orden;
        detalle.Valorextra = request.valorExtra?.Trim();
        if (request.activo.HasValue)
        {
            detalle.Activo = request.activo.Value;
        }
        
        detalle.Usuariomodificacion = usuarioActual;
        detalle.Fechamodificacion = DateTime.UtcNow;
        detalle.Ipmodificacion = ipActual;

        _dbContext.TblAdministracionCatalogoDetalles.Update(detalle);
        await _dbContext.SaveChangesAsync();

        return new SuccessResponse("Detalle de catálogo actualizado exitosamente.");
    }

    public async Task<SuccessResponse> EliminarDetalleAsync(int id, int? idCatalogo, string usuarioActual, string ipActual)
    {
        if (idCatalogo == -101)
        {
            var tipoProyecto = await _dbContext.TblTimeReportTipoProyectos.FirstOrDefaultAsync(tp => tp.Id == id);
            if (tipoProyecto == null) throw new DetalleNoEncontradoException(id);

            tipoProyecto.Activo = false;
            tipoProyecto.Usuariomodificacion = usuarioActual;
            tipoProyecto.Fechamodificacion = DateTime.UtcNow;
            tipoProyecto.Ipmodificacion = ipActual;

            _dbContext.TblTimeReportTipoProyectos.Update(tipoProyecto);
            await _dbContext.SaveChangesAsync();
            return new SuccessResponse("Tipo de proyecto eliminado exitosamente.");
        }

        if (idCatalogo == -102)
        {
            var tipoActividad = await _dbContext.TblTimeReportTipoActividads.FirstOrDefaultAsync(ta => ta.Id == id);
            if (tipoActividad == null) throw new DetalleNoEncontradoException(id);

            tipoActividad.Activo = false;
            tipoActividad.Usuariomodificacion = usuarioActual;
            tipoActividad.Fechamodificacion = DateTime.UtcNow;
            tipoActividad.Ipmodificacion = ipActual;

            _dbContext.TblTimeReportTipoActividads.Update(tipoActividad);
            await _dbContext.SaveChangesAsync();
            return new SuccessResponse("Tipo de actividad eliminado exitosamente.");
        }

        var detalle = await _dbContext.TblAdministracionCatalogoDetalles
            .FirstOrDefaultAsync(d => d.Id == id);

        if (detalle == null)
            throw new DetalleNoEncontradoException(id);

        // Soft Delete
        detalle.Activo = false;
        detalle.Usuariomodificacion = usuarioActual;
        detalle.Fechamodificacion = DateTime.UtcNow;
        detalle.Ipmodificacion = ipActual;

        _dbContext.TblAdministracionCatalogoDetalles.Update(detalle);
        await _dbContext.SaveChangesAsync();

        return new SuccessResponse("Detalle de catálogo eliminado exitosamente.");
    }

    public async Task<List<CatalogoDetalleConfigResponse>> ObtenerDetallesPorCatalogoIdAsync(int idCatalogo)
    {
        if (idCatalogo == -101)
        {
            return await _dbContext.TblTimeReportTipoProyectos
                .OrderBy(tp => tp.Nombretipo)
                .Select(tp => new CatalogoDetalleConfigResponse(
                    tp.Id,
                    -101,
                    tp.Id.ToString(),
                    tp.Nombretipo,
                    "Tipo de Proyecto",
                    null,
                    null,
                    tp.Activo
                ))
                .ToListAsync();
        }

        if (idCatalogo == -102)
        {
            return await _dbContext.TblTimeReportTipoActividads
                .OrderBy(ta => ta.Nombretipo)
                .Select(ta => new CatalogoDetalleConfigResponse(
                    ta.Id,
                    -102,
                    ta.Id.ToString(),
                    ta.Nombretipo,
                    ta.Descripcion,
                    null,
                    ta.Codigocolor,
                    ta.Activo
                ))
                .ToListAsync();
        }

        var catalogoExiste = await _dbContext.TblAdministracionCatalogos
            .AnyAsync(c => c.Id == idCatalogo);

        if (!catalogoExiste)
            throw new CatalogoNoEncontradoException(idCatalogo);

        return await _dbContext.TblAdministracionCatalogoDetalles
            .Where(d => d.Idcatalogo == idCatalogo)
            .OrderBy(d => d.Orden)
            .Select(d => new CatalogoDetalleConfigResponse(
                d.Id,
                d.Idcatalogo,
                d.Codigovalor,
                d.Valor,
                d.Descripcion,
                d.Orden,
                d.Valorextra,
                d.Activo
            ))
            .ToListAsync();
    }

    public async Task<List<CatalogoDetalleConfigResponse>> ObtenerDetallesPorCatalogoCodigoAsync(string codigoCatalogo)
    {
        if (codigoCatalogo.ToLower() == "tpr")
        {
            return await ObtenerDetallesPorCatalogoIdAsync(-101);
        }
        if (codigoCatalogo.ToLower() == "tac")
        {
            return await ObtenerDetallesPorCatalogoIdAsync(-102);
        }

        var catalogo = await _dbContext.TblAdministracionCatalogos
            .FirstOrDefaultAsync(c => c.Codigo.ToLower() == codigoCatalogo.ToLower());

        if (catalogo == null)
            throw new CatalogoNoEncontradoException(codigoCatalogo);

        return await _dbContext.TblAdministracionCatalogoDetalles
            .Where(d => d.Idcatalogo == catalogo.Id)
            .OrderBy(d => d.Orden)
            .Select(d => new CatalogoDetalleConfigResponse(
                d.Id,
                d.Idcatalogo,
                d.Codigovalor,
                d.Valor,
                d.Descripcion,
                d.Orden,
                d.Valorextra,
                d.Activo
            ))
            .ToListAsync();
    }

    public async Task<List<CatalogoMasterResponse>> ObtenerCatalogosAsync()
    {
        var dbCatalogos = await _dbContext.TblAdministracionCatalogos
            .Where(c => c.Codigo != "TPR" && c.Codigo != "TAC" 
                        && c.Tipocatalogo != "TIPO_PROYECTO" && c.Tipocatalogo != "TIPO_ACTIVIDAD"
                        && !c.Descripcion.ToLower().Contains("tipo de proyecto")
                        && !c.Descripcion.ToLower().Contains("tipo de actividad"))
            .OrderBy(c => c.Tipocatalogo)
            .ThenBy(c => c.Codigo)
            .Select(c => new CatalogoMasterResponse(
                c.Id,
                c.Tipocatalogo,
                c.Codigo,
                c.Descripcion,
                c.Activo
            ))
            .ToListAsync();

        dbCatalogos.Add(new CatalogoMasterResponse(-101, "TMR", "TPR", "Tipo de Proyecto", true));
        dbCatalogos.Add(new CatalogoMasterResponse(-102, "TMR", "TAC", "Tipo de Actividad", true));

        return dbCatalogos;
    }
}
