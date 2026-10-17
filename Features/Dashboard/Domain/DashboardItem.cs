namespace tmr_backend.Features.Dashboard.Domain;

public class DashboardItem
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public DateTime FechaCreacion { get; private set; }
    public bool Activo { get; private set; }

    private DashboardItem() { }

    public static DashboardItem Crear(string nombre, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es requerido.");

        return new DashboardItem
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
