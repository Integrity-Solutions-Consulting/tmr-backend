namespace tmr_backend.Features.Lideres.Domain;

public class Lider
{
    public Guid Id { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Tipo { get; private set; } = string.Empty; // Interno / Externo
    public string PrimerNombre { get; private set; } = string.Empty;
    public string Apellidos { get; private set; } = string.Empty;
    public string CorreoElectronico { get; private set; } = string.Empty;
    public string Telefono { get; private set; } = string.Empty;
    public Guid? ClienteId { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public bool Activo { get; private set; }

    private Lider() { }

    public static Lider Crear(string codigo, string tipo, string primerNombre, string apellidos, string correo, string telefono, Guid? clienteId)
    {
        if (string.IsNullOrWhiteSpace(primerNombre))
            throw new ArgumentException("El nombre es requerido.");
        if (string.IsNullOrWhiteSpace(tipo))
            throw new ArgumentException("El tipo es requerido.");

        return new Lider
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            Tipo = tipo,
            PrimerNombre = primerNombre,
            Apellidos = apellidos,
            CorreoElectronico = correo,
            Telefono = telefono,
            ClienteId = clienteId,
            FechaCreacion = DateTime.UtcNow,
            Activo = true
        };
    }

    public void ActualizarDetalles(string tipo, string primerNombre, string apellidos, string correo, string telefono, Guid? clienteId, bool activo)
    {
        if (string.IsNullOrWhiteSpace(primerNombre))
            throw new ArgumentException("El nombre es requerido.");

        Tipo = tipo;
        PrimerNombre = primerNombre;
        Apellidos = apellidos;
        CorreoElectronico = correo;
        Telefono = telefono;
        ClienteId = clienteId;
        Activo = activo;
    }

    public void Desactivar()
    {
        Activo = false;
    }
}