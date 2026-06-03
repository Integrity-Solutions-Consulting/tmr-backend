using System.Globalization;
using System.Net;
using FluentValidation;
using tmr_backend.Features.Auth.DTOs.Request;

namespace tmr_backend.Features.Auth.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.IdGenero)
            .GreaterThan(0).WithMessage("El género es requerido.");

        RuleFor(x => x.IdNacionalidad)
            .GreaterThan(0).WithMessage("La nacionalidad es requerida.");

        RuleFor(x => x.IdTipoIdentificacion)
            .GreaterThan(0).WithMessage("El tipo de identificación es requerido.");

        RuleFor(x => x.Numeroidentificacion)
            .NotEmpty().WithMessage("El número de identificación es requerido.")
            .Must((request, numero) =>
            {
                if (string.IsNullOrWhiteSpace(numero)) return false;

                return request.TipoIdentificacion switch
                {
                    "C" => numero.Length == 10 && numero.All(char.IsDigit),
                    "R" => numero.Length == 13 && numero.All(char.IsDigit),
                    "P" => numero.Length <= 20 && numero.All(char.IsLetterOrDigit),
                    "O" => numero.Length <= 20,
                    _   => false
                };
            })
            .WithMessage(request => request.TipoIdentificacion switch
            {
                "C" => "La cédula debe tener exactamente 10 dígitos numéricos.",
                "R" => "El RUC debe tener exactamente 13 dígitos numéricos.",
                "P" => "El pasaporte debe tener máximo 20 caracteres alfanuméricos.",
                "O" => "El documento debe tener máximo 20 caracteres.",
                _   => "El número de identificación no es válido."
            });

        RuleFor(x => x.Nombres)
            .NotEmpty().WithMessage("El nombre es requerido.")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres.");

        RuleFor(x => x.Apellidos)
            .NotEmpty().WithMessage("Los apellidos son requeridos.")
            .MaximumLength(100).WithMessage("Los apellidos no pueden superar los 100 caracteres.");

        RuleFor(x => x.CorreoContacto)
            .NotEmpty().WithMessage("El correo de contacto es requerido.")
            .EmailAddress().WithMessage("El correo de contacto no tiene un formato válido.")
            .MaximumLength(255).WithMessage("El correo de contacto no puede superar los 255 caracteres.");

        RuleFor(x => x.TipoPersona)
            .NotEmpty().WithMessage("El tipo de persona es requerido.")
            .Must(x => x == "NATURAL" || x == "JURIDICA")
            .WithMessage("El tipo de persona debe ser 'NATURAL' o 'JURIDICA'.");
        
        RuleFor(x => x.FechaNacimiento)
            .NotEmpty().WithMessage("La fecha de nacimiento es requerida.")
            .Must(fecha => DateOnly.TryParseExact(fecha, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .WithMessage("La fecha de nacimiento debe tener el formato dd-MM-yyyy.")
            .Must(fecha =>
            {
                if (!DateOnly.TryParseExact(fecha, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    return false;
                return parsed < DateOnly.FromDateTime(DateTime.Today);
            })
            .WithMessage("La fecha de nacimiento debe ser anterior a hoy.");

        RuleFor(x => x.Telefono)
            .MaximumLength(20).WithMessage("El teléfono no puede superar los 20 caracteres.")
            .When(x => x.Telefono is not null);

        RuleFor(x => x.Direccion)
            .MaximumLength(255).WithMessage("La dirección no puede superar los 255 caracteres.")
            .When(x => x.Direccion is not null);

        RuleFor(x => x.IP)
            .NotEmpty().WithMessage("La IP es requerida.")
            .MaximumLength(45).WithMessage("La IP no puede superar los 45 caracteres.")
            .Must(ip => IPAddress.TryParse(ip, out _))
            .WithMessage("La IP no tiene un formato válido.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es requerido.")
            //.EmailAddress().WithMessage("El email no tiene un formato válido.")
            .MaximumLength(255).WithMessage("El email no puede superar los 255 caracteres.");
        
        RuleFor(x => x.Usuario)
            .NotEmpty().WithMessage("El usuario es requerido.")
            .MaximumLength(50).WithMessage("El usuario no puede superar los 50 caracteres.");

        // RuleFor(x => x.Password)
        //     .NotEmpty().WithMessage("El password es requerido.")
        //     .MinimumLength(8).WithMessage("Mínimo 8 caracteres.")
        //     .Matches("[A-Z]").WithMessage("Debe tener al menos una mayúscula.")
        //     .Matches("[a-z]").WithMessage("Debe tener al menos una minúscula.")
        //     .Matches("[0-9]").WithMessage("Debe tener al menos un número.")
        //     .Matches("[^a-zA-Z0-9]").WithMessage("Debe tener al menos un carácter especial.");
    }
}
