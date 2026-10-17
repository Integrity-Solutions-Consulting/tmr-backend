using System.ComponentModel;
using tmr_backend.Features.Lideres.DTOs;

namespace tmr_backend.Features.Lideres.DTOs.Response;
public record PersonaResponse(
    int Id,
    string Nombre,
    string Apellido,
    string Correo
);
