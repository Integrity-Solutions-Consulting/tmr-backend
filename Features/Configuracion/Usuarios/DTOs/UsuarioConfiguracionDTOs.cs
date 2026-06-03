namespace tmr_backend.Features.Configuracion.Usuarios.DTOs;

public record CreateUsuarioRequest(
    string numeroidentificacion,
    string nombres,
    string apellidos,
    string email,
    string password,
    int? idtipoidentificacion = null,
    int? idgenero = null,
    int? idnacionalidad = null,
    DateOnly? fechanacimiento = null,
    string? telefono = null,
    string? direccion = null,
    List<int>? rolesids = null);

public record UpdateUsuarioRequest(
    string nombres,
    string apellidos,
    int? idgenero = null,
    int? idnacionalidad = null,
    DateOnly? fechanacimiento = null,
    string? telefono = null,
    string? direccion = null,
    List<int>? rolesids = null);

public record ChangePasswordRequest(
    string passwordactual,
    string passwordnueva,
    string confirmarpassword);

public record AsignarRolesRequest(
    List<int> rolesids);

public record UsuarioListaResponse(
    int id,
    string numeroidentificacion,
    string nombres,
    string apellidos,
    string email,
    List<string> roles,
    bool activo,
    DateTime? ultimologin);

public record UsuarioDetalleResponse(
    int id,
    string numeroidentificacion,
    string nombres,
    string apellidos,
    string email,
    int? idtipoidentificacion,
    string? tipoidentificacionvalor,
    int? idgenero,
    string? generovalor,
    int? idnacionalidad,
    string? nacionalidadvalor,
    DateOnly? fechanacimiento,
    string? telefono,
    string? direccion,
    List<RolResponse> roles,
    bool activo,
    bool debecambiarpassword,
    DateTime? ultimologin,
    DateTime fechacreacion,
    string usuariocreacion,
    DateTime? fechamodificacion,
    string? usuariomodificacion);

public record RolResponse(
    int id,
    string nombre,
    string? descripcion);

public record PaginatedResponse<T>(
    List<T> items,
    int totalcount,
    int pagenumber,
    int pagesize,
    int totalpages);

public record CrearUsuarioResponse(
    int id,
    string numeroidentificacion,
    string nombres,
    string apellidos,
    string email,
    List<RolResponse> roles,
    bool activo,
    bool debecambiarpassword,
    DateTime fechacreacion);

public record ErrorResponse(
    string mensaje,
    string? errorcode = null,
    DateTime timestamp = default);

public record SuccessResponse(
    string mensaje,
    DateTime timestamp = default);

public record ObtenerUsuariosQuery(
    string? search = null,
    bool? activo = null,
    int pagenumber = 1,
    int pagesize = 10);
