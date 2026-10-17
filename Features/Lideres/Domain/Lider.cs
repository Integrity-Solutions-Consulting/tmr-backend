namespace tmr_backend.Features.Lideres.Domain;

public class Lider
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Tipo { get; set; } = null!;
    public string PrimerNombre { get; set; } = null!;
    public string Apellidos { get; set; } = null!;
    public string? CorreoElectronico { get; set; }
    public string? Telefono { get; set; }
    public bool Activo { get; set; }
}
