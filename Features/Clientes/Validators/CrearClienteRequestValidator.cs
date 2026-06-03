using FluentValidation;
using tmr_backend.Features.Clientes.DTOs.Request;

namespace tmr_backend.Features.Clientes.Validators;

// Validador para crear clientes.
public class CrearClienteRequestValidator : AbstractValidator<CrearClienteRequest>
{
    public CrearClienteRequestValidator()
    {
        // ── Información General ──────────────────────────────
        RuleFor(x => x.IdTipoIdentificacion)
            .GreaterThan(0).WithMessage("Debe seleccionar un tipo de identificación.");

        RuleFor(x => x.NumeroIdentificacion)
            .NotEmpty().WithMessage("El número de identificación es requerido.")
            .MaximumLength(20).WithMessage("El número de identificación no puede superar los 20 caracteres.");

        RuleFor(x => x.NombreComercial)
            .NotEmpty().WithMessage("El nombre comercial es requerido.")
            .MaximumLength(100).WithMessage("El nombre comercial no puede superar los 100 caracteres.");

        // ── Datos de contacto ────────────────────────────────
        RuleFor(x => x.Nombres)
            .NotEmpty().WithMessage("Los nombres son requeridos.")
            .MaximumLength(150).WithMessage("Los nombres no pueden superar los 150 caracteres.");

        RuleFor(x => x.Apellidos)
            .NotEmpty().WithMessage("Los apellidos son requeridos.")
            .MaximumLength(150).WithMessage("Los apellidos no pueden superar los 150 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es requerido.")
            .EmailAddress().WithMessage("El correo electrónico no tiene un formato válido.")
            .MaximumLength(100).WithMessage("El correo electrónico no puede superar los 100 caracteres.");

        RuleFor(x => x.Telefono)
            .NotEmpty().WithMessage("El teléfono es requerido.")
            .MaximumLength(20).WithMessage("El teléfono no puede superar los 20 caracteres.");

        RuleFor(x => x.Direccion)
            .NotEmpty().WithMessage("La dirección es requerida.")
            .MaximumLength(255).WithMessage("La dirección no puede superar los 255 caracteres.");
    }
}