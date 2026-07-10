using tmr_backend.Features.Configuracion.Usuarios.Domain.Exceptions;

namespace tmr_backend.Features.Configuracion.Catalogos.Domain.Exceptions;

public class CatalogoNoEncontradoException : DomainException
{
    public CatalogoNoEncontradoException(int id) 
        : base($"No se encontró el catálogo maestro con ID '{id}'.", "CATALOGO_NO_ENCONTRADO")
    {
    }
    
    public CatalogoNoEncontradoException(string codigo) 
        : base($"No se encontró el catálogo maestro con código '{codigo}'.", "CATALOGO_NO_ENCONTRADO")
    {
    }
}

public class DetalleNoEncontradoException : DomainException
{
    public DetalleNoEncontradoException(int id) 
        : base($"No se encontró el detalle de catálogo con ID '{id}'.", "DETALLE_NO_ENCONTRADO")
    {
    }
}

public class DetalleCodigoDuplicadoException : DomainException
{
    public DetalleCodigoDuplicadoException(string codigoValor, int idCatalogo) 
        : base($"Ya existe un detalle con el código '{codigoValor}' para este catálogo.", "DETALLE_CODIGO_DUPLICADO")
    {
    }
}

public class DatosInvalidosDetalleException : DomainException
{
    public DatosInvalidosDetalleException(string mensaje) 
        : base(mensaje, "DATOS_INVALIDOS_DETALLE")
    {
    }
}
