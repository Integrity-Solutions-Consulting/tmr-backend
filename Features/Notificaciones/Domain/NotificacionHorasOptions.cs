namespace TmrBackend.Features.Notificaciones.Domain;

public class NotificacionHorasOptions
{
    public bool Habilitado { get; set; }
    public DateOnly InicioPeriodo { get; set; }
    public DateOnly FinPeriodo { get; set; }
    public DateTimeOffset FechaCorte { get; set; }
    public int UmbralNotificacionManual { get; set; }
}