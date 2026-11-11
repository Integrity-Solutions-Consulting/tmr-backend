using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public class TblTimeReportTipoPermiso
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string CodigoCorto { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    public string? Descripcion { get; set; }
    public bool RequiereAdjunto { get; set; }
    public short Orden { get; set; } = 1;
    public bool Activo { get; set; } = true;

    public virtual ICollection<TblTimeReportSolicitud> Solicitudes { get; set; } = new List<TblTimeReportSolicitud>();
}

