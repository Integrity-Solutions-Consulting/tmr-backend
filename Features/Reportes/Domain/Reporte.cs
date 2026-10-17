namespace tmr_backend.Features.Reportes.Domain;

public class Reporte
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public DateTime FechaCreacion { get; private set; }
    public bool Activo { get; private set; }

    private Reporte() { }

    public static Reporte Crear(string nombre, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido.");

        return new Reporte
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Descripcion = descripcion,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };
    }

    public void ActualizarDetalles(string nombre, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido.");

        Nombre = nombre;
        Descripcion = descripcion;
    }

    public void Desactivar()
    {
        Activo = false;
    }
}
