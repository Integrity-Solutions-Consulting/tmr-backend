using System.Net;
using System.Text.Json;
using FluentValidation;
using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Shared.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context); // deja pasar el request normalmente
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Excepción no controlada: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();

        var (statusCode, message) = exception switch
        {
            FluentValidation.ValidationException  => (HttpStatusCode.BadRequest,      "Error de validación"),
            Exceptions.ConflictException          => (HttpStatusCode.Conflict,        "Conflicto con un recurso existente"),
            Exceptions.NotFoundException          => (HttpStatusCode.NotFound,        "Recurso no encontrado"),
            Exceptions.UnauthorizedException      => (HttpStatusCode.Unauthorized,    "No autorizado"),

            KeyNotFoundException                  => (HttpStatusCode.NotFound,        "Recurso no encontrado"),
            UnauthorizedAccessException           => (HttpStatusCode.Unauthorized,    "No autorizado"),
            ArgumentException                     => (HttpStatusCode.BadRequest,      "Solicitud inválida"),
            InvalidOperationException             => (HttpStatusCode.BadRequest,      "Operación inválida"),
            _                                     => (HttpStatusCode.InternalServerError, "Ocurrió un error inesperado")
        };

        IEnumerable<ApiError> errors = exception is FluentValidation.ValidationException fluentEx
            ? fluentEx.Errors.Select(e => new ApiError(e.PropertyName, e.ErrorMessage))
            : [new ApiError("server", env.IsDevelopment() ? exception.ToString() : exception.Message)];

        var code = (int)statusCode;

        var response = ApiResponse<object>.Fail(
            code,
            message,
            errors
        );

        context.Response.StatusCode  = code;
        context.Response.ContentType = "application/json";

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder                     = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        await context.Response.WriteAsync(json);
    }
}