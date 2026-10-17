using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblTimeReportProyeccionHora
{
    public int Id { get; set; }

    public Guid? Grupoproyeccion { get; set; }

    public int Idtiporecurso { get; set; }

    public string Nombrerecurso { get; set; } = null!;

    public string Nombreproyeccion { get; set; } = null!;

    public decimal Costoporhora { get; set; }

    public int Cantidadrecurso { get; set; }

    public string Distribuciontiempo { get; set; } = null!;

    public decimal Tiempototal { get; set; }

    public decimal Costorecurso { get; set; }

    public decimal Porcentajeparticipacion { get; set; }

    public bool Tipoperiodo { get; set; }

    public int Cantidadperiodo { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }

    public string? Usuariomodificacion { get; set; }

    public DateTime? Fechamodificacion { get; set; }

    public string Ipcreacion { get; set; } = null!;

    public string? Ipmodificacion { get; set; }

    public virtual TblAdministracionCargo IdtiporecursoNavigation { get; set; } = null!;
}
