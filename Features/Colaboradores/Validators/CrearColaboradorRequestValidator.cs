using FluentValidation;
using tmr_backend.Features.Colaboradores.Domain;
using tmr_backend.Features.Colaboradores.DTOs.Request;

namespace tmr_backend.Features.Colaboradores.Validators;

// Validador para crear colaboradores.
// Valida datos de Persona + datos de Empleado.
public class CrearColaboradorRequestValidator : AbstractValidator<CrearColaboradorRequest>
{
    public CrearColaboradorRequestValidator()
    {
        // ── Datos de persona ──────────────────────────────

        RuleFor(x => x.TipoPersona)
            .NotEmpty().WithMessage("El tipo de persona es requerido.")
            .Must(x => x == "NATURAL" || x == "JURIDICA")
            .WithMessage("El tipo de persona debe ser NATURAL o JURIDICA.");

        RuleFor(x => x.NumeroIdentificacion)
            .NotEmpty().WithMessage("La identificación es requerida.")
            .MaximumLength(20).WithMessage("La identificación no puede superar los 20 caracteres.");

        RuleFor(x => x.IdTipoIdentificacion)
            .NotNull().WithMessage("El tipo de identificación es requerido.")
            .GreaterThan(0).WithMessage("El tipo de identificación es requerido.")
            .When(x => x.TipoPersona == "NATURAL");

        RuleFor(x => x.Nombres)
            .NotEmpty().WithMessage("Los nombres son requeridos.")
            .MaximumLength(100).WithMessage("Los nombres no pueden superar los 100 caracteres.");

        RuleFor(x => x.Apellidos)
            .NotEmpty().WithMessage("Los apellidos son requeridos.")
            .MaximumLength(100).WithMessage("Los apellidos no pueden superar los 100 caracteres.");

        // Debe existir al menos un correo: personal o institucional.
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.EmailCorporativo))
            .WithMessage("Ingrese al menos un correo: personal o institucional.");

        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(100).WithMessage("El correo personal no puede superar los 100 caracteres.")
            .EmailAddress().WithMessage("El correo personal no tiene un formato válido.")
            .Must(x => !CorreoInstitucional.EsInstitucional(x))
            .WithMessage($"El correo personal no puede ser {CorreoInstitucional.Dominio}.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.EmailCorporativo)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(100).WithMessage("El correo institucional no puede superar los 100 caracteres.")
            .EmailAddress().WithMessage("El correo institucional no tiene un formato válido.")
            .Must(CorreoInstitucional.EsInstitucional)
            .WithMessage($"El correo institucional debe ser {CorreoInstitucional.Dominio}.")
            .When(x => !string.IsNullOrWhiteSpace(x.EmailCorporativo));

        RuleFor(x => x.Telefono)
            .MaximumLength(20).WithMessage("El teléfono no puede superar los 20 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Telefono));

        RuleFor(x => x.Direccion)
            .MaximumLength(255).WithMessage("La dirección no puede superar los 255 caracteres.")
            .When(x => !string.IsNullOrWhiteSpace(x.Direccion));

        // ── Contrato ──────────────────────────────────────

        RuleFor(x => x.IdEmpresaCatalogo)
            .GreaterThan(0).WithMessage("La empresa es requerida.");

        RuleFor(x => x.IdTipoContrato)
            .GreaterThan(0).WithMessage("El tipo de contrato es requerido.");

        // ── Datos laborales ───────────────────────────────

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

        RuleFor(x => x.AniosExperiencia)
            .GreaterThanOrEqualTo(0).WithMessage("Los años de experiencia no pueden ser negativos.")
            .LessThanOrEqualTo(50).WithMessage("Los años de experiencia no pueden superar 50 años.")
            .When(x => x.AniosExperiencia.HasValue);

        // ================================================================
        // NUEVO: Validar reemplazo (si se envía, debe existir)
        // ================================================================
        RuleFor(x => x.IdEmpleadoReemplazo)
            .GreaterThan(0)
            .When(x => x.IdEmpleadoReemplazo.HasValue)
            .WithMessage("El colaborador de reemplazo no es válido.");
    }
}