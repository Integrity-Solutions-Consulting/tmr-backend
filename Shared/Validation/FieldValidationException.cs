using tmr_backend.Shared.Wrappers;

namespace tmr_backend.Shared.Validation;

public sealed class FieldValidationException(IReadOnlyList<ApiError> errors) : Exception("Error de validación")
{
    public IReadOnlyList<ApiError> Errors { get; } = errors;
}
