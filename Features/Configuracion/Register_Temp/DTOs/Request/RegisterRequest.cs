namespace tmr_backend.Features.Configuracion.Register_Temp.DTOs.Request;

public record RegisterRequest(string Email, string Password, string ConfirmPassword);
