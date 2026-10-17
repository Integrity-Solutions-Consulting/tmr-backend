using tmr_backend.Features.Configuracion.Usuarios.Domain.Exceptions;

namespace tmr_backend.Features.Configuracion.Usuarios.Domain;

/// <summary>
/// Entidad de dominio para Usuario de Configuración.
/// Representa un usuario del sistema con datos personales, credenciales y roles.
/// 
/// Esta entidad respeta DDD encapsulando la lógica de negocio:
/// - Validación de datos
/// - Lanzamiento de excepciones de dominio
/// - Factory methods para creación segura
/// </summary>
public class UsuarioConfiguracion
{
    // ─────────────────────────────────────────────────────────────────────
    // PROPIEDADES - Datos de tbl_administracion_persona
    // ─────────────────────────────────────────────────────────────────────
    public int IdPersona { get; private set; }
    public string NumeroIdentificacion { get; private set; } = string.Empty;
    public string Nombres { get; private set; } = string.Empty;
    public string Apellidos { get; private set; } = string.Empty;
    public int? IdTipoIdentificacion { get; private set; }
    public int? IdGenero { get; private set; }
    public int? IdNacionalidad { get; private set; }
    public DateOnly? FechaNacimiento { get; private set; }
    public string? Telefono { get; private set; }
    public string? Direccion { get; private set; }

    // ─────────────────────────────────────────────────────────────────────
    // PROPIEDADES - Datos de tbl_autenticacion_usuario
    // ─────────────────────────────────────────────────────────────────────
    public int IdUsuarioAuth { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string HashPassword { get; private set; } = string.Empty;
    public bool DebeCambiarPassword { get; private set; }
    public DateTime? UltimoLogin { get; private set; }
    public bool Activo { get; private set; }

    // ─────────────────────────────────────────────────────────────────────
    // PROPIEDADES - Auditoría
    // ─────────────────────────────────────────────────────────────────────
    public string UsuarioCreacion { get; private set; } = string.Empty;
    public DateTime FechaCreacion { get; private set; }
    public string? IpCreacion { get; private set; }
    public string? UsuarioModificacion { get; private set; }
    public DateTime? FechaModificacion { get; private set; }
    public string? IpModificacion { get; private set; }

    // ─────────────────────────────────────────────────────────────────────
    // LISTA DE ROLES (En memoria, sincronizada con BD)
    // ─────────────────────────────────────────────────────────────────────
    private List<int> _rolesIds = new();
    public IReadOnlyList<int> RolesIds => _rolesIds.AsReadOnly();

    // ─────────────────────────────────────────────────────────────────────
    // CONSTRUCTOR PRIVADO - Fuerza uso de factory methods
    // ─────────────────────────────────────────────────────────────────────
    private UsuarioConfiguracion() { }

    // ─────────────────────────────────────────────────────────────────────
    // FACTORY METHOD - Crear nuevo usuario
    // ─────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Crea un nuevo usuario con validación completa de datos.
    /// Lanza excepciones si algún dato no es válido.
    /// </summary>
    public static UsuarioConfiguracion Crear(
        string numeroidentificacion,
        string nombres,
        string apellidos,
        string email,
        string hashPassword,
        int? idTipoIdentificacion = null,
        int? idGenero = null,
        int? idNacionalidad = null,
        DateOnly? fechaNacimiento = null,
        string? telefono = null,
        string? direccion = null,
        string usuarioCreacion = "SYSTEM",
        string? ipCreacion = null,
        List<int>? rolesIds = null)
    {
        // Validar datos requeridos
        if (string.IsNullOrWhiteSpace(numeroidentificacion))
            throw new DatosInvalidosException(nameof(numeroidentificacion));

        if (string.IsNullOrWhiteSpace(nombres))
            throw new DatosInvalidosException(nameof(nombres));

        if (string.IsNullOrWhiteSpace(apellidos))
            throw new DatosInvalidosException(nameof(apellidos));

        if (string.IsNullOrWhiteSpace(email))
            throw new DatosInvalidosException(nameof(email));

        if (string.IsNullOrWhiteSpace(hashPassword))
            throw new DatosInvalidosException(nameof(hashPassword));

        // Validar formato de email
        if (!EsEmailValido(email))
            throw new EmailInvalidoException(email);

        // Validar longitud mínima
        if (nombres.Length < 3 || nombres.Length > 100)
            throw new DatosInvalidosException($"{nameof(nombres)} debe tener entre 3 y 100 caracteres");

        if (apellidos.Length < 3 || apellidos.Length > 100)
            throw new DatosInvalidosException($"{nameof(apellidos)} debe tener entre 3 y 100 caracteres");

        // Validar identificación
        if (numeroidentificacion.Length < 8 || numeroidentificacion.Length > 20)
            throw new DatosInvalidosException($"{nameof(numeroidentificacion)} debe tener entre 8 y 20 caracteres");

        // Validar roles
        if (rolesIds == null || rolesIds.Count == 0)
            throw new UsuarioSinRolesException();

        return new UsuarioConfiguracion
        {
            NumeroIdentificacion = numeroidentificacion.Trim(),
            Nombres = nombres.Trim(),
            Apellidos = apellidos.Trim(),
            Email = email.Trim().ToLower(),
            HashPassword = hashPassword,
            DebeCambiarPassword = true,
            Activo = true,
            IdTipoIdentificacion = idTipoIdentificacion,
            IdGenero = idGenero,
            IdNacionalidad = idNacionalidad,
            FechaNacimiento = fechaNacimiento,
            Telefono = telefono?.Trim(),
            Direccion = direccion?.Trim(),
            UsuarioCreacion = usuarioCreacion,
            FechaCreacion = DateTime.UtcNow,
            IpCreacion = ipCreacion,
            _rolesIds = rolesIds.ToList()
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // MÉTODOS DE NEGOCIO
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Actualiza los datos personales del usuario.
    /// NO permite cambiar email ni numeroIdentificacion (son inmutables).
    /// </summary>
    public void ActualizarDatos(
        string nombres,
        string apellidos,
        int? idGenero = null,
        int? idNacionalidad = null,
        DateOnly? fechaNacimiento = null,
        string? telefono = null,
        string? direccion = null,
        string usuarioModificacion = "SYSTEM",
        string? ipModificacion = null)
    {
        // Validar datos requeridos
        if (string.IsNullOrWhiteSpace(nombres))
            throw new DatosInvalidosException(nameof(nombres));

        if (string.IsNullOrWhiteSpace(apellidos))
            throw new DatosInvalidosException(nameof(apellidos));

        if (nombres.Length < 3 || nombres.Length > 100)
            throw new DatosInvalidosException($"{nameof(nombres)} debe tener entre 3 y 100 caracteres");

        if (apellidos.Length < 3 || apellidos.Length > 100)
            throw new DatosInvalidosException($"{nameof(apellidos)} debe tener entre 3 y 100 caracteres");

        Nombres = nombres.Trim();
        Apellidos = apellidos.Trim();
        IdGenero = idGenero;
        IdNacionalidad = idNacionalidad;
        FechaNacimiento = fechaNacimiento;
        Telefono = telefono?.Trim();
        Direccion = direccion?.Trim();
        UsuarioModificacion = usuarioModificacion;
        FechaModificacion = DateTime.UtcNow;
        IpModificacion = ipModificacion;
    }

    /// <summary>
    /// Asigna nuevos roles al usuario.
    /// Reemplaza completamente los roles anteriores.
    /// </summary>
    public void AsignarRoles(
        List<int> rolesIds,
        string usuarioModificacion = "SYSTEM",
        string? ipModificacion = null)
    {
        if (rolesIds == null || rolesIds.Count == 0)
            throw new UsuarioSinRolesException();

        _rolesIds = rolesIds.ToList();
        UsuarioModificacion = usuarioModificacion;
        FechaModificacion = DateTime.UtcNow;
        IpModificacion = ipModificacion;
    }

    /// <summary>
    /// Cambia la contraseña del usuario.
    /// Valida que no sea igual a la actual y genera un nuevo hash.
    /// </summary>
    public void CambiarPassword(
        string nuevoHashPassword,
        string usuarioModificacion = "SYSTEM",
        string? ipModificacion = null)
    {
        if (string.IsNullOrWhiteSpace(nuevoHashPassword))
            throw new DatosInvalidosException(nameof(nuevoHashPassword));

        // Validar que no sea igual al hash anterior (comparación básica)
        if (nuevoHashPassword == HashPassword)
            throw new PasswordIgualAlActualException();

        HashPassword = nuevoHashPassword;
        DebeCambiarPassword = false;
        UsuarioModificacion = usuarioModificacion;
        FechaModificacion = DateTime.UtcNow;
        IpModificacion = ipModificacion;
    }

    /// <summary>
    /// Marca el usuario como inactivo (soft delete).
    /// El usuario no podrá autenticarse después de esto.
    /// </summary>
    public void Desactivar(
        string usuarioModificacion = "SYSTEM",
        string? ipModificacion = null)
    {
        Activo = false;
        UsuarioModificacion = usuarioModificacion;
        FechaModificacion = DateTime.UtcNow;
        IpModificacion = ipModificacion;
    }

    /// <summary>
    /// Reactiva un usuario que fue desactivado.
    /// </summary>
    public void Reactivar(
        string usuarioModificacion = "SYSTEM",
        string? ipModificacion = null)
    {
        Activo = true;
        UsuarioModificacion = usuarioModificacion;
        FechaModificacion = DateTime.UtcNow;
        IpModificacion = ipModificacion;
    }

    /// <summary>
    /// Registra el último login del usuario.
    /// </summary>
    public void RegistrarUltimoLogin(string? ipModificacion = null)
    {
        UltimoLogin = DateTime.UtcNow;
        IpModificacion = ipModificacion;
    }

    /// <summary>
    /// Valida que el usuario pueda realizar login.
    /// </summary>
    public bool PuedeIniciarSesion()
    {
        return Activo && HashPassword != string.Empty;
    }

    // ─────────────────────────────────────────────────────────────────────
    // MÉTODOS AUXILIARES
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Valida el formato básico de un email.
    /// </summary>
    private static bool EsEmailValido(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Obtiene el nombre completo del usuario.
    /// </summary>
    public string ObtenerNombreCompleto() => $"{Nombres} {Apellidos}".Trim();
}
