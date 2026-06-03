namespace tmr_backend.Features.Configuracion.Usuarios.Domain.Exceptions;

/// <summary>
/// Excepción base para errores de lógica de negocio en el módulo de Usuarios.
/// Mantiene la arquitectura DDD dentro de la feature.
/// </summary>
public class DomainException : Exception
{
    public string? ErrorCode { get; set; }

    public DomainException(string message, string? errorCode = null) 
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public DomainException(string message, Exception? innerException, string? errorCode = null) 
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
