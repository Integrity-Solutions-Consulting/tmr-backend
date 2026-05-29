using Microsoft.AspNetCore.Authorization;

namespace tmr_backend.Features.Auth.Authorization;

/// <summary>
/// Requirement para verificar que usuario tiene permiso sobre un módulo + acción.
/// Soporta acciones: READ/VIEW, CREATE, UPDATE/EDIT, DELETE
/// </summary>
public class HasModulePermissionRequirement : IAuthorizationRequirement
{
    public string ModuleName { get; set; }
    public string Action { get; set; }  // READ, CREATE, UPDATE, DELETE, VIEW, EDIT

    public HasModulePermissionRequirement(string moduleName, string action)
    {
        ModuleName = moduleName;
        Action = action;
    }

    public override string ToString() => $"Module: {ModuleName}, Action: {Action}";
}
