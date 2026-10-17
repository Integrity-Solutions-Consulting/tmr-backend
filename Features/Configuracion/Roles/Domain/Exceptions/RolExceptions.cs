using tmr_backend.Features.Configuracion.Usuarios.Domain.Exceptions;

namespace tmr_backend.Features.Configuracion.Roles.Domain.Exceptions;

public class RolYaExisteException : DomainException
{
    public RolYaExisteException(string nombre) 
        : base($"Ya existe un rol registrado con el nombre '{nombre}'.", "ROL_YA_EXISTE")
    {
    }
}

public class RolNoEncontradoException : DomainException
{
    public RolNoEncontradoException(int id) 
        : base($"No se encontró el rol con ID '{id}'.", "ROL_NO_ENCONTRADO")
    {
    }
}

public class DatosInvalidosRolException : DomainException
{
    public DatosInvalidosRolException(string mensaje) 
        : base(mensaje, "DATOS_INVALIDOS")
    {
    }
}
