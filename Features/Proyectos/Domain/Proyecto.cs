using System;
using tmr_backend.Infrastructure.Database.Entities;

namespace tmr_backend.Features.Proyectos.Domain;

public class Proyecto
{
    public int Id { get; private set; } // Ajustado a int secuencial de Postgres
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public int IdCliente { get; private set; }
    public int IdLider { get; private set; }
    public int IdEstadoProyecto { get; private set; }
    public bool Activo { get; private set; }
    
    // Campos de Auditoría unificados con la base de datos
    public string Usuariocreacion { get; private set; } = string.Empty;
    public DateTime Fechacreacion { get; private set; }
    public string Ipcreacion { get; private set; } = string.Empty;
    public string? Usuariomodificacion { get; private set; }
    public DateTime? Fechamodificacion { get; private set; }
    public string? Ipmodificacion { get; private set; }

    // Propiedades de navegación apuntando al modelo común de infraestructura
    public virtual TblAdministracionCliente? Cliente { get; private set; }
    public virtual TblAdministracionLider? Lider { get; private set; }
    public virtual TblAdministracionCatalogoDetalle? EstadoProyecto { get; private set; }

    private Proyecto() { }

    public static Proyecto Crear(string nombre, string descripcion, int idCliente, int idLider, int idEstadoProyecto, string usuario, string ip)
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
            Ipcreacion = ip
        };
    }

    public void Actualizar(string nombre, string descripcion, int idCliente, int idLider, int idEstadoProyecto, string usuario, string ip)
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
    }

    public void Desactivar(string usuario, string ip)
    {
        Activo = false;
        Usuariomodificacion = usuario;
        Fechamodificacion = DateTime.UtcNow;
        Ipmodificacion = ip;
    }
}