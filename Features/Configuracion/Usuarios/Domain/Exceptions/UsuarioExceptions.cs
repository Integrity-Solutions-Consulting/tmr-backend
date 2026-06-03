namespace tmr_backend.Features.Configuracion.Usuarios.Domain.Exceptions;

/// <summary>
/// Excepción lanzada cuando se intenta crear un usuario con un email que ya existe.
/// </summary>
public class UsuarioEmailYaExisteException : DomainException
{
    public string Email { get; set; }

    public UsuarioEmailYaExisteException(string email) 
        : base($"Ya existe un usuario registrado con el email '{email}'.", "USUARIO_EMAIL_EXISTE")
    {
        Email = email;
    }
}

/// <summary>
/// Excepción lanzada cuando se intenta crear un usuario con un número de identificación que ya existe.
/// </summary>
public class UsuarioIdentificacionYaExisteException : DomainException
{
    public string NumeroIdentificacion { get; set; }

    public UsuarioIdentificacionYaExisteException(string numeroidentificacion) 
        : base($"Ya existe un usuario registrado con el número de identificación '{numeroidentificacion}'.", 
            "USUARIO_IDENTIFICACION_EXISTE")
    {
        NumeroIdentificacion = numeroidentificacion;
    }
}

/// <summary>
/// Excepción lanzada cuando se intenta operar sobre un usuario que no existe.
/// </summary>
public class UsuarioNoEncontradoException : DomainException
{
    public int UsuarioId { get; set; }

    public UsuarioNoEncontradoException(int usuarioId) 
        : base($"No se encontró el usuario con ID '{usuarioId}'.", "USUARIO_NO_ENCONTRADO")
    {
        UsuarioId = usuarioId;
    }
}

/// <summary>
/// Excepción lanzada cuando el password no cumple con los requisitos de complejidad.
/// </summary>
public class PasswordDebilException : DomainException
{
    public PasswordDebilException() 
        : base(
            "La contraseña debe contener: mínimo 8 caracteres, una mayúscula, una minúscula, un número y un carácter especial (!@#$%^&*).",
            "PASSWORD_DEBIL")
    {
    }
}

/// <summary>
/// Excepción lanzada cuando la contraseña actual no coincide durante un cambio de password.
/// </summary>
public class PasswordIncorrectoException : DomainException
{
    public PasswordIncorrectoException() 
        : base("La contraseña actual es incorrecta.", "PASSWORD_INCORRECTO")
    {
    }
}

/// <summary>
/// Excepción lanzada cuando las contraseñas de confirmación no coinciden.
/// </summary>
public class PasswordNoCoincideException : DomainException
{
    public PasswordNoCoincideException() 
        : base("Las contraseñas no coinciden.", "PASSWORD_NO_COINCIDE")
    {
    }
}

/// <summary>
/// Excepción lanzada cuando el rol a asignar no existe.
/// </summary>
public class RolNoEncontradoException : DomainException
{
    public int RolId { get; set; }

    public RolNoEncontradoException(int rolId) 
        : base($"No se encontró el rol con ID '{rolId}'.", "ROL_NO_ENCONTRADO")
    {
        RolId = rolId;
    }
}

/// <summary>
/// Excepción lanzada cuando se intenta asignar cero roles a un usuario.
/// </summary>
public class UsuarioSinRolesException : DomainException
{
    public UsuarioSinRolesException() 
        : base("Un usuario debe tener al menos un rol asignado.", "USUARIO_SIN_ROLES")
    {
    }
}

/// <summary>
/// Excepción lanzada cuando datos requeridos están vacíos o inválidos.
/// </summary>
public class DatosInvalidosException : DomainException
{
    public DatosInvalidosException(string campo) 
        : base($"El campo '{campo}' es requerido y no puede estar vacío.", "DATOS_INVALIDOS")
    {
    }
}

/// <summary>
/// Excepción lanzada cuando el formato del email es inválido.
/// </summary>
public class EmailInvalidoException : DomainException
{
    public EmailInvalidoException(string email) 
        : base($"El formato del email '{email}' no es válido.", "EMAIL_INVALIDO")
    {
    }
}

/// <summary>
/// Excepción lanzada cuando se intenta cambiar a una contraseña igual a la actual.
/// </summary>
public class PasswordIgualAlActualException : DomainException
{
    public PasswordIgualAlActualException() 
        : base("La nueva contraseña no puede ser igual a la contraseña actual.", "PASSWORD_IGUAL_ACTUAL")
    {
    }
}
