namespace tmr_backend.Features.Configuracion.Usuarios.DTOs;

public record CrearUsuarioConfigRequest(
    int? idPersona,
    string email,
    string password,
    List<int>? rolesids = null,
    bool? debeCambiarPassword = true);

public record UpdateUsuarioRequest(
    int? idPersona = null,
    List<int>? rolesids = null,
    string? email = null,
    string? nombreusuario = null,
    string? password = null,
    bool? debeCambiarPassword = null);

public record ActivarUsuarioRequest(bool activo);

public record ChangePasswordRequest(
    string passwordactual,
    string passwordnueva,
    string confirmarpassword);

public record AsignarRolesRequest(
    List<int> rolesids);

public record UsuarioListaResponse(
    int id,
    int idUsuario,
    int? idPersona,
    string? numeroidentificacion,
    string? nombres,
    string? apellidos,
    string email,
    List<string> roles,
    bool activo,
    bool debecambiarpassword,
    DateTime? ultimologin);

public record UsuarioDetalleResponse(
    int id,
    int idUsuario,
    int? idPersona,
    string? numeroidentificacion,
    string? nombres,
    string? apellidos,
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

public record CrearUsuarioConfigResponse(
    int id,
    int idusuario,
    int? idpersona,
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
