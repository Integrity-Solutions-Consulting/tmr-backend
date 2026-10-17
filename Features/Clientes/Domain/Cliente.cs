namespace tmr_backend.Features.Clientes.Domain;

// En Domain Driven Design, las entidades deben tener un estado válido y controlar sus propias invariantes.
// Esta es la raíz de agregado (Aggregate Root) para el feature Clientes.
public class Cliente
{
    public Guid Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Empresa { get; private set; } = string.Empty;
    public DateTime FechaCreacion { get; private set; }
    public bool Activo { get; private set; }

    // Constructor privado para EF Core
    private Cliente() { }

    // Constructor de fábrica
    public static Cliente Crear(string nombre, string empresa)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del cliente es requerido.");

        return new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = nombre,
            Empresa = empresa,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };
    }

    public void ActualizarDetalles(string nombre, string empresa)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del cliente es requerido.");

        Nombre = nombre;
        Empresa = empresa;
    }

    public void Desactivar()
    {
        Activo = false;
    }
}
