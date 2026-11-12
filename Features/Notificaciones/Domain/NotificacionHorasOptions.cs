namespace tmr_backend.Features.Notificaciones.Domain;

/// <summary>
/// Sección NotificacionHoras de appsettings (en Dokploy: NotificacionHoras__Habilitado, etc.).
/// Las fechas de corte no van aquí: están en notificacion.notificacion_fecha_corte y se editan
/// desde la pantalla Fechas de Corte.
/// </summary>
public class NotificacionHorasOptions
{
    /// <summary>Interruptor general de los cortes automáticos (flujos 1 y 3).</summary>
    public bool Habilitado { get; set; }

    /// <summary>Horas mínimas entre dos avisos manuales al mismo colaborador y proyecto (flujo 2).</summary>
    public int UmbralNotificacionManual { get; set; }
}
