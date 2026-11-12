using System.ComponentModel.DataAnnotations;

namespace tmr_backend.Features.ActualizacionDatos.Domain;

public enum EstadoActualizacion { Borrador, Enviada, Devuelta, Aprobada }

public sealed class ActualizacionSolicitud
{
    public long Id { get; set; }
    public int IdEmpleado { get; set; }
    public EstadoActualizacion Estado { get; set; }
    public string? Observacion { get; set; }
    public int? IdRevisor { get; set; }
    public DateTime Fechacreacion { get; set; }
    public DateTime Fechamodificacion { get; set; }
    public DateTime? FechaRevision { get; set; }
    public int Version { get; set; }
    public string DatosPropuestosJson { get; set; } = "{}";
    public ICollection<ActualizacionDetalle> Detalles { get; set; } = new List<ActualizacionDetalle>();
    public ICollection<ActualizacionFormacion> Formaciones { get; set; } = new List<ActualizacionFormacion>();
    public ICollection<ActualizacionCertificacion> Certificaciones { get; set; } = new List<ActualizacionCertificacion>();
    public ICollection<ActualizacionArchivo> Archivos { get; set; } = new List<ActualizacionArchivo>();
    public ICollection<ActualizacionFamiliar> Familiares { get; set; } = new List<ActualizacionFamiliar>();
}

public sealed class ActualizacionDetalle
{
    public long Id { get; set; }
    public long IdSolicitud { get; set; }
    public string Seccion { get; set; } = null!;
    public string Campo { get; set; } = null!;
    public string? ValorVigente { get; set; }
    public string? ValorPropuesto { get; set; }
    public ActualizacionSolicitud Solicitud { get; set; } = null!;
}

public sealed class ActualizacionFormacion
{
    public long Id { get; set; }
    public long IdSolicitud { get; set; }
    public string Nivel { get; set; } = null!;
    public string Institucion { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public ActualizacionSolicitud Solicitud { get; set; } = null!;
}

public sealed class ActualizacionCertificacion
{
    public long Id { get; set; }
    public long IdSolicitud { get; set; }
    public string Nombre { get; set; } = null!;
    public string EntidadEmisora { get; set; } = null!;
    public DateOnly FechaEmision { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public ActualizacionSolicitud Solicitud { get; set; } = null!;
}

public sealed class ColaboradorFormacion
{
    public long Id { get; set; }
    public int IdEmpleado { get; set; }
    public string Nivel { get; set; } = null!;
    public string Institucion { get; set; } = null!;
    public string Titulo { get; set; } = null!;
    public DateOnly? FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }
    public bool Activo { get; set; }
    public string Clave { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed class ColaboradorCertificacion
{
    public long Id { get; set; }
    public int IdEmpleado { get; set; }
    public string Nombre { get; set; } = null!;
    public string EntidadEmisora { get; set; } = null!;
    public DateOnly FechaEmision { get; set; }
    public DateOnly? FechaVencimiento { get; set; }
    public bool Activo { get; set; }
    public string Clave { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed class ActualizacionArchivo
{
    public long Id { get; set; }
    public long IdSolicitud { get; set; }
    public string NombreOriginal { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Tamano { get; set; }
    public string StorageKey { get; set; } = null!;
    public string Provider { get; set; } = "local";
    public string Sha256 { get; set; } = null!;
    public DateTime Fechacreacion { get; set; }
    public string Seccion { get; set; } = "General";
    public long? ReferenciaId { get; set; }
    public ActualizacionSolicitud Solicitud { get; set; } = null!;
}

public sealed class ColaboradorInformacionFamiliar
{
    public int IdEmpleado { get; set; }
    public string? EstadoCivil { get; set; }
    public string? TieneHijos { get; set; }
    public string? TieneDiscapacidad { get; set; }
    public string? TipoDiscapacidad { get; set; }
    public decimal? PorcentajeDiscapacidad { get; set; }
    public ICollection<ColaboradorFamiliar> Familiares { get; set; } = new List<ColaboradorFamiliar>();
}

public sealed class ColaboradorFamiliar
{
    public long Id { get; set; }
    public int IdEmpleado { get; set; }
    public string TipoRelacion { get; set; } = null!;
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public DateOnly? FechaNacimiento { get; set; }
    public string? TieneDiscapacidad { get; set; }
    public string? TipoDiscapacidad { get; set; }
    public decimal? PorcentajeDiscapacidad { get; set; }
    public bool Activo { get; set; }
    public string? Identificacion { get; set; }
    public string Clave { get; set; } = Guid.NewGuid().ToString("N");
    public ColaboradorInformacionFamiliar? Informacion { get; set; }
}

public sealed class ColaboradorDatosControl
{
    public int IdEmpleado { get; set; }
    public long Version { get; set; }
    public DateTime FechaModificacion { get; set; }
}

public sealed class ColaboradorDocumento
{
    public long Id { get; set; }
    public int IdEmpleado { get; set; }
    public string Seccion { get; set; } = null!;
    public long? ReferenciaId { get; set; }
    public string? ReferenciaClave { get; set; }
    public string NombreOriginal { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Tamano { get; set; }
    public string StorageKey { get; set; } = null!;
    public string Provider { get; set; } = "local";
    public string Sha256 { get; set; } = null!;
    public DateTime FechaCreacion { get; set; }
    public bool Activo { get; set; }
}

public sealed class ColaboradorSolicitud
{
    public long Id { get; set; }
    public int IdEmpleado { get; set; }
    public string Asunto { get; set; } = null!;
    public string Descripcion { get; set; } = null!;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaEnvio { get; set; }
    public bool Enviada { get; set; }
    public ICollection<ColaboradorSolicitudArchivo> Archivos { get; set; } = new List<ColaboradorSolicitudArchivo>();
}

public sealed class ColaboradorSolicitudArchivo
{
    public long Id { get; set; }
    public long IdSolicitud { get; set; }
    public string NombreOriginal { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long Tamano { get; set; }
    public string StorageKey { get; set; } = null!;
    public string Provider { get; set; } = "local";
    public string Sha256 { get; set; } = null!;
    public DateTime FechaCreacion { get; set; }
    public ColaboradorSolicitud Solicitud { get; set; } = null!;
}

public sealed class ActualizacionFamiliar
{
    public long Id { get; set; }
    public long IdSolicitud { get; set; }
    public string TipoRelacion { get; set; } = null!;
    public string Nombres { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public DateOnly? FechaNacimiento { get; set; }
    public string? TieneDiscapacidad { get; set; }
    public string? TipoDiscapacidad { get; set; }
    public decimal? PorcentajeDiscapacidad { get; set; }
    public ActualizacionSolicitud Solicitud { get; set; } = null!;
}
