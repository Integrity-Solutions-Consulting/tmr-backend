using System;

namespace tmr_backend.Infrastructure.Database.Entities;

/// <summary>
/// sm - Historial del tipo de contrato del empleado con su vigencia (script 11). FechaHasta null = vigente.
/// </summary>
public partial class TblAdministracionEmpleadoContrato
{
    public int Id { get; set; }

    public int Idempleado { get; set; }

    public int Idtipocontrato { get; set; }

    public DateOnly Fechadesde { get; set; }

    public DateOnly? Fechahasta { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string? Ipmodificacion { get; set; }
}
