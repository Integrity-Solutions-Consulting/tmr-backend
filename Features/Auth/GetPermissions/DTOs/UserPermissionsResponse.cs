namespace tmr_backend.Features.Auth.GetPermissions.DTOs;

/// <summary>
/// Response con permisos granulares, módulos y acciones del usuario.
/// </summary>
public record UserPermissionsResponse(
    string[] Roles,
    ModulePermission[] Modules,
    MenuItem[] MenuItems
);

public record ModulePermission(
    int Id,
    string Nombre,
    string[] Acciones  // CREATE, READ, UPDATE, DELETE
);

public record MenuItem(
    int Id,
    string Nombre,
    string Ruta,
    string? Icono = null,
    int? Orden = null,
    MenuItem[]? Items = null
);
