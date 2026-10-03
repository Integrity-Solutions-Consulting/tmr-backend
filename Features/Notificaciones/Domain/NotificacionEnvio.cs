namespace TmrBackend.Features.Notificaciones.Domain;

public class NotificacionEnvio
{
    public int Id { get; set; }
    public int IdEmpleado { get; set; }
    public DateOnly InicioPeriodo { get; set; }
    public DateOnly FinPeriodo { get; set; }
    public DateTimeOffset? FechaCorte { get; set; }       // null en los envíos manuales
    public OrigenNotificacion Origen { get; set; }
    public EstadoEnvio Estado { get; set; }
    public decimal HorasFaltantes { get; set; }
    public int? IdUsuarioEjecutor { get; set; }           // líder que pidió el envío manual
    public DateTimeOffset FechaEnvio { get; set; }
    public string? DetalleError { get; set; }
}