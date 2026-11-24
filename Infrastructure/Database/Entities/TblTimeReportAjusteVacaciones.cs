using System;

namespace tmr_backend.Infrastructure.Database.Entities;

public class TblTimeReportAjusteVacaciones
{
    public int Id { get; set; }
    public int IdEmpleado { get; set; }
    public string TipoAjuste { get; set; } = "AJUSTE_DIAS"; // AJUSTE_DIAS | EXCEPCION_FACTURA
    public decimal DiasAjuste { get; set; }
    public bool? PermiteVacaciones { get; set; }
    public string Motivo { get; set; } = null!;
    public bool Activo { get; set; } = true;
    public string UsuarioCreacion { get; set; } = null!;
    public DateTime FechaCreacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string IpCreacion { get; set; } = null!;
    public string? IpModificacion { get; set; }

    public virtual TblAdministracionEmpleado Empleado { get; set; } = null!;
}
