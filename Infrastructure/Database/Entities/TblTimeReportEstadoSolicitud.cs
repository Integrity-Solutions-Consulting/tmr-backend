using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public class TblTimeReportEstadoSolicitud
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string CodigoCorto { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string Semaforo { get; set; } = null!;
    public short Orden { get; set; }
    public bool Activo { get; set; } = true;

    public virtual ICollection<TblTimeReportSolicitud> Solicitudes { get; set; } = new List<TblTimeReportSolicitud>();
}

