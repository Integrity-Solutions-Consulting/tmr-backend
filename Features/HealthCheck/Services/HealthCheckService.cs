using tmr_backend.Features.HealthCheck.DTOs;
using tmr_backend.Infrastructure.Database;

namespace tmr_backend.Features.HealthCheck.Services;

public class HealthCheckService : IHealthCheckService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(ApplicationDbContext context, ILogger<HealthCheckService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<HealthCheckResponse> CheckHealthAsync()
    {
        var response = new HealthCheckResponse();

        try
        {
            // Verificar conexión a la base de datos
            var isConnected = await _context.Database.CanConnectAsync();

            if (!isConnected)
            {
                response.Status = "Unhealthy";
                response.Message = "No se pudo conectar a la base de datos";
                response.Database.IsConnected = false;
                response.Database.ConnectionStatus = "DESCONECTADO";
                return response;
            }

            response.Database.IsConnected = true;
            response.Database.ConnectionStatus = "CONECTADO";

            // Obtener conteos de tablas principales
            response.Database.TableRecordCounts = new Dictionary<string, int>
            {
                ["tbl_administracion_catalogo"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblAdministracionCatalogos),
                ["tbl_administracion_cargo"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblAdministracionCargos),
                ["tbl_administracion_cliente"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblAdministracionClientes),
                ["tbl_administracion_empleado"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblAdministracionEmpleados),
                ["tbl_administracion_persona"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblAdministracionPersonas),
                ["tbl_autenticacion_usuario"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblAutenticacionUsuarios),
                ["tbl_autenticacion_rol"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblAutenticacionUsuarioRols),
                ["tbl_inventario_equipo"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblInventarioEquipos),
                ["tbl_inventario_proveedor"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblInventarioProveedors),
                ["tbl_time_report_proyecto"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblTimeReportProyectos),
                ["tbl_time_report_actividad_diaria"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblTimeReportActividadDiaria),
                ["tbl_time_report_permiso"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblTimeReportPermisos),
                ["tbl_administracion_catalogo"] = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(_context.TblAdministracionCatalogos),
            };

            response.Status = "Healthy";
            response.Message = "Conexión exitosa a la base de datos";
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error en health check: {ex.Message}");
            response.Status = "Unhealthy";
            response.Message = "Error durante el health check";
            response.Database.IsConnected = false;
            response.Database.ConnectionStatus = "ERROR";
            response.Database.ErrorMessage = ex.Message;
            return response;
        }
    }

    public async Task<HealthCheckLiveResponse> CheckLiveAsync()
    {
        var response = new HealthCheckLiveResponse();

        try
        {
            // Verificar conexión rápida a la base de datos (sin contar registros)
            var isConnected = await _context.Database.CanConnectAsync();

            if (!isConnected)
            {
                response.Status = "Unhealthy";
                response.Message = "Aplicación no disponible - sin acceso a base de datos";
                return response;
            }

            response.Status = "Healthy";
            response.Message = "Aplicación funcionando";
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error en liveness check: {ex.Message}");
            response.Status = "Unhealthy";
            response.Message = "Aplicación no disponible - error interno";
            return response;
        }
    }
}
