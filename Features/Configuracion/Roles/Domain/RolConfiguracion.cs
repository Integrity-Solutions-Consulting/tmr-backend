using tmr_backend.Features.Configuracion.Roles.Domain.Exceptions;

namespace tmr_backend.Features.Configuracion.Roles.Domain;

public class RolConfiguracion
{
    public int Id { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public bool Activo { get; private set; }

    private List<int> _modulosIds = new();
    public IReadOnlyList<int> ModulosIds => _modulosIds.AsReadOnly();

    private RolConfiguracion() { }

    public static RolConfiguracion Crear(string nombre, string descripcion, List<int> modulosIds)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DatosInvalidosRolException("El nombre del rol es requerido.");
            
        if (string.IsNullOrWhiteSpace(descripcion))
            throw new DatosInvalidosRolException("La descripción del rol es requerida.");

        if (modulosIds == null || !modulosIds.Any())
            throw new DatosInvalidosRolException("Un rol debe tener al menos un módulo asignado.");

        return new RolConfiguracion
        {
            Nombre = nombre.Trim().ToUpper(),
            Descripcion = descripcion.Trim(),
            Activo = true,
            _modulosIds = modulosIds.Distinct().ToList()
        };
    }

    public void ActualizarDatos(string nombre, string descripcion, List<int> modulosIds)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new DatosInvalidosRolException("El nombre del rol es requerido.");
            
        if (string.IsNullOrWhiteSpace(descripcion))
            throw new DatosInvalidosRolException("La descripción del rol es requerida.");

        if (modulosIds == null || !modulosIds.Any())
            throw new DatosInvalidosRolException("Un rol debe tener al menos un módulo asignado.");

        Nombre = nombre.Trim().ToUpper();
        Descripcion = descripcion.Trim();
        _modulosIds = modulosIds.Distinct().ToList();
    }
}
