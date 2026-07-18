using FluentValidation;
using tmr_backend.Features.Configuracion.Usuarios.DTOs;
using tmr_backend.Features.Configuracion.Usuarios.Validation;

namespace tmr_backend.Features.Configuracion.Usuarios.Validators;

public class CrearUsuarioConfigRequestValidator : AbstractValidator<CrearUsuarioConfigRequest>
{
    public CrearUsuarioConfigRequestValidator()
    {
        RuleFor(x => x.idPersona)
            .GreaterThan(0).WithMessage("La persona seleccionada no es valida.")
            .When(x => x.idPersona.HasValue);

        RuleFor(x => x.email)
            .NotEmpty().WithMessage("El email es requerido.")
            .EmailAddress().WithMessage("El email no tiene un formato valido.")
            .MaximumLength(100).WithMessage("El email no puede superar los 100 caracteres.");

        RuleFor(x => x.password)
            .NotEmpty().WithMessage("La contrasena es requerida.")
            .Must(PasswordValidator.EsValida)
            .WithMessage(PasswordValidator.ObtenerRequisitos());

        RuleFor(x => x.rolesids)
            .NotNull().WithMessage("Debe asignar al menos un rol.")
            .Must(roles => roles is { Count: > 0 })
            .WithMessage("Debe asignar al menos un rol.")
            .Must(roles => roles is null || roles.All(id => id > 0))
            .WithMessage("Todos los roles enviados deben tener un id valido.");
    }
}
