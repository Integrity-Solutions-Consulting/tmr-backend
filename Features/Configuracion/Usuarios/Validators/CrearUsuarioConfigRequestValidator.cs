using FluentValidation;
using tmr_backend.Features.Configuracion.Usuarios.DTOs;
using tmr_backend.Features.Configuracion.Usuarios.Validation;

namespace tmr_backend.Features.Configuracion.Usuarios.Validators;

public class CrearUsuarioConfigRequestValidator : AbstractValidator<CrearUsuarioConfigRequest>
{
    public CrearUsuarioConfigRequestValidator()
    {
        RuleFor(x => x.numeroidentificacion)
            .NotEmpty().WithMessage("El numero de identificacion es requerido.")
            .Length(8, 20).WithMessage("El numero de identificacion debe tener entre 8 y 20 caracteres.");

        RuleFor(x => x.nombres)
            .NotEmpty().WithMessage("Los nombres son requeridos.")
            .Length(3, 100).WithMessage("Los nombres deben tener entre 3 y 100 caracteres.");

        RuleFor(x => x.apellidos)
            .NotEmpty().WithMessage("Los apellidos son requeridos.")
            .Length(3, 100).WithMessage("Los apellidos deben tener entre 3 y 100 caracteres.");

        RuleFor(x => x.email)
            .NotEmpty().WithMessage("El email es requerido.")
            .EmailAddress().WithMessage("El email no tiene un formato valido.")
            .MaximumLength(100).WithMessage("El email no puede superar los 100 caracteres.");

        RuleFor(x => x.password)
            .NotEmpty().WithMessage("La contrasena es requerida.")
            .Must(PasswordValidator.EsValida)
            .WithMessage(PasswordValidator.ObtenerRequisitos());

        RuleFor(x => x.idtipoidentificacion)
            .GreaterThan(0).WithMessage("El tipo de identificacion no es valido.")
            .When(x => x.idtipoidentificacion.HasValue);

        RuleFor(x => x.idgenero)
            .GreaterThan(0).WithMessage("El genero no es valido.")
            .When(x => x.idgenero.HasValue);

        RuleFor(x => x.idnacionalidad)
            .GreaterThan(0).WithMessage("La nacionalidad no es valida.")
            .When(x => x.idnacionalidad.HasValue);

        RuleFor(x => x.fechanacimiento)
            .LessThan(DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("La fecha de nacimiento debe ser anterior a hoy.")
            .When(x => x.fechanacimiento.HasValue);

        RuleFor(x => x.telefono)
            .MaximumLength(20).WithMessage("El telefono no puede superar los 20 caracteres.")
            .When(x => x.telefono is not null);

        RuleFor(x => x.direccion)
            .MaximumLength(255).WithMessage("La direccion no puede superar los 255 caracteres.")
            .When(x => x.direccion is not null);

        RuleFor(x => x.rolesids)
            .NotNull().WithMessage("Debe asignar al menos un rol.")
            .Must(roles => roles is { Count: > 0 })
            .WithMessage("Debe asignar al menos un rol.")
            .Must(roles => roles is null || roles.All(id => id > 0))
            .WithMessage("Todos los roles enviados deben tener un id valido.");
    }
}
