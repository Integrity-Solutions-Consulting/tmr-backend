namespace tmr_backend.Features.Clientes.DTOs;

public record CrearClienteRequest(string Nombre, string Empresa);

public record ClienteResponse(Guid Id, string Nombre, string Empresa, bool Activo, DateTime FechaCreacion);

public record ActualizarClienteRequest(string Nombre, string Empresa);

public record ClienteLookupResponse(int Id, string Nombre);
