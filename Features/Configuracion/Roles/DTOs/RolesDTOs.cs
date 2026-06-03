namespace tmr_backend.Features.Configuracion.Roles.DTOs;

public record CreateRolRequest(string nombre, string descripcion, List<int> modulosids);
public record UpdateRolRequest(string nombre, string descripcion, List<int> modulosids);

public record RolResponse(int id, string nombre, string descripcion, List<ModuloResponse> modulos, bool activo);

public record ModuloResponse(int id, string nombre);

public record SuccessResponse(string Mensaje);
