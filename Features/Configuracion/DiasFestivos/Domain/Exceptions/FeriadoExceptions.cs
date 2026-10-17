using tmr_backend.Features.Configuracion.Usuarios.Domain.Exceptions;

namespace tmr_backend.Features.Configuracion.DiasFestivos.Domain.Exceptions;

public class FeriadoNoEncontradoException : DomainException
{
    public FeriadoNoEncontradoException(int id) 
        : base($"No se encontró el feriado con ID '{id}'.", "FERIADO_NO_ENCONTRADO")
    {
    }
}

public class FeriadoYaExisteException : DomainException
{
    public FeriadoYaExisteException(string nombre) 
        : base($"Ya existe un feriado activo con el nombre '{nombre}' para la misma fecha.", "FERIADO_YA_EXISTE")
    {
    }
}

public class DatosInvalidosFeriadoException : DomainException
{
    public DatosInvalidosFeriadoException(string mensaje) 
        : base(mensaje, "DATOS_INVALIDOS_FERIADO")
    {
    }
}
