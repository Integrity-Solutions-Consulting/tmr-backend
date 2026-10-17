namespace tmr_backend.Features.Notificaciones.Domain;

public class NotificacionFechaCorte
{
    public int Id { get; set; }
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public DateOnly InicioPeriodo { get; set; }
    public DateOnly FinPeriodo { get; set; }
    public DateTimeOffset FechaCorte { get; set; }
    public bool Ejecutada { get; set; }
    public DateTimeOffset? FechaEjecucion { get; set; }
    public int IdUsuarioCreador { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }
}

