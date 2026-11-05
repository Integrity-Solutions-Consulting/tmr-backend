using FluentValidation;
using tmr_backend.Features.Colaboradores.DTOs.Request;

namespace tmr_backend.Features.Colaboradores.Validators;

// Validador para editar colaboradores.
// Solo valida datos laborales (los personales no se editan aquí).
public class ActualizarColaboradorRequestValidator : AbstractValidator<ActualizarColaboradorRequest>
{
    public ActualizarColaboradorRequestValidator()
    {
        // Las reglas de dominio de los correos se validan en el servicio y solo si el correo
        // cambia, para que los datos antiguos no bloqueen otras ediciones ni "Activar".
        RuleFor(x => x.EmailCorporativo)
            .MaximumLength(100).WithMessage("El correo institucional no puede superar los 100 caracteres.")
            .EmailAddress().WithMessage("El correo institucional no tiene un formato válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.EmailCorporativo));

        // TEMPORAL-CONTRATO: un valor vacío ya no da error. Al revertir, volver a exigirlo con NotNull.
        RuleFor(x => x.IdTipoContrato)
            .GreaterThan(0).WithMessage("El tipo de contrato es requerido.");

        RuleFor(x => x.IdDepartamento)
            .GreaterThan(0).WithMessage("El departamento es requerido.");

        RuleFor(x => x.IdCargo)
            .GreaterThan(0).WithMessage("El cargo es requerido.");

        RuleFor(x => x.IdModoTrabajo)
            .GreaterThan(0)
            .When(x => x.IdModoTrabajo.HasValue)
            .WithMessage("La modalidad seleccionada no es válida.");

        RuleFor(x => x.IdCategoriaEmpleado)
            .GreaterThan(0)
            .When(x => x.IdCategoriaEmpleado.HasValue)
            .WithMessage("La categoría seleccionada no es válida.");

        // ================================================================
        // NUEVO: Validar reemplazo (si se envía, debe existir)
        // ================================================================
        RuleFor(x => x.IdEmpleadoReemplazo)
            .GreaterThan(0)
            .When(x => x.IdEmpleadoReemplazo.HasValue)
            .WithMessage("El colaborador de reemplazo no es válido.");
    }
}