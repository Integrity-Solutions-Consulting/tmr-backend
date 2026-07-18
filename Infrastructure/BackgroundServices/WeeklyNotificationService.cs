using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using tmr_backend.Infrastructure.Database;
using tmr_backend.Infrastructure.Database.Entities;
using tmr_backend.Infrastructure.Shared;

namespace tmr_backend.Infrastructure.BackgroundServices;

public sealed class WeeklyNotificationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklyNotificationService> _logger;
    private readonly IConfiguration _configuration;
    private DateTime? _lastRunDate;

    public WeeklyNotificationService(
        IServiceScopeFactory scopeFactory,
        ILogger<WeeklyNotificationService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WeeklyNotificationService iniciado.");

        // Espera inicial de 1 minuto para dar tiempo al inicio y estabilización del backend
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var settings = new WeeklyNotificationSettings();
                _configuration.GetSection("WeeklyNotificationSettings").Bind(settings);

                if (settings.Enabled)
                {
                    var now = DateTime.Now;
                    if (Enum.TryParse<DayOfWeek>(settings.DayOfWeek, true, out var targetDay))
                    {
                        // Verificar si estamos en el día de la semana y hora correctos, 
                        // y asegurarnos de no ejecutar el envío repetidas veces en el mismo día.
                        if (now.DayOfWeek == targetDay && 
                            now.Hour == settings.Hour && 
                            now.Minute >= settings.Minute && 
                            (!_lastRunDate.HasValue || _lastRunDate.Value.Date != now.Date))
                        {
                            _logger.LogInformation("WeeklyNotificationService: Se cumple la regla de programación semanal. Iniciando proceso de alertas automáticas.");
                            await SendWeeklyNotificationsAsync(stoppingToken);
                            _lastRunDate = now;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("WeeklyNotificationService: El valor de DayOfWeek '{Day}' configurado no es válido.", settings.DayOfWeek);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WeeklyNotificationService: Excepción capturada durante el ciclo de control.");
            }

            // Intervalo configurable de chequeo
            var intervalMin = _configuration.GetValue("WeeklyNotificationSettings:CheckIntervalMinutes", 5);
            if (intervalMin <= 0) intervalMin = 5;

            await Task.Delay(TimeSpan.FromMinutes(intervalMin), stoppingToken);
        }

        _logger.LogInformation("WeeklyNotificationService detenido.");
    }

    private async Task SendWeeklyNotificationsAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var hoy = DateTime.UtcNow.Date;
        var endDate = DateOnly.FromDateTime(hoy);
        var startDate = new DateOnly(hoy.Year, hoy.Month, 1);

        // 1. Obtener feriados activos en el mes actual hasta hoy
        var feriados = await db.TblTimeReportFeriados
            .Where(f => f.Activo && f.Fechaferiado >= startDate && f.Fechaferiado <= endDate)
            .Select(f => f.Fechaferiado)
            .ToListAsync(ct);

        // 2. Obtener colaboradores activos
        var empleados = await db.TblAdministracionEmpleados
            .Include(e => e.IdpersonaNavigation)
            .Where(e => e.Activo)
            .ToListAsync(ct);

        if (!empleados.Any())
        {
            _logger.LogInformation("WeeklyNotificationService: No se encontraron empleados activos para evaluar.");
            return;
        }

        var empIds = empleados.Select(e => e.Id).ToList();

        // 3. Obtener actividades de todos los colaboradores en el mes
        var actividades = await db.TblTimeReportActividadDiaria
            .Where(a => a.Activo && empIds.Contains(a.Idempleado) && a.Fechaactividad >= startDate && a.Fechaactividad <= endDate)
            .Select(a => new { a.Idempleado, a.Fechaactividad, a.Cantidadhoras })
            .ToListAsync(ct);

        var horasPorDia = actividades
            .GroupBy(a => new { a.Idempleado, a.Fechaactividad })
            .ToDictionary(
                g => (g.Key.Idempleado, g.Key.Fechaactividad),
                g => g.Sum(a => a.Cantidadhoras)
            );

        int totalCorreosEnviados = 0;

        foreach (var empleado in empleados)
        {
            var persona = empleado.IdpersonaNavigation;
            if (persona == null) continue;

            var emailDestino = empleado.Emailcorporativo ?? persona.Email;
            if (string.IsNullOrEmpty(emailDestino))
            {
                _logger.LogWarning("WeeklyNotificationService: Colaborador {Nombre} (ID: {EmpId}) no tiene correo electrónico configurado. Omitiendo notificación.", 
                    $"{persona.Nombres} {persona.Apellidos}".Trim(), empleado.Id);
                continue;
            }

            // Determinar rango de chequeo de este empleado
            var startCheck = startDate;
            if (empleado.Fechaingreso.HasValue && empleado.Fechaingreso.Value > startDate)
            {
                startCheck = empleado.Fechaingreso.Value;
            }

            var endCheck = endDate;
            if (empleado.Fechaterminacion.HasValue && empleado.Fechaterminacion.Value < endDate)
            {
                endCheck = empleado.Fechaterminacion.Value;
            }

            if (startCheck > endCheck) continue;

            var diasIncompletos = new List<(DateOnly Fecha, string Dia, decimal Faltantes)>();
            decimal totalExpected = 0m;
            decimal totalRegistered = 0m;

            var culture = new CultureInfo("es-ES");

            for (var date = startCheck; date <= endCheck; date = date.AddDays(1))
            {
                var dayOfWeek = new DateTime(date.Year, date.Month, date.Day).DayOfWeek;
                var isWeekend = dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
                var isFeriado = feriados.Contains(date);

                if (!isWeekend && !isFeriado)
                {
                    totalExpected += 8m;

                    horasPorDia.TryGetValue((empleado.Id, date), out var logged);
                    totalRegistered += logged;

                    if (logged < 8m)
                    {
                        var diaSemana = culture.DateTimeFormat.GetDayName(dayOfWeek);
                        diaSemana = culture.TextInfo.ToTitleCase(diaSemana);
                        diasIncompletos.Add((date, diaSemana, 8m - logged));
                    }
                }
            }

            if (diasIncompletos.Any())
            {
                var faltantes = totalExpected - totalRegistered;
                var nombreColaborador = $"{persona.Nombres} {persona.Apellidos}".Trim();

                // Construcción de filas de detalle en HTML
                var filasHtml = string.Join("", diasIncompletos.Select(d => 
                    $"<tr style='border-bottom: 1px solid #e2e8f0;'>" +
                    $"<td style='padding: 10px 8px; color: #334155; font-size: 13px;'>{d.Fecha:dd/MM/yyyy}</td>" +
                    $"<td style='padding: 10px 8px; color: #475569; font-size: 13px;'>{d.Dia}</td>" +
                    $"<td style='padding: 10px 8px; text-align: right; font-weight: bold; color: #ef4444; font-size: 13px;'>{d.Faltantes}h</td>" +
                    $"</tr>"
                ));

                var subject = "Recordatorio: Registro de horas pendientes";
                var frontendUrl = _configuration["EmailSettings:FrontendUrl"] ?? "http://localhost:3000";
                var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e5e7eb; border-radius: 8px;'>
                    <div style='text-align: center; margin-bottom: 24px;'>
                        <h2 style='color: #163572; margin: 0;'>ISC Time Report</h2>
                        <p style='color: #64748b; margin: 4px 0 0 0;'>Recordatorio de Horas Pendientes</p>
                    </div>
                    <div style='background-color: #f8fafc; padding: 16px; border-radius: 6px; margin-bottom: 24px; border-left: 4px solid #ef4444;'>
                        <p style='margin: 0; font-size: 16px; color: #1e293b;'>Hola <strong>{nombreColaborador}</strong>,</p>
                        <p style='margin: 12px 0 0 0; font-size: 14px; color: #475569; line-height: 1.5;'>
                            Se ha detectado que tienes un registro de horas pendiente en el sistema.
                        </p>
                        <p style='margin: 12px 0 0 0; font-size: 14px; color: #475569;'>
                            Horas faltantes: <strong style='color: #ef4444; font-size: 16px;'>{faltantes} horas</strong>
                        </p>
                    </div>
                    <p style='font-size: 14px; color: #475569; line-height: 1.5;'>
                        Por favor, ingresa a la plataforma de Time Report a la brevedad para completar tus registros diarios de actividades.
                    </p>
                    <div style='text-align: center; margin: 28px 0 12px 0;'>
                        <a href='{frontendUrl}' style='background-color: #163572; color: #ffffff; padding: 10px 20px; text-decoration: none; border-radius: 4px; font-weight: bold; font-size: 14px;'>Ir a Time Report</a>
                    </div>
                    <hr style='border: 0; border-top: 1px solid #e5e7eb; margin: 24px 0;' />
                    <p style='font-size: 11px; color: #94a3b8; text-align: center; margin: 0;'>
                        Este es un correo automático, por favor no respondas a este mensaje.
                    </p>
                </div>";

                try
                {
                    await emailService.SendEmailAsync(emailDestino, subject, body);
                    totalCorreosEnviados++;
                    _logger.LogInformation("WeeklyNotificationService: Alerta enviada con éxito a {Email}", emailDestino);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WeeklyNotificationService: Error al enviar alerta a {Email}", emailDestino);
                }
            }
        }

        _logger.LogInformation("WeeklyNotificationService: Ciclo de alertas automáticas finalizado. Total correos enviados: {Count}", totalCorreosEnviados);
    }
}
