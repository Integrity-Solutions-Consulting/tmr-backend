namespace tmr_backend.Features.Usuarios.Domain;

/// <summary>
/// Entidad de dominio para Usuario.
/// Representa un usuario en el sistema (en memoria, no es una tabla de BD).
/// </summary>
public class Usuario
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public DateTime FechaCreacion { get; private set; }
    public bool Activo { get; private set; }

    private Usuario() { }

    /// <summary>
    /// Crea un nuevo usuario con los datos especificados.
    /// </summary>
    public static Usuario Crear(string nombre, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido.");

        return new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Descripcion = descripcion,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };
    }

    /// <summary>
    /// Actualiza los detalles del usuario (nombre y descripción).
    /// </summary>
    public void ActualizarDetalles(string nombre, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido.");

        Nombre = nombre;
        Descripcion = descripcion;
    }

    /// <summary>
    /// Desactiva el usuario.
    /// </summary>
    public void Desactivar()
    {
        Activo = false;
    }
}
