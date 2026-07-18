using System;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Proyectos.Domain;

public class Proyecto
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public int IdCliente { get; private set; }
    public int IdLider { get; private set; }
    public int IdEstadoProyecto { get; private set; }
    public bool Activo { get; private set; }
    
    // Auditoría
    public string Usuariocreacion { get; private set; } = string.Empty;
    public DateTime Fechacreacion { get; private set; }
    public string Ipcreacion { get; private set; } = string.Empty;
    public string? Usuariomodificacion { get; private set; }
    public DateTime? Fechamodificacion { get; private set; }
    public string? Ipmodificacion { get; private set; }

    // Nuevos campos
    public string? Observacion { get; private set; }
    public DateTime? FechaInicioReal { get; private set; }   // existe en BD, pero no se usa en creación/actualización por ahora
    public DateTime? FechaFinReal { get; private set; }
    public DateTime? FechaInicioEspera { get; private set; }
    public DateTime? FechaFinEspera { get; private set; }

    // Navegación
    public virtual TblAdministracionCliente? Cliente { get; private set; }
    public virtual TblAdministracionLider? Lider { get; private set; }
    public virtual TblAdministracionCatalogoDetalle? EstadoProyecto { get; private set; }

    private Proyecto() { }

    public static Proyecto Crear(
        string nombre,
        string descripcion,
        int idCliente,
        int idLider,
        int idEstadoProyecto,
        string usuario,
        string ip,
        string? observacion = null,
        DateTime? fechaInicioEspera = null,
        DateTime? fechaFinEspera = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido.");

        return new Proyecto
        {
            Nombre = nombre,
            Descripcion = descripcion,
            IdCliente = idCliente,
            IdLider = idLider,
            IdEstadoProyecto = idEstadoProyecto,
            Activo = true,
            Usuariocreacion = usuario,
            Fechacreacion = DateTime.UtcNow,
            Ipcreacion = ip,
            Observacion = observacion,
            FechaInicioEspera = fechaInicioEspera,
            FechaFinEspera = fechaFinEspera
            // FechaInicioReal y FechaFinReal no se asignan en creación
        };
    }

    public void Actualizar(
        string nombre,
        string descripcion,
        int idCliente,
        int idLider,
        int idEstadoProyecto,
        string usuario,
        string ip,
        string? observacion = null,
        DateTime? fechaFinReal = null,
        DateTime? fechaInicioEspera = null,
        DateTime? fechaFinEspera = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido.");

        Nombre = nombre;
        Descripcion = descripcion;
        IdCliente = idCliente;
        IdLider = idLider;
        IdEstadoProyecto = idEstadoProyecto;
        Usuariomodificacion = usuario;
        Fechamodificacion = DateTime.UtcNow;
        Ipmodificacion = ip;
        Observacion = observacion;
        FechaFinReal = fechaFinReal;
        FechaInicioEspera = fechaInicioEspera;
        FechaFinEspera = fechaFinEspera;
    }

    public void Desactivar(string usuario, string ip)
    {
        Activo = false;
        Usuariomodificacion = usuario;
        Fechamodificacion = DateTime.UtcNow;
        Ipmodificacion = ip;
    }
}