using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace tmr_backend.Infrastructure.OpenApi;

// Agrega el esquema de seguridad JWT Bearer a la documentación OpenAPI,
// para que Scalar muestre el botón global de Authorization.
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        // Definimos el esquema "Bearer".
        var securityScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "Ingrese el token JWT."
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = securityScheme;

        // Referencia al esquema para aplicarlo a los endpoints.
        var referenceScheme = new OpenApiSecuritySchemeReference("Bearer", document);

        var securityRequirement = new OpenApiSecurityRequirement
        {
            [referenceScheme] = new List<string>()
        };

        // Aplicamos el esquema a todos los endpoints.
        foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations.Values))
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(securityRequirement);
        }

        return Task.CompletedTask;
    }
}